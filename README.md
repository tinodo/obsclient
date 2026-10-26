# ObsClient
A Complete cross platform .NET WebSocket Client for OBS Studio version 28 and up.  
Currently implementing: [**obs-websocket 5.7.6 Protocol**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md) (including multi-canvas support added in 5.7.0)  
Targets `net8.0` and `net10.0`.  
Our intent is to create a <ins>complete</ins> and <ins>easy to use</ins> client for OBS Studio.

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 3.1.0`  
From the command line: `dotnet add package OBSClient --version 3.1.0`  

## Sample usage

### Simple

```
ObsClient client = new();
bool isConnected = await client.ConnectAsync();
if (isConnected)
{
    await client.SetCurrentProgramScene("Gaming Scene");
    Monitor[] monitors = await client.GetMonitorList();
    client.Disconnect();
}
client.Dispose();
```

### Request Batch

```
ObsClient client = new();
bool isConnected = await client.ConnectAsync(true, "P@ssw0rd", "localhost", 4455, EventSubscriptions.Filters | EventSubscriptions.Scenes);
if (isConnected)
{
    RequestBatchMessage batchRequest = new();
    batchRequest.AddToggleVirtualCamRequest();
    batchRequest.AddSetStudioModeEnabledRequest(true);
    batchRequest.AddSleepRequest(5000, null);
    batchRequest.AddToggleVirtualCamRequest();
    batchRequest.AddSetStudioModeEnabledRequest(false);
    var result = await client.SendRequestBatchAsync(batchRequest, 6000);
    client.Disconnect();
}
client.Dispose();
```

### Canvases (obs-websocket 5.7.0+)

```
ObsClient client = new();
bool isConnected = await client.ConnectAsync();
if (isConnected)
{
    Canvas[] canvases = await client.GetCanvasList();
    foreach (var canvas in canvases)
    {
        SceneListResponse scenes = await client.GetSceneList(canvas.CanvasUuid);
    }
    client.Disconnect();
}
client.Dispose();
```

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=tinodo_obsclient&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tinodo_obsclient)
![Build and Test](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg?branch=main)
![CodeQL](https://github.com/tinodo/obsclient/actions/workflows/codeql.yml/badge.svg?branch=main)
[![NuGet](https://img.shields.io/nuget/v/obsclient.svg?style=flat)](https://www.nuget.org/packages/obsclient)  

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test, and contribute, and [CHANGELOG.md](CHANGELOG.md) for release history.
