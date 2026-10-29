namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Classes;

    /// <summary>
    /// The levels that OBS Studio sends for an input (the event InputVolumeMeters), and what the library makes of them in decibels. OBS Studio
    /// sends three numbers for each channel of an input, and an empty list for some inputs.
    /// </summary>
    public class VolumeMeterTests
    {
        // As OBS Studio 33.0.0 sent them for a stereo input (see the capture in Fixtures), and for a second channel at half scale.
        private static InputVolumeMeterResponse Stereo() =>
            new([[0.006970884744077921f, 0.014309629797935486f, 0.014309629797935486f], [0.5f, 0.75f, 0.75f]], null, "Mic/Aux", null);

        [Fact]
        public void TheLevelOfAChannel_IsItsFirstNumber_InDecibels()
        {
            var meter = Stereo();

            Assert.Equal(-43.14f, meter.GetLevelDb(0), 0.01f);
            Assert.Equal(-6.02f, meter.GetLevelDb(1), 0.01f);
        }

        [Fact]
        public void AChannelThatIsSilent_GivesTheFloorAndNotMinusInfinity()
        {
            var meter = new InputVolumeMeterResponse([[0f, 0f, 0f]], null, "Mic/Aux", null);

            Assert.Equal(-100f, meter.GetLevelDb(0));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void AChannelThatIsNotThere_GivesMinusInfinity(int channel)
        {
            Assert.Equal(float.NegativeInfinity, Stereo().GetLevelDb(channel));
        }

        [Fact]
        public void AnInputWithoutLevels_GivesMinusInfinityForEveryChannel()
        {
            // OBS Studio sends an empty list for some inputs: the capture of OBS Studio 33.0.0 has one (Desktop Audio).
            var meter = new InputVolumeMeterResponse([], null, "Desktop Audio", null);

            Assert.Equal(float.NegativeInfinity, meter.GetLevelDb(0));
            Assert.Equal(float.NegativeInfinity, meter.GetLevelDb(-1));
        }

        [Fact]
        public void AChannelWithoutNumbers_GivesMinusInfinity()
        {
            var meter = new InputVolumeMeterResponse([[]], null, "Mic/Aux", null);

            Assert.Equal(float.NegativeInfinity, meter.GetLevelDb(0));
        }
    }

    /// <summary>The conversion between a multiplier and decibels, which the level of a channel is given with.</summary>
    public class AudioMathTests
    {
        [Theory]
        [InlineData(1f, 0f)]
        [InlineData(0.5f, -6.0206f)]
        [InlineData(0.1f, -20f)]
        [InlineData(2f, 6.0206f)]
        public void AMultiplier_IsTwentyTimesItsLogarithm(float multiplier, float expectedDb)
        {
            Assert.Equal(expectedDb, AudioMath.MulToDb(multiplier), 0.001f);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.5f)]
        [InlineData(1e-9f)]
        public void NoSound_OrLessThanTheFloor_GivesTheFloor(float multiplier)
        {
            Assert.Equal(-100f, AudioMath.MulToDb(multiplier));
        }

        [Fact]
        public void TheFloorIsAParameter()
        {
            Assert.Equal(-60f, AudioMath.MulToDb(0f, -60f));
            Assert.Equal(-60f, AudioMath.MulToDb(1e-6f, -60f));
            Assert.Equal(-20f, AudioMath.MulToDb(0.1f, -60f), 0.001f);
        }

        [Theory]
        [InlineData(0f, 1f)]
        [InlineData(-20f, 0.1f)]
        [InlineData(20f, 10f)]
        [InlineData(-6.0206f, 0.5f)]
        public void Decibels_AreConvertedBackToAMultiplier(float db, float expectedMultiplier)
        {
            Assert.Equal(expectedMultiplier, AudioMath.DbToMul(db), 0.0001f);
        }

        [Theory]
        [InlineData(0.01f)]
        [InlineData(0.3f)]
        [InlineData(1f)]
        [InlineData(1.7f)]
        public void ConvertingThere_AndBack_GivesTheMultiplierAgain(float multiplier)
        {
            Assert.Equal(multiplier, AudioMath.DbToMul(AudioMath.MulToDb(multiplier)), 0.0001f);
        }
    }
}
