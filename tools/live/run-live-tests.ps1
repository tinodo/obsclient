<#
.SYNOPSIS
    Runs the live tests of the client against a real OBS Studio that this script starts: a private copy, always with --portable, with a
    configuration of its own and a WebSocket port of its own.

.DESCRIPTION
    The live tests (OBSClient.Tests/Live) talk to a real OBS Studio. Several versions of OBS Studio can be installed on a machine, and the
    arguments of a start decide which configuration (scenes, profiles) an instance uses, so this script is careful:

      * It copies the programs of an OBS Studio (the installed one, or a portable one) to a folder of its own, without the configuration.
        The installs themselves are only read: they are never started, and never written to.
      * It always starts that copy with --portable (it refuses to start without), so that its configuration is the one next to the copy:
        a new one for every run, which nobody else uses. It never starts the virtual camera.
      * It gives the copy a WebSocket port of its own: a free one (never 4455, which OBS Studio uses by default), and it checks that the
        only process that listens on it is the copy that it started. Other copies of OBS Studio may be running; they are never touched.
      * It names the scene collection of the copy after the run and tells the tests, which only change an OBS Studio that has that
        scene collection as its current one.
      * It stops only the process that it started.

    The first time, Windows may ask whether the copy of OBS Studio may use the network: the tests only use the local machine.

.PARAMETER ObsPath
    The folder of an OBS Studio (the one with bin\64bit\obs64.exe in it), or obs64.exe itself. It is only read. Default: the environment
    variable OBS_LIVE_OBS_PATH, then the installed OBS Studio.

.PARAMETER CacheDirectory
    Where the copy of OBS Studio and its configuration are kept: %LOCALAPPDATA%\obsclient-live. It can be deleted at any time. One run at a
    time uses a folder (it is locked): a second run ends at once instead of stopping the OBS Studio of the first; give it another folder
    to run both.

.PARAMETER Configuration
    The build configuration of the tests (Release).

.PARAMETER Framework
    The target framework of the tests (net10.0).

.PARAMETER Filter
    The filter of the tests that are run. Default: all the live tests.

.PARAMETER StartupSeconds
    How long to wait for OBS Studio to start (120).

.PARAMETER WebSocketDebug
    Has the copy of OBS Studio log every message of its WebSocket server (--websocket_debug), for finding out what OBS Studio did. The log is
    kept in %LOCALAPPDATA%\obsclient-live\last-run, with what the tests sent in it: do not share it.

.PARAMETER KeepObsRunning
    Leaves the copy of OBS Studio running after the tests, to look at what the tests did. Stop it with Stop-Process -Id <the id that is shown>.

.EXAMPLE
    pwsh tools/live/run-live-tests.ps1

.EXAMPLE
    pwsh tools/live/run-live-tests.ps1 -ObsPath C:\Tools\OBS-Studio-33.0.0-Windows-x64 -Filter "FullyQualifiedName~LiveSceneTests"
#>
#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ObsPath = $env:OBS_LIVE_OBS_PATH,
    [string]$CacheDirectory = (Join-Path $env:LOCALAPPDATA 'obsclient-live'),
    [string]$Configuration = 'Release',
    [string]$Framework = 'net10.0',
    [string]$Filter = 'FullyQualifiedName~.Live.',
    [int]$StartupSeconds = 120,
    [switch]$WebSocketDebug,
    [switch]$KeepObsRunning
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    throw 'This script makes a private copy of OBS Studio for Windows. On Linux the live tests run in CI (.github/workflows/live-obs.yml).'
}
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# --portable is not a parameter: it starts every start of OBS Studio here, and Start-ObsCopy refuses a start without it. --multi only
# keeps OBS Studio from asking whether a second instance is wanted while one of somebody else is running. There is no --startvirtualcam.
$StartArguments = @('--portable', '--multi', '--minimize-to-tray', '--disable-shutdown-check', '--disable-missing-files-check')
if ($WebSocketDebug) { $StartArguments += '--websocket_debug' }
$DefaultWebSocketPort = 4455
$MarkerName = '.obsclient-live'

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Test-IsInside([string]$Path, [string]$Folder) {
    $child = (Get-FullPath $Path) + '\'
    $parent = (Get-FullPath $Folder) + '\'
    return $child.StartsWith($parent, [System.StringComparison]::OrdinalIgnoreCase) -and $child.Length -gt $parent.Length
}

