using System.Globalization;
using System.Text;
using OBSStudioClient.Exceptions;
using OBSStudioClient.Model;

// OBS Studio by NAME. The plain ObsClient asks for UUIDs, scene item ids and indexes. An ObsModel reads your scenes, inputs and scene
// items when it connects, follows the events of OBS Studio, and keeps a copy in memory, so that you can say what you mean:
//
//   scene "Gaming"      mute "Mic"      hide "Gaming" "Webcam"      volume "Mic" -12
//
// Run it (Visual Studio: open this project and press F5; or: dotnet run --project samples/ModelSample -- [host] [port]).
// It only reads until you type a command that changes something, and it changes only what you name.
//
// The password is read from the environment variable OBS_PASSWORD (leave it unset when OBS Studio asks for none). The host is localhost
// and the port 4455, which is the port that OBS Studio uses unless you change it, unless you give others. When more than one OBS Studio
// is running (each has its own port), the port is what decides which one you get.
var host = args.Length > 0 ? args[0] : "localhost";
var port = 4455;
if (args.Length > 1 && !(int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535))
{
    // A port that is not one is not replaced by the default: that would connect to another OBS Studio than the one that was meant.
    Console.WriteLine($"'{args[1]}' is not a port. Give a number from 1 to 65535: dotnet run --project samples/ModelSample -- [host] [port]");
    return 2;
}

var password = Environment.GetEnvironmentVariable("OBS_PASSWORD") ?? string.Empty;

// IgnoreCase: "mic" finds "Mic". An exact match always wins, and two names that differ only by case are refused as ambiguous.
await using var model = new ObsModel(new ObsModelOptions { IgnoreCase = true });

// The changes are raised one at a time, on a thread of their own, in the order in which they happened: also the ones that you make yourself,
// and the ones that somebody makes in OBS Studio while this runs. Try it: click a scene in OBS Studio.
var watching = true;
model.Changed += (_, e) =>
{
    if (watching)
    {
        Console.WriteLine($"  (OBS Studio) {e}");
    }
};

Console.WriteLine($"Connecting to OBS Studio at {host}:{port}...");
try
{
    // Returns when the model is complete: from here on, everything that is read is answered from memory.
    await model.ConnectAsync(host, port, password);
}
catch (ObsNotConnectedException ex)
{
    Console.WriteLine($"Could not connect: {ex.Message}");
    Console.WriteLine("Is OBS Studio running, is its WebSocket server enabled (Tools, WebSocket Server Settings), and is OBS_PASSWORD right?");
    return 1;
}
catch (ObsVersionNotSupportedException ex)
{
    Console.WriteLine(ex.Message);
    return 1;
}
catch (TimeoutException)
{
    Console.WriteLine("OBS Studio did not finish telling about its scenes in time.");
    return 1;
}

// Nothing is hidden: the plain client that the model is built on is still there, for every request that the model does not offer yet.
var version = await model.Client.GetVersion();
Console.WriteLine($"Connected to OBS Studio {version.ObsVersion} (obs-websocket {version.ObsWebSocketVersion}).");
PrintStatus();
PrintScenes();
PrintInputs();
PrintOutputs();
Console.WriteLine();
PrintHelp();

while (true)
{
    Console.Write("> ");
    var line = await Task.Run(Console.ReadLine);
    if (line is null)
    {
        break;
    }

    var words = Split(line);
    if (words.Count == 0)
    {
        continue;
    }

    try
    {
        if (!await RunAsync(words))
        {
            break;
        }
    }
    catch (Exception ex) when (ex is UserError or ObsClientException or InvalidOperationException or ArgumentException)
    {
        // What the library says is meant to be read: it names what holds a name, what was changed and what was not.
        Console.WriteLine($"Not done: {ex.Message}");
    }
}

return 0;

