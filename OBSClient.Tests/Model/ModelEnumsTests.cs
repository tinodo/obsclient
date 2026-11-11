namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;

    /// <summary>
    /// The conversions between what obs-websocket says and what the model shows for the monitor type, the deinterlacing and the audio tracks: every value goes there and back.
    /// </summary>
    public class ModelEnumsTests
    {
        [Fact]
        public void EveryMonitorType_GoesThereAndBack()
        {
            foreach (var type in Enum.GetValues<ObsMonitorType>())
            {
                Assert.Equal(type, ModelEnums.ToModel(ModelEnums.ToWire(type)));
            }

            Assert.Equal(Enum.GetValues<MonitorType>().Length, Enum.GetValues<ObsMonitorType>().Length);
            Assert.Equal(Enum.GetValues<MonitorType>().Order(), Enum.GetValues<ObsMonitorType>().Select(ModelEnums.ToWire).Order());
        }

        [Fact]
        public void EveryDeinterlaceMode_GoesThereAndBack()
        {
            foreach (var mode in Enum.GetValues<ObsDeinterlaceMode>())
            {
                Assert.Equal(mode, ModelEnums.ToModel(ModelEnums.ToWire(mode)));
            }

            Assert.Equal(Enum.GetValues<DeinterlaceMode>().Order(), Enum.GetValues<ObsDeinterlaceMode>().Select(ModelEnums.ToWire).Order());
        }

        [Fact]
        public void EveryFieldOrder_GoesThereAndBack_AndAnUnknownOneIsTheTopFieldFirst()
        {
            foreach (var order in Enum.GetValues<ObsDeinterlaceFieldOrder>())
            {
                Assert.Equal(order, ModelEnums.ToModelFieldOrder(ModelEnums.ToWire(order)));
            }

            Assert.Equal("OBS_DEINTERLACE_FIELD_ORDER_TOP", ModelEnums.ToWire(ObsDeinterlaceFieldOrder.TopFieldFirst));
            Assert.Equal("OBS_DEINTERLACE_FIELD_ORDER_BOTTOM", ModelEnums.ToWire(ObsDeinterlaceFieldOrder.BottomFieldFirst));
            Assert.Equal(ObsDeinterlaceFieldOrder.TopFieldFirst, ModelEnums.ToModelFieldOrder("something else"));
        }

        [Theory]
        [InlineData(0, false, false, false, false, false, false)]
        [InlineData(63, true, true, true, true, true, true)]
        [InlineData(1, true, false, false, false, false, false)]
        [InlineData(32, false, false, false, false, false, true)]
        [InlineData(0b010110, false, true, true, false, true, false)]
        public void TheTracks_AreOneBitForEach_AndGoThereAndBack(int mask, bool one, bool two, bool three, bool four, bool five, bool six)
        {
            var wire = ModelEnums.ToWire((ObsAudioTracks)mask);
            var tracks = new AudioTracks(wire["1"], wire["2"], wire["3"], wire["4"], wire["5"], wire["6"]);

            Assert.Equal(new[] { one, two, three, four, five, six }, new[] { wire["1"], wire["2"], wire["3"], wire["4"], wire["5"], wire["6"] });
            Assert.Equal(mask, ModelEnums.ToMask(tracks));
            Assert.Equal(6, wire.Count);
        }

        [Fact]
        public void TheTracksThatAreAll_AreTheSixBits()
        {
            Assert.Equal(63, (int)ObsAudioTracks.All);
            Assert.Equal(ObsAudioTracks.Track1 | ObsAudioTracks.Track2 | ObsAudioTracks.Track3 | ObsAudioTracks.Track4 | ObsAudioTracks.Track5 | ObsAudioTracks.Track6, ObsAudioTracks.All);
        }
    }
}
