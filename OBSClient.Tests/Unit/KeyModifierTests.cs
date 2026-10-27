namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    /// <summary>
    /// The modifiers of a hotkey combine (Ctrl+Shift+R is three keys): the client takes them as one KeyModifier, and sends one
    /// flag for each of the four keys.
    /// </summary>
    public class KeyModifierTests
    {
        [Fact]
        public void KeyModifier_IsAFlagsEnumeration_SoTheModifiersCombine()
        {
            Assert.True(typeof(KeyModifier).IsDefined(typeof(FlagsAttribute), false));
            Assert.Equal("Control, Shift", (KeyModifier.Control | KeyModifier.Shift).ToString());
        }

        [Theory]
        [InlineData(KeyModifier.None, false, false, false, false)]
        [InlineData(KeyModifier.Control, true, false, false, false)]
        [InlineData(KeyModifier.Control | KeyModifier.Shift, true, true, false, false)]
        [InlineData(KeyModifier.Shift | KeyModifier.Alt | KeyModifier.Command, false, true, true, true)]
        public void ACombinationOfModifiers_IsSentAsOneFlagForEachKey(KeyModifier modifiers, bool control, bool shift, bool alt, bool command)
        {
            var batch = new RequestBatchMessage();

            batch.AddTriggerHotkeyByKeySequenceRequest(ObsKey.OBS_KEY_R, modifiers);

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            var sent = data.GetProperty("keyModifiers");
            Assert.Equal(control, sent.GetProperty("control").GetBoolean());
            Assert.Equal(shift, sent.GetProperty("shift").GetBoolean());
            Assert.Equal(alt, sent.GetProperty("alt").GetBoolean());
            Assert.Equal(command, sent.GetProperty("command").GetBoolean());
        }
    }
}
