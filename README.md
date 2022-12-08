# ObsClient
A Complete cross platform .NET WebSocket Client for OBS Studio version 28 and up.  
Currently implementing: [**obs-websocket 5.1.0 Protocol**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md)

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 1.0.1`  

## Sample usage
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


[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=tinodo_obsclient&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=tinodo_obsclient)
![Build](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg?branch=main)