function Find-ObsRoot([string]$Path) {
    if (-not $Path) { return $null }
    if (Test-Path -LiteralPath $Path -PathType Leaf) { $Path = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $Path)) }
    if (Test-Path -LiteralPath (Join-Path $Path 'bin\64bit\obs64.exe')) { return (Resolve-Path -LiteralPath $Path).ProviderPath }
    return $null
}

function Write-Utf8([string]$Path, [string]$Text) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

# The ids of the processes that listen on a port, for IPv4 and IPv6. It reads the table of the system and connects to nothing.
function Get-ListenerIds([int]$Port) {
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return @($connections | ForEach-Object { [int]$_.OwningProcess } | Sort-Object -Unique)
}

# A port that nothing listens on, for IPv4 and IPv6 alike (obs-websocket listens on both), and not the default one of OBS Studio:
# an OBS Studio that runs for somebody else must never be what these tests talk to.
function Get-FreePort {
    for ($try = 0; $try -lt 100; $try++) {
        $port = 0
        try {
            $socket = New-Object System.Net.Sockets.Socket([System.Net.Sockets.AddressFamily]::InterNetworkV6, [System.Net.Sockets.SocketType]::Stream, [System.Net.Sockets.ProtocolType]::Tcp)
            try {
                $socket.DualMode = $true
                $socket.Bind((New-Object System.Net.IPEndPoint([System.Net.IPAddress]::IPv6Any, 0)))
                $port = $socket.LocalEndPoint.Port
            } finally { $socket.Close() }
        } catch {
            $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            try { $port = $listener.LocalEndpoint.Port } finally { $listener.Stop() }
        }
        if ($port -ne $DefaultWebSocketPort -and (Get-ListenerIds $port).Count -eq 0) { return $port }
    }
    throw 'No free port was found.'
}

