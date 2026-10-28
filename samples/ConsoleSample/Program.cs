using System.Collections.Concurrent;
using System.Globalization;
using OBSStudioClient;

// A small tour of OBSClient: connect, read what OBS Studio has, and listen for changes. It only reads: nothing in OBS Studio is changed.
//
//   dotnet run --project samples/ConsoleSample -- [host] [port]
//
// The password is read from the environment variable OBS_PASSWORD (leave it unset when OBS Studio asks for none). The host is localhost
// and the port 4455, which is the port that OBS Studio uses unless you change it, unless you give others. When more than one OBS Studio
// is running (each has its own port), the port is what decides which one you get, so give the port of the one that you mean: the sample
// says which address it connects to and which OBS Studio it reached.
var host = args.Length > 0 ? args[0] : "localhost";
var port = 4455;
if (args.Length > 1 && !(int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535))
{
    // A port that is not one is not replaced by the default: that would connect to another OBS Studio than the one that was meant.
    Console.WriteLine($"'{args[1]}' is not a port. Give a number from 1 to 65535: dotnet run --project samples/ConsoleSample -- [host] [port]");
    return 2;
}

var password = Environment.GetEnvironmentVariable("OBS_PASSWORD") ?? string.Empty;
Console.WriteLine($"Connecting to OBS Studio at {host}:{port}{(args.Length > 1 ? string.Empty : " (the usual port of OBS Studio: give another one as the second argument)")}...");

using var client = new ObsClient();

// The events are raised one at a time, on a thread of the thread pool, in the order in which OBS Studio sent them.
client.CurrentProgramSceneChanged += (_, e) => Console.WriteLine($"On air now: {e.SceneName}");

// OBS Studio announces a new scene twice, and the client raises what OBS Studio sends, so remember the scenes that were announced.
var announced = new ConcurrentDictionary<Guid, bool>();
client.SceneCreated += (_, e) =>
{
    if (announced.TryAdd(e.SceneUuid, true))
    {
        Console.WriteLine($"A scene was created: {e.SceneName}");
    }
};

client.ConnectionClosed += (_, e) => Console.WriteLine($"The connection was closed: {e.WebSocketCloseDescription}");

// ConnectAsync returns false (it does not throw) when OBS Studio is not there, or refuses the password.
if (!await client.ConnectAsync(password: password, hostname: host, port: port))
{
    Console.WriteLine($"Could not connect to OBS Studio at {host}:{port}. Is its WebSocket server enabled (Tools, WebSocket Server Settings), and is OBS_PASSWORD right?");
    return 1;
}

var version = await client.GetVersion();
Console.WriteLine($"Connected to OBS Studio {version.ObsVersion} (obs-websocket {version.ObsWebSocketVersion}) at {host}:{port}");

// The scenes belong to a scene collection and the settings to a profile; saying which ones makes clear whose scenes these are.
var collection = await client.GetSceneCollectionList();
var profile = await client.GetProfileList();
Console.WriteLine($"Scene collection: {collection.CurrentSceneCollectionName}, profile: {profile.CurrentProfileName}");

var scenes = await client.GetSceneList();
Console.WriteLine($"On air: {scenes.CurrentProgramSceneName}");
foreach (var scene in scenes.Scenes)
{
    Console.WriteLine($"- {scene.SceneName}");
}

Console.WriteLine("Listening for events. Press Enter to stop.");
await Task.Run(Console.ReadLine);
return 0;