async Task<bool> RunAsync(List<string> words)
{
    var command = words[0].ToLowerInvariant();
    string Arg(int position, string what) => position < words.Count ? words[position] : throw new UserError($"Missing {what}. Type help.");

    switch (command)
    {
        case "help":
        case "?":
            PrintHelp();
            break;
        case "status":
            PrintStatus();
            break;
        case "scenes":
            PrintScenes();
            break;
        case "inputs":
            PrintInputs();
            break;
        case "outputs":
            PrintOutputs();
            break;
        case "filters":
            PrintFilters(FindSource(Arg(1, "scene or input name")));
            break;
        case "filterkinds":
            Console.WriteLine(string.Join(", ", await model.Client.GetSourceFilterKindList()));
            break;
        case "filter":
            {
                // A filter is found by its name within the scene or the input that has it.
                var filter = FindSource(Arg(1, "scene or input name")).Filters[Arg(2, "filter name")];
                switch (Arg(3, "on, off or toggle").ToLowerInvariant())
                {
                    case "on":
                        await filter.SetEnabledAsync(true);
                        break;
                    case "off":
                        await filter.SetEnabledAsync(false);
                        break;
                    case "toggle":
                        await filter.ToggleEnabledAsync();
                        break;
                    default:
                        throw new UserError("What to do with the filter: on, off or toggle.");
                }

                Console.WriteLine($"'{filter.Name}' on '{filter.Source.Name}' is {(filter.IsEnabled ? "on" : "off")} now.");
                break;
            }

        case "newfilter":
            {
                var source = FindSource(Arg(1, "scene or input name"));
                var filter = await source.CreateFilterAsync(Arg(2, "name of the new filter"), Arg(3, "kind of filter (type filterkinds for the list)"));
                Console.WriteLine($"Made filter '{filter.Name}' ({filter.Kind}) on '{source.Name}', at position {filter.Index}.");
                break;
            }

        case "renamefilter":
            {
                var filter = FindSource(Arg(1, "scene or input name")).Filters[Arg(2, "filter name")];
                var before = filter.Name;
                await filter.RenameAsync(Arg(3, "new name"));
                Console.WriteLine($"'{before}' is called '{filter.Name}' now.");
                break;
            }

        case "movefilter":
            {
                var filter = FindSource(Arg(1, "scene or input name")).Filters[Arg(2, "filter name")];
                if (!int.TryParse(Arg(3, "position, 0 is the first"), NumberStyles.None, CultureInfo.InvariantCulture, out var position))
                {
                    throw new UserError("The position is a number, 0 for the first filter.");
                }

                await filter.SetIndexAsync(position);
                Console.WriteLine($"'{filter.Name}' is at position {filter.Index} now.");
                break;
            }

        case "removefilter":
            {
                var filter = FindSource(Arg(1, "scene or input name")).Filters[Arg(2, "filter name")];
                var name = filter.Name;
                await filter.RemoveAsync();
                Console.WriteLine($"Removed filter '{name}'.");
                break;
            }
        case "stream":
        case "record":
        case "virtualcam":
        case "replay":
            await RunOutputAsync(command, Arg(1, "action: start, stop, toggle (record: pause, resume; replay: save)"));
            break;
        case "watch":
            watching = Arg(1, "on or off").Equals("on", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine(watching ? "Changes in OBS Studio are shown." : "Changes in OBS Studio are not shown.");
            break;
        case "scene":
            // The scene is found by name, and the call returns when the model shows it on the air.
            await model.Scenes[Arg(1, "scene name")].SwitchToAsync();
            Console.WriteLine($"On air now: {model.ProgramScene?.Name}");
            break;
        case "mute":
        case "unmute":
            {
                var input = model.Inputs[Arg(1, "input name")];
                await input.SetMutedAsync(command == "mute");
                Console.WriteLine($"'{input.Name}' is {(input.IsMuted ? "muted" : "not muted")} now.");
                break;
            }

        case "togglemute":
            {
                var input = model.Inputs[Arg(1, "input name")];
                var muted = await input.ToggleMuteAsync();
                Console.WriteLine($"'{input.Name}' is {(muted ? "muted" : "not muted")} now.");
                break;
            }

        case "volume":
            {
                var input = model.Inputs[Arg(1, "input name")];
                if (!double.TryParse(Arg(2, "volume in dB, for example -12"), NumberStyles.Float, CultureInfo.InvariantCulture, out var decibels))
                {
                    throw new UserError("The volume is a number of decibels, for example -12 (0 is full volume).");
                }

                await input.SetVolumeDbAsync(decibels);
                Console.WriteLine($"'{input.Name}' is at {input.VolumeDb:0.0} dB now.");
                break;
            }

        case "show":
        case "hide":
            {
                // A scene item is found by the name of the scene and the name of the source that it shows.
                var item = model.Scenes[Arg(1, "scene name")].Items[Arg(2, "source name")];
                await item.SetEnabledAsync(command == "show");
                Console.WriteLine($"'{item.Source.Name}' is {(item.IsEnabled ? "shown" : "hidden")} in '{item.Scene.Name}' now.");
                break;
            }

        case "toggle":
            {
                var item = model.Scenes[Arg(1, "scene name")].Items[Arg(2, "source name")];
                var enabled = await item.ToggleEnabledAsync();
                Console.WriteLine($"'{item.Source.Name}' is {(enabled ? "shown" : "hidden")} in '{item.Scene.Name}' now.");
                break;
            }

        case "newscene":
            {
                var scene = await model.CreateSceneAsync(Arg(1, "name of the new scene"));
                Console.WriteLine($"Made scene '{scene.Name}'.");
                break;
            }

        case "newcolor":
            {
                // An input is made in a scene, with the kind of input that OBS Studio calls it by (the plain client lists them).
                var scene = model.Scenes[Arg(1, "scene name")];
                var kind = (await model.Client.GetInputKindList())
                    .Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal))
                    .OrderByDescending(candidate => candidate, StringComparer.Ordinal)
                    .FirstOrDefault() ?? throw new UserError("This OBS Studio has no color source.");
                var input = await scene.CreateInputAsync(Arg(2, "name of the new input"), kind, new { color = 4278190335L, width = 640, height = 360 });
                Console.WriteLine($"Made input '{input.Name}', and a scene item that shows it in '{scene.Name}'.");
                break;
            }

        case "renamescene":
            {
                var scene = model.Scenes[Arg(1, "scene name")];
                var before = scene.Name;
                await scene.RenameAsync(Arg(2, "new name"));
                Console.WriteLine($"'{before}' is called '{scene.Name}' now.");
                break;
            }

        case "renameinput":
            {
                var input = model.Inputs[Arg(1, "input name")];
                var before = input.Name;
                await input.RenameAsync(Arg(2, "new name"));
                Console.WriteLine($"'{before}' is called '{input.Name}' now.");
                break;
            }

        case "removescene":
            {
                var scene = model.Scenes[Arg(1, "scene name")];
                var name = scene.Name;
                await scene.RemoveAsync();
                Console.WriteLine($"Removed scene '{name}'.");
                break;
            }

        case "removeinput":
            {
                var input = model.Inputs[Arg(1, "input name")];
                var name = input.Name;
                await input.RemoveAsync();
                Console.WriteLine($"Removed input '{name}', and the scene items that showed it.");
                break;
            }

        case "quit":
        case "exit":
            return false;
        default:
            Console.WriteLine($"I do not know '{words[0]}'. Type help.");
            break;
    }

    return true;
}

