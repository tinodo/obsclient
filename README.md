# ObsClient
A Complete cross platform .NET WebSocket Client for OBS Studio version 28 and up.  
Currently implementing: [**obs-websocket 5.3.0 Protocol**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md)
Our intent is to create a <ins>complete</ins> and <ins>easy to use</ins> client for OBS Studio.

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 2.0.1`  
From the command line: `dotnet add package OBSClient --version 2.0.1`  

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
    var result = await _client.SendRequestBatchAsync(batchRequest, 6000);
    client.Disconnect();
}
client.Dispose();
```

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=tinodo_obsclient&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tinodo_obsclient)
![Build](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg?branch=main)
[![NuGet](https://img.shields.io/nuget/v/obsclient.svg?style=flat)](https://www.nuget.org/packages/obsclient)  