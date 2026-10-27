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
        /// <param name="cancellationToken">A token that ends the wait for the response, which makes the method throw an <see cref="OperationCanceledException"/>. A request that was already sent is not undone: OBS Studio may still carry it out.</param>
        public async Task TriggerHotkeyByKeySequence(ObsKey? keyId, KeyModifier? keyModifier, CancellationToken cancellationToken = default)
        {
            KeyModifiers keyModifiers = new(keyModifier);
            await this.SendRequestAsync(RequestType.TriggerHotkeyByKeySequence, new { keyId, keyModifiers }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
