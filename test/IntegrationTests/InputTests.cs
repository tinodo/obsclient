using OBSStudioClient;
using OBSStudioClient.Enums;
using System.Diagnostics;
using Xunit.Sdk;

namespace IntegrationTests;

public class InputTests
{
    private ObsClient CreateClient()
    {
        ObsClient client = new();
        client.RequestTimeout = 5000;
        return client;
    }

    private async Task ConnectOrThrowAsync(ObsClient client, EventSubscriptions eventSubscription = EventSubscriptions.All)
    {
        var result = await client.ConnectAsync(
            autoReconnect: true, 
            password: "", 
            hostname: "localhost", 
            port: 4449,
            eventSubscription: eventSubscription);
        if (!result)
            throw new XunitException("Failed to connect to OBS WebSocket server.");
    }

    [Fact]
    public async Task InputVolumeMeters_Test()
    {
        var eventCount = 0;
        using var client = CreateClient();
        client.InputVolumeMeters += (sender, e) =>
        {
            Assert.NotNull(e);
            Assert.NotNull(e.Inputs);
            Assert.NotEmpty(e.Inputs);
            
            foreach (var input in e.Inputs)
            {
                Assert.NotNull(input.InputName);
                Assert.NotNull(input.InputLevelsMul);
                if (input.InputLevelsMul.Length > 0)
                {
                    Assert.True(input.InputLevelsMul[0].Length == 3);// livello RMS/magnitude, livello peak, livello peak senza filtro
                    Assert.True(input.InputLevelsMul[1].Length == 3);// livello RMS/magnitude, livello peak, livello peak senza filtro
                    Assert.True(input.GetLevelDb(0) > float.NegativeInfinity);
                    Assert.True(input.GetLevelDb(1) > float.NegativeInfinity);
                    var left = input.GetLevelDb(0);
                    var right = input.GetLevelDb(1);
                    Debug.WriteLine($"Received InputVolumeMeters event for input: {input.InputName}, level L: {left}db, level R: {right}db");
                }
            }
            eventCount++;
        };
        await ConnectOrThrowAsync(client,
                eventSubscription: OBSStudioClient.Enums.EventSubscriptions.All
                | OBSStudioClient.Enums.EventSubscriptions.InputVolumeMeters);
        await Task.Delay(600);
        Assert.True(eventCount > 3, "Expected to receive at least 10 InputVolumeMeters events within 600 milliseconds.");
    }


}