function Read-SharedText([string]$Path) {
    $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object System.IO.StreamReader($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-ObsLog {
    return Get-ChildItem -Path (Join-Path $configRoot 'obs-studio\logs') -Filter '*.txt' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
}

function Save-ObsLog {
    $log = Get-ObsLog
    if ($log) {
        New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
        Copy-Item -LiteralPath $log.FullName -Destination (Join-Path $logDirectory 'obs-log.txt') -Force
    }
    return $log
}

# The configuration of the copy: new for every run, so that the scenes and the settings are the same every time. The WebSocket password
# has characters that are not ASCII, which checks that the client hashes it as UTF-8.
function Write-ObsConfiguration([int]$Port, [string]$Password, [string]$Collection) {
    if (Test-Path -LiteralPath $configRoot) {
        if (-not (Test-IsInside $configRoot $CacheDirectory)) { throw "Refusing to remove ${configRoot}: it is not inside ${CacheDirectory}." }
        Remove-Item -LiteralPath $configRoot -Recurse -Force
    }

    Write-Utf8 (Join-Path $configRoot 'obs-studio\global.ini') "[General]`nEnableAutoUpdates=false`n"
    Write-Utf8 (Join-Path $configRoot 'obs-studio\user.ini') "[General]`nFirstRun=true`nConfirmOnExit=false`n`n[BasicWindow]`nSysTrayEnabled=true`nSysTrayWhenStarted=true`n`n[Basic]`nSceneCollection=$Collection`n"
    $websocket = [ordered]@{
        alerts_enabled  = $false
        auth_required   = $true
        first_load      = $false
        server_enabled  = $true
        server_password = $Password
        server_port     = $Port
    }
    Write-Utf8 (Join-Path $configRoot 'obs-studio\plugin_config\obs-websocket\config.json') ($websocket | ConvertTo-Json)
}

# The only place where OBS Studio is started. It is the private copy, with --portable, and never with the virtual camera.
function Start-ObsCopy {
    if ($StartArguments -notcontains '--portable') { throw 'Refusing to start OBS Studio without --portable.' }
    if ($StartArguments -contains '--startvirtualcam') { throw 'Refusing to start the virtual camera of a copy of OBS Studio.' }
    if (-not (Test-IsInside $exe $obsRoot) -or -not (Test-IsInside $obsRoot $CacheDirectory)) { throw "Refusing to start ${exe}: it is not the private copy." }
    if ((Get-FullPath $exe) -ieq (Get-FullPath (Join-Path $source 'bin\64bit\obs64.exe'))) { throw 'Refusing to start the OBS Studio that was copied.' }
    return Start-Process -FilePath $exe -ArgumentList $StartArguments -WorkingDirectory $binDirectory -PassThru
}

# Only the process that this script started, by its id. OBS Studio sits in the tray, without a window to close, so it is usually ended.
function Stop-ObsCopy($Process) {
    if ($null -eq $Process -or $Process.HasExited) { return }
    $closed = $false
    try { $closed = $Process.CloseMainWindow() } catch { }
    if (-not ($closed -and $Process.WaitForExit(10000))) { Stop-Process -Id $Process.Id -Force }
    $null = $Process.WaitForExit(15000)
}

# Waits until the copy runs in portable mode and is the only process that listens on its port. It returns what is wrong, or nothing.
function Wait-ObsCopy($Process, [int]$Port, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $portable = $false
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) { return "OBS Studio ended while it started (exit code $($Process.ExitCode))." }

        if (-not $portable) {
            $log = Get-ObsLog
            if ($log) {
                try { $text = Read-SharedText $log.FullName } catch { $text = '' }
                if ($text -match 'Portable mode: true') { $portable = $true }
                elseif ($text -match 'Portable mode: false') { return 'OBS Studio did not start in portable mode.' }
            }
        }

        $listeners = Get-ListenerIds $Port
        if ($listeners.Count -gt 0) {
            $others = @($listeners | Where-Object { $_ -ne $Process.Id })
            if ($others.Count -gt 0) { return "Another process listens on port $Port as well: $($others -join ', ')." }
            if ($portable) { return $null }
        }

        Start-Sleep -Milliseconds 500
    }

    return "OBS Studio did not come up on port $Port, in portable mode, within $Seconds seconds."
}

$source = $null
foreach ($candidate in @($ObsPath, (Join-Path $env:ProgramW6432 'obs-studio'), (Join-Path $env:ProgramFiles 'obs-studio'))) {
    $source = Find-ObsRoot $candidate
    if ($source) { break }
}
if (-not $source) {
    throw 'No OBS Studio found: give the folder of one with -ObsPath (the folder that has bin\64bit\obs64.exe in it), or set OBS_LIVE_OBS_PATH.'
}

$CacheDirectory = Get-FullPath $CacheDirectory
$obsRoot = Join-Path $CacheDirectory 'obs'
$binDirectory = Join-Path $obsRoot 'bin\64bit'
$exe = Join-Path $binDirectory 'obs64.exe'
$configRoot = Join-Path $obsRoot 'config'
$logDirectory = Join-Path $CacheDirectory 'last-run'

# The folder is this script's own: it is only used (and a copy is only mirrored into it) when this script made it.
if ((Get-FullPath $source) -ieq $CacheDirectory -or (Test-IsInside $source $CacheDirectory) -or (Test-IsInside $CacheDirectory $source)) {
    throw "The cache directory $CacheDirectory and the OBS Studio that is copied ($source) overlap."
}
$marker = Join-Path $CacheDirectory $MarkerName
if (Test-Path -LiteralPath $CacheDirectory) {
    if (-not (Test-Path -LiteralPath $marker)) { throw "$CacheDirectory exists, but this script did not make it (no $MarkerName in it). Give another -CacheDirectory." }
} else {
    New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null
    Write-Utf8 $marker "Made by tools/live/run-live-tests.ps1 for a copy of OBS Studio that the live tests use. It can be deleted.`n"
}

