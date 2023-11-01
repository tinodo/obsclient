# ObsClient
A Complete cross platform .NET WebSocket Client for OBS Studio version 28 and up.  
Currently implementing: [**obs-websocket 5.3.0 Protocol**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md)

## Version 2
We're working on Version 2 of the client. You can download and use our first preview version.
Although the changes between v1 and v2 are not huge, they are breaking compatibility with previous versions, so we decided to bump the major version. (See: [SemVer](https://semver.org/))
The main goal for Version 2 is to reduce (cognitive) complexity and improve the overall design of the client.
For now, the main branch will contain the latest preview version of the code. For future releases (post V2-release), we will use seperate branches.

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 1.4.0`  
From the command line: `dotnet add package OBSClient --version 1.4.0`  

## Sample usages
```
ObsClient client = new();
bool isConnected = await client.ConnectAsync();
if (isConnected)
{
    await client.SetCurrentProgramScene("Gaming Scene");
    Monitor[] monitors = await client.GetMonitorList();
    client.Disconnect();
}
```

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
    var result = await _client.SendRequestBatchAsync(batchRequest, 6000);
    client.Disconnect();
}
```

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=tinodo_obsclient&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tinodo_obsclient)
![Build](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg?branch=main)
[![NuGet](https://img.shields.io/nuget/v/obsclient.svg?style=flat)](https://www.nuget.org/packages/obsclient)  