void PrintStatus()
{
    Console.WriteLine($"State: {model.State}. On air: {model.ProgramScene?.Name ?? "(nothing)"}. Studio mode: {(model.IsStudioModeEnabled ? "on" : "off")}.");
}

void PrintScenes()
{
    Console.WriteLine($"Scenes ({model.Scenes.Count}), * is on the air:");
    foreach (var scene in model.Scenes)
    {
        Console.WriteLine($"  {(scene.IsProgram ? "*" : " ")} {scene.Name}{(scene.IsGroup ? " (group)" : string.Empty)}{FilterCount(scene)}");
        foreach (var item in scene.Items)
        {
            Console.WriteLine($"        {item.Source.Name}  [{(item.IsEnabled ? "shown" : "hidden")}{(item.IsLocked ? ", locked" : string.Empty)}]");
        }
    }
}

void PrintInputs()
{
    Console.WriteLine($"Inputs ({model.Inputs.Count}):");
    foreach (var input in model.Inputs)
    {
        var audio = input.HasAudio ? $"{(input.IsMuted ? "muted" : "not muted")}, {input.VolumeDb:0.0} dB" : "no audio";
        Console.WriteLine($"    {input.Name}  ({input.Kind}; {audio}{FilterCount(input)})");
    }
}

// What a scene or an input says about its filters in the lists: nothing when it has none.
static string FilterCount(ObsSource source)
{
    return source.Filters.Count == 0 ? string.Empty : (source is ObsScene ? "  " : "; ") + $"{source.Filters.Count} filter{(source.Filters.Count == 1 ? string.Empty : "s")}";
}

void PrintOutputs()
{
    Console.WriteLine("Outputs:");
    foreach (var output in model.Outputs)
    {
        Console.WriteLine($"    {output.Name}: {output.Status}");
    }
}

// A scene or an input, by name. OBS Studio before 31.1 can have a scene and an input with the same name: the scene is taken then.
ObsSource FindSource(string name)
{
    return (ObsSource?)model.Scenes.Find(name) ?? model.Inputs.Find(name) ?? throw new UserError($"There is no scene or input named '{name}'.");
}

void PrintFilters(ObsSource source)
{
    Console.WriteLine($"Filters of {(source is ObsScene ? "scene" : "input")} '{source.Name}' ({source.Filters.Count}), from the first:");
    foreach (var filter in source.Filters)
    {
        Console.WriteLine($"    {filter.Index}. {filter.Name}  ({filter.Kind}; {(filter.IsEnabled ? "on" : "off")})");
    }
}

