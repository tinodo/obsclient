namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    // The requests that tools/protocol/generate.py does not write ([manual] in tools/protocol/overlay.toml): the client offers
    // them in another shape than the protocol does.
    public partial class ObsClient
    {
        /// <summary>
        /// Triggers a hotkey using a sequence of keys.
        /// </summary>
        /// <param name="keyId">The OBS key ID to use. See https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h</param>
        /// <param name="keyModifier">Key modifiers to apply</param>
        public async Task TriggerHotkeyByKeySequence(ObsKey? keyId, KeyModifier? keyModifier)
        {
            KeyModifiers keyModifiers = new(keyModifier);
            await this.SendRequestAsync(RequestType.TriggerHotkeyByKeySequence, new { keyId, keyModifiers }).ConfigureAwait(false);
        }
    }
}