# One run at a time for a cache directory: a second run would stop the OBS Studio of the first one, copy over its files and delete its
# configuration while it is still testing. The lock is a file that stays open until this script ends: it is released in the final
# finally, and by the system when the process ends however it ends, so a crash leaves nothing behind that has to be removed.
$lock = $null
try {
    $lock = [System.IO.File]::Open((Join-Path $CacheDirectory '.lock'), [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
} catch [System.IO.IOException] {
    throw "Another run of this script is using $CacheDirectory. Wait for it to end, or give this run another -CacheDirectory."
}

# What is found running from this folder (the lock is held) is a copy that an earlier run left (a crash), and nobody's work. It is
# recognised by its path, which is nobody else's.
$obs = $null
$port = 0
$problem = 'OBS Studio was not started.'
try {
    Get-CimInstance Win32_Process -Filter "Name = 'obs64.exe'" | Where-Object { $_.ExecutablePath -and ((Get-FullPath $_.ExecutablePath) -ieq (Get-FullPath $exe)) } | ForEach-Object {
        Write-Host "Stopping the copy of OBS Studio that an earlier run left (process $($_.ProcessId))."
        Stop-Process -Id $_.ProcessId -Force
    }

    Write-Host "OBS Studio programs (only read): $source"
    Write-Host "Private copy:                    $obsRoot (without the configuration of the original)"
    $null = & robocopy $source $obsRoot /MIR /XD (Join-Path $source 'config') (Join-Path $obsRoot 'config') /NFL /NDL /NJH /NJS /NP /R:2 /W:2
    if ($LASTEXITCODE -ge 8) { throw "Copying OBS Studio failed (robocopy exit code $LASTEXITCODE)." }

    $password = 'p' + [char]0x00E4 + 's' + [char]0x00F6 + '-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
    $collection = 'obsclient-live-' + [guid]::NewGuid().ToString('N').Substring(0, 12)

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $port = Get-FreePort
        Write-ObsConfiguration $port $password $collection
        $obs = Start-ObsCopy
        Write-Host "Started the copy of OBS Studio: process $($obs.Id), $($StartArguments -join ' '), WebSocket port $port (not $DefaultWebSocketPort), scene collection $collection."

        $problem = Wait-ObsCopy $obs $port $StartupSeconds
        if (-not $problem) { break }

        Write-Host "Attempt ${attempt}: $problem"
        $null = Save-ObsLog
        Stop-ObsCopy $obs
        $obs = $null
    }

    if ($problem) {
        $log = Save-ObsLog
        if ($log) { Get-Content -LiteralPath (Join-Path $logDirectory 'obs-log.txt') -Tail 25 | ForEach-Object { Write-Host "  obs: $_" } }
        throw $problem
    }

    Write-Host "The copy of OBS Studio is up, and the only one that listens on port $port. Running the live tests."
    $saved = @{}
    $names = 'OBS_LIVE_HOST', 'OBS_LIVE_PORT', 'OBS_LIVE_PASSWORD', 'OBS_LIVE_ALLOW_CHANGES', 'OBS_LIVE_COLLECTION'
    foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        $env:OBS_LIVE_HOST = '127.0.0.1'
        $env:OBS_LIVE_PORT = "$port"
        $env:OBS_LIVE_PASSWORD = $password
        $env:OBS_LIVE_ALLOW_CHANGES = '1'
        $env:OBS_LIVE_COLLECTION = $collection
        & dotnet test (Join-Path $repository 'OBSClient.Tests') -c $Configuration -f $Framework --filter $Filter --logger 'console;verbosity=detailed'
        $exitCode = $LASTEXITCODE
    } finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    }
} finally {
    if ($null -ne $obs) {
        if ($KeepObsRunning) {
            Write-Host "The copy of OBS Studio is still running (process $($obs.Id), port $port); stop it with: Stop-Process -Id $($obs.Id)"
        } else {
            Stop-ObsCopy $obs
        }
    }
    $null = Save-ObsLog
    if (Test-Path -LiteralPath (Join-Path $logDirectory 'obs-log.txt')) { Write-Host "The log of the copy of OBS Studio is kept in $(Join-Path $logDirectory 'obs-log.txt')." }
    if ($null -ne $lock) { $lock.Dispose() }
}

exit $exitCode
