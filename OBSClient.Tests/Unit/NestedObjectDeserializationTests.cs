namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// The objects obs-websocket nests in its responses are not described by the machine-readable protocol definition, so
    /// they cannot be checked by the conformance tests. These tests pin what they have to carry, which was taken from
    /// the obs-websocket source (Obs_ArrayHelper.cpp and Obs_ObjectHelper.cpp).
    /// </summary>
    public class NestedObjectDeserializationTests
    {
        [Fact]
        public void Input_ExposesTheUuidAndTheCapabilitiesOfTheInput()
        {
            var inputUuid = Guid.NewGuid();
            var json = $$"""
                {"inputName":"Mic","inputUuid":"{{inputUuid}}","inputKind":"wasapi_input_capture","unversionedInputKind":"wasapi_input_capture","inputKindCaps":9}
                """;

            var input = JsonSerializer.Deserialize<Input>(json)!;

            Assert.Equal("Mic", input.InputName);
            Assert.Equal(inputUuid, input.InputUuid);
            Assert.Equal(9u, input.InputKindCaps);
        }

        [Fact]
        public void Input_FromAnOlderObsStudio_HasNoUuidAndNoCapabilities()
        {
            var input = JsonSerializer.Deserialize<Input>("""{"inputName":"Mic","inputKind":"k","unversionedInputKind":"k"}""")!;

            Assert.Null(input.InputUuid);
            Assert.Null(input.InputKindCaps);
        }

        [Fact]
        public void SceneItem_ExposesTheUuidOfItsSource_AndTheCropToBoundsOfItsTransform()
        {
            var sourceUuid = Guid.NewGuid();
            // Three '$' so that the "}}" that closes the nested object is content, not the end of an interpolation hole.
            var json = $$$"""
                {"inputKind":"image_source","isGroup":null,"sceneItemBlendMode":"OBS_BLEND_NORMAL","sceneItemEnabled":true,"sceneItemId":1,"sceneItemIndex":0,
                 "sceneItemLocked":false,"sourceName":"Logo","sourceType":"OBS_SOURCE_TYPE_INPUT","sourceUuid":"{{{sourceUuid}}}",
                 "sceneItemTransform":{"alignment":5,"boundsAlignment":0,"boundsHeight":0.0,"boundsType":"OBS_BOUNDS_NONE","boundsWidth":0.0,
                   "cropBottom":0,"cropLeft":0,"cropRight":0,"cropTop":0,"cropToBounds":true,"height":100.0,"positionX":10.5,"positionY":0.0,
                   "rotation":0.0,"scaleX":1.0,"scaleY":1.0,"sourceHeight":100.0,"sourceWidth":100.0,"width":100.0}}
                """;

            var item = JsonSerializer.Deserialize<SceneItem>(json)!;

            Assert.Equal(sourceUuid, item.SourceUuid);
            Assert.Equal("Logo", item.SourceName);
            Assert.True(item.SceneItemTransform.CropToBounds);
            Assert.Equal(10.5f, item.SceneItemTransform.PositionX);
        }

        [Fact]
        public void SceneItemTransform_WithoutCropToBounds_LeavesItUnset_AndSerializesItAsNull()
        {
            var transform = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 100f, 0f, 0f, 0f, 1f, 1f, 100f, 100f, 100f);

            var json = JsonSerializer.Serialize(transform);

            Assert.Null(transform.CropToBounds);
            // A null is sent as JSON null, which obs-websocket reads as absent.
            using var document = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("cropToBounds").ValueKind);
        }
    }
}