async Task RunOutputAsync(string which, string action)
{
    var output = which switch
    {
        "stream" => model.Outputs.Stream,
        "record" => model.Outputs.Record,
        "virtualcam" => model.Outputs.VirtualCamera,
        _ => (ObsOutput)model.Outputs.ReplayBuffer,
    };

    // Starting and stopping return when OBS Studio has BEGUN: a stream that connects takes seconds, and can fail. The status says how it goes
    // (type outputs, or watch the changes that are shown).
    switch (action.ToLowerInvariant())
    {
        case "start":
            await ConfirmGoingLiveAsync(output);
            await output.StartAsync();
            Console.WriteLine($"{output}. (A start takes a moment: type outputs to see when it runs.)");
            break;
        case "stop":
            await output.StopAsync();
            Console.WriteLine($"{output}. (A stop takes a moment too.)");
            break;
        case "toggle":
            if (!output.IsActive)
            {
                await ConfirmGoingLiveAsync(output);
            }

            var started = await output.ToggleAsync();
            Console.WriteLine($"{output}. ({(started ? "Started" : "Stopped")} by the toggle.)");
            break;
        case "pause" when output is ObsRecordOutput recording:
            await recording.PauseAsync();
            Console.WriteLine($"{recording}.");
            break;
        case "resume" when output is ObsRecordOutput recording:
            await recording.ResumeAsync();
            Console.WriteLine($"{recording}.");
            break;
        case "save" when output is ObsReplayBufferOutput buffer:
            await buffer.SaveAsync();
            Console.WriteLine("Asked OBS Studio to save the replay buffer: it writes the file a moment later.");
            break;
        default:
            throw new UserError($"What can the {output.Name} do? Type help.");
    }
}

// A stream goes live on the service that OBS Studio has set up: it is not started by a typo.
async Task ConfirmGoingLiveAsync(ObsOutput output)
{
    if (output.Kind != ObsOutputKind.Stream)
    {
        return;
    }

    Console.Write("This goes live on the streaming service that OBS Studio has set up. Type yes to start the stream: ");
    var answer = await Task.Run(Console.ReadLine);
    if (!string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
    {
        throw new UserError("You did not type yes, so the stream was not started.");
    }
}

void PrintHelp()
{
    Console.WriteLine("Commands (put a name that has spaces in double quotes):");
    Console.WriteLine("  scenes | inputs | status                  what OBS Studio has, from memory");
    Console.WriteLine("  scene \"Gaming\"                           put a scene on the air");
    Console.WriteLine("  mute \"Mic\" | unmute \"Mic\" | togglemute \"Mic\"");
    Console.WriteLine("  volume \"Mic\" -12                         set the volume, in dB");
    Console.WriteLine("  show \"Gaming\" \"Webcam\" | hide ... | toggle ...      a scene item: the scene, then the source it shows");
    Console.WriteLine("  newscene \"Break\" | newcolor \"Break\" \"Red box\"      make a scene, or a color input in a scene");
    Console.WriteLine("  renamescene \"Break\" \"Pause\" | renameinput \"Red box\" \"Box\"");
    Console.WriteLine("  removescene \"Pause\" | removeinput \"Box\"            (the scene items that show it go too)");
    Console.WriteLine("  outputs                                  what the stream, recording, virtual camera and replay buffer do");
    Console.WriteLine("  filters \"Mic\"                            the filters of a scene or an input, from the first");
    Console.WriteLine("  filter \"Mic\" \"Gain\" on | off | toggle     enable or disable a filter");
    Console.WriteLine("  newfilter \"Mic\" \"Boost\" gain_filter      make a filter (filterkinds lists the kinds) | removefilter \"Mic\" \"Boost\"");
    Console.WriteLine("  renamefilter \"Mic\" \"Boost\" \"Louder\" | movefilter \"Mic\" \"Louder\" 0");
    Console.WriteLine("  stream start | stop | toggle             (start asks you to type yes: it goes live)");
    Console.WriteLine("  record start | stop | toggle | pause | resume");
    Console.WriteLine("  virtualcam start | stop | toggle");
    Console.WriteLine("  replay start | stop | toggle | save      the replay buffer (it must be enabled in OBS Studio)");
    Console.WriteLine("  watch on | watch off                     show the changes that OBS Studio announces");
    Console.WriteLine("  help | quit");
}

static List<string> Split(string line)
{
    var words = new List<string>();
    var current = new StringBuilder();
    var quoted = false;
    var started = false;
    foreach (var character in line)
    {
        if (character == '"')
        {
            quoted = !quoted;
            started = true;
        }
        else if (char.IsWhiteSpace(character) && !quoted)
        {
            if (started)
            {
                words.Add(current.ToString());
                current.Clear();
                started = false;
            }
        }
        else
        {
            current.Append(character);
            started = true;
        }
    }

    if (started)
    {
        words.Add(current.ToString());
    }

    return words;
}

internal sealed class UserError(string message) : Exception(message);
