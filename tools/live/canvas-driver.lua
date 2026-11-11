-- The canvas driver of the live tests of OBSClient (OBSClient.Tests/Live/LiveCanvasModelTests.cs).
--
-- OBS Studio makes a canvas only for a plugin or a script that asks (in its source the frontend calls AddCanvas only for
-- obs_frontend_add_canvas, and obs-websocket has no request that makes one). A plugin (or a script) makes one with obs_canvas_create,
-- directly or through obs_frontend_add_canvas. This script is loaded by the scene collection that tools/live/run-live-tests.ps1 makes for the canvas
-- tests, and does what the tests ask for in a text file: one command to a line, run in order, once. What it did goes to a second file (the
-- commands file with .out after it), one line for each command: "<number> <command> <label> <what happened>".
--
-- Commands (the label is the name that the test gives the canvas, without spaces; the name of a canvas can have them):
--   ping <label>                          answers "ok": the script is loaded and polls
--   create <label> <flags> <name>         obs_canvas_create(name, no video, flags), as a plugin makes a canvas
--   rename <label> <name>                 obs_canvas_set_name
--   remove <label>                        obs_canvas_remove: the script keeps its reference, so OBS Studio holds on to the canvas
--   release <label>                       obs_canvas_release: lets go of the reference that the script holds
--
-- It holds a reference on each canvas that it makes, until a test says release or the collection unloads (then it removes and releases the
-- ones that are left, so that nothing that a test made outlives it).

obs = obslua

local commands_path = "__COMMANDS_PATH__"
local results_path = commands_path .. ".out"
local canvases = {}
local processed = 0

-- An answer is kept until it is written: the test may have the answers file open at the moment (a program that has a file open for reading
-- without sharing it for writing makes the open for appending fail), and an answer that is lost is a test that waits for nothing.
local unwritten = {}
local was_busy = false

local function flush_results()
    if #unwritten == 0 then
        return
    end

    local file = io.open(results_path, "a")
    if file then
        file:write(table.concat(unwritten, "\n") .. "\n")
        file:close()
        unwritten = {}
        was_busy = false
    elseif not was_busy then
        was_busy = true
        obs.script_log(obs.LOG_WARNING, "canvas driver: the answers file is busy, " .. #unwritten .. " answer(s) will be written at the next tick")
    end
end

local function write_result(text)
    unwritten[#unwritten + 1] = text
    flush_results()
end

local function describe(canvas)
    return "name=" .. obs.obs_canvas_get_name(canvas) .. " uuid=" .. obs.obs_canvas_get_uuid(canvas) .. " flags=" .. obs.obs_canvas_get_flags(canvas)
end

local function run(index, line)
    local verb, label, rest = line:match("^(%S+)%s+(%S+)%s*(.*)$")
    if verb == nil then
        write_result(index .. " ? ? not a command")
        return
    end

    if verb == "ping" then
        write_result(index .. " ping " .. label .. " ok")
    elseif verb == "create" then
        local flags, name = rest:match("^(%d+)%s+(.+)$")
        if flags == nil then
            write_result(index .. " create " .. label .. " bad arguments")
            return
        end

        local canvas = obs.obs_canvas_create(name, nil, tonumber(flags))
        if canvas == nil then
            write_result(index .. " create " .. label .. " failed")
            return
        end

        canvases[label] = canvas
        write_result(index .. " create " .. label .. " ok " .. describe(canvas))
    elseif verb == "rename" then
        local canvas = canvases[label]
        if canvas == nil then
            write_result(index .. " rename " .. label .. " unknown")
            return
        end

        obs.obs_canvas_set_name(canvas, rest)
        write_result(index .. " rename " .. label .. " ok " .. describe(canvas))
    elseif verb == "remove" then
        local canvas = canvases[label]
        if canvas == nil then
            write_result(index .. " remove " .. label .. " unknown")
            return
        end

        obs.obs_canvas_remove(canvas)
        write_result(index .. " remove " .. label .. " ok " .. describe(canvas) .. " removed=" .. tostring(obs.obs_canvas_removed(canvas)))
    elseif verb == "release" then
        local canvas = canvases[label]
        if canvas == nil then
            write_result(index .. " release " .. label .. " unknown")
            return
        end

        obs.obs_canvas_release(canvas)
        canvases[label] = nil
        write_result(index .. " release " .. label .. " ok")
    else
        write_result(index .. " " .. verb .. " " .. label .. " unknown command")
    end
end

local function poll()
    flush_results()

    local file = io.open(commands_path, "r")
    if not file then
        return
    end

    local text = file:read("*a")
    file:close()

    -- Only the lines that end with a newline: the test may be in the middle of writing the last one.
    local index = 0
    for line in text:gmatch("([^\n]*)\n") do
        index = index + 1
        if index > processed then
            processed = index
            obs.script_log(obs.LOG_INFO, "canvas driver: " .. index .. " " .. line)
            run(index, (line:gsub("\r$", "")))
        end
    end
end

function script_description()
    return "Makes, renames and removes canvases for the live tests of OBSClient."
end

function script_load(settings)
    obs.script_log(obs.LOG_INFO, "canvas driver: loaded, obs_canvas_create is a " .. type(obs.obs_canvas_create) .. ", commands in " .. commands_path)
    obs.timer_add(poll, 100)
end

function script_unload()
    obs.timer_remove(poll)
    for _, canvas in pairs(canvases) do
        obs.obs_canvas_remove(canvas)
        obs.obs_canvas_release(canvas)
    end

    canvases = {}
end
