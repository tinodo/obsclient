namespace OBSStudioClient.Tests.Unit
{
    using System.Text.Json;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The value of an item of a list property is a string, an integer, a floating point number or null, depending on the format
    /// of the list (checked in the source of obs-websocket 5.7.6, <c>ArrayHelper::GetListPropertyItems</c>). The client read it
    /// as a string, so that every list of numbers made <c>GetInputPropertiesListPropertyItems</c> fail.
    /// </summary>
    public class ListPropertyItemTests
    {
        [Fact]
        public async Task TheItemsOfAList_KeepTheKindOfTheirValues()
        {
            var (sent, items) = await ObsClientHarness.QueryAsync(
                client => client.GetInputPropertiesListPropertyItems("Capture", "mode"),
                new
                {
                    propertyItems = new object[]
                    {
                        new { itemName = "Text", itemEnabled = true, itemValue = "left" },
                        new { itemName = "Integer", itemEnabled = true, itemValue = 2 },
                        new { itemName = "Float", itemEnabled = false, itemValue = 0.5 },
                        new { itemName = "Nothing", itemEnabled = true, itemValue = (string?)null },
                    },
                });

            Assert.Equal("GetInputPropertiesListPropertyItems", sent.RequestType);
            Assert.Equal(4, items.Length);
            Assert.Equal("left", items[0].ItemValue!.Value.GetString());
            Assert.Equal(2, items[1].ItemValue!.Value.GetInt32());
            Assert.False(items[2].ItemEnabled);
            Assert.Equal(0.5, items[2].ItemValue!.Value.GetDouble());
            Assert.Null(items[3].ItemValue);
        }

        [Fact]
        public void AnItem_IsWrittenAsItWasRead()
        {
            const string json = """{"itemEnabled":true,"itemName":"Integer","itemValue":2}""";
            var item = JsonSerializer.Deserialize<OBSStudioClient.Classes.PropertyItem>(json)!;

            Assert.Equal(json, JsonSerializer.Serialize(item));
        }
    }
}
