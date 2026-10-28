using System.Collections.Concurrent;
using OBSStudioClient;

// A small tour of OBSClient: connect, read what OBS Studio has, and listen for changes. It only reads: nothing in OBS Studio is changed.
//
//   dotnet run --project samples/ConsoleSample -- [host] [port]
//
// The password is read from the environment variable OBS_PASSWORD (leave it unset when OBS Studio asks for none). The host is localhost
// and the port 4455 unless you give others. When more than one OBS Studio is running (each has its own port), the port is what decides
// which one you get, so give the port of the one that you mean: the sample says which one it is connected to.
var host = args.Length > 0 ? args[0] : "localhost";
var port = args.Length > 1 && int.TryParse(args[1], out var parsed) ? parsed : 4455;
var password = Environment.GetEnvironmentVariable("OBS_PASSWORD") ?? string.Empty;

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
