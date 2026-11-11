namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    // What OBS Studio offers to make: the kinds of inputs and filters, and their defaults; and the inputs that it uses for the audio of the computer.
    public sealed partial class ObsModel
    {
        /// <summary>
        /// Asks OBS Studio for the kinds of inputs that it can make (and that its plugins add): <c>color_source_v3</c>, <c>wasapi_input_capture</c>, <c>browser_source</c>, and so on.
        /// </summary>
        /// <param name="unversioned">True for the kinds without their version (<c>color_source</c>), false for the kinds with it (<c>color_source_v3</c>), as <see cref="ObsInput.Kind"/> says them.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The kinds.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The kinds that a computer has depend on its operating system and on what is installed: a kind that is not in the list can not be made there (<see cref="ObsScene.CreateInputAsync"/>
        /// ends with a refusal). The request is made in its turn, after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<IReadOnlyList<string>> GetInputKindsAsync(bool unversioned = false, CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<IReadOnlyList<string>>(
                new WriteJob
                {
                    Description = "asking for the kinds of inputs of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetInputKindList, new { unversioned })],
                    ReadResponses = static responses => ((InputKindsResponse)responses[0].ResponseData!).InputKinds,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the settings that an input of a kind has until they are changed.
        /// </summary>
        /// <param name="kind">The kind, as <see cref="GetInputKindsAsync"/> says it (with its version).</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The default settings, as a JSON object.</returns>
        /// <exception cref="ArgumentException"><paramref name="kind"/> is empty.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it does not have that kind (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidInputKind"/>).</exception>
        /// <remarks>
        /// <see cref="ObsInput.Settings"/> has the settings that were set, and not the defaults: lay the settings over the defaults for the whole picture. The request is made in its turn,
        /// after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<JsonElement> GetDefaultInputSettingsAsync(string kind, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(kind);
            return this._writer.ExecuteAsync<JsonElement>(
                new WriteJob
                {
                    Description = $"asking for the default settings of the input kind '{kind}'",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetInputDefaultSettings, new { inputKind = kind })],
                    ReadResponses = static responses => SettingsValue.From(((DefaultInputSettingsResponse)responses[0].ResponseData!).DefaultInputSettings).Element,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio which inputs it uses for the audio devices of the computer that every scene has: the desktop audio and the microphones.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The inputs, which are in <see cref="Inputs"/> too. A device that is not chosen is null.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio says the names of the inputs, and the model gives the objects that have those names when the answer comes. An input that the model does not have yet (OBS Studio made it a
        /// moment ago) is null. The request is made in its turn, after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<ObsSpecialInputs> GetSpecialInputsAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<ObsSpecialInputs>(
                new WriteJob
                {
                    Description = "asking for the global audio inputs of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSpecialInputs, null)],
                    ReadResponses = responses =>
                    {
                        var special = (SpecialInputsResponse)responses[0].ResponseData!;
                        var inputs = this.Inputs;

                        // OBS Studio says the exact name of an input: IgnoreCase is for the names that an application passes, and would take an input of another case for it.
                        ObsInput? Named(string? name) => name is null ? null : inputs.FirstOrDefault(input => string.Equals(input.Name, name, StringComparison.Ordinal));
                        return new ObsSpecialInputs(Named(special.Desktop1), Named(special.Desktop2), Named(special.Mic1), Named(special.Mic2), Named(special.Mic3), Named(special.Mic4));
                    },
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the kinds of filters that it can make (and that its plugins add): <c>color_filter_v2</c>, <c>noise_gate_filter</c>, <c>chroma_key_filter_v2</c>, and so on.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The kinds, as <see cref="ObsFilter.Kind"/> says them.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: its obs-websocket is older than 5.4.0.</exception>
        /// <remarks>
        /// A kind of filter works on video, on audio, or on both: a kind that is not for the source that you give it is refused (<see cref="ObsSource.CreateFilterAsync"/>). The request is made in its turn,
        /// after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<IReadOnlyList<string>> GetFilterKindsAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<IReadOnlyList<string>>(
                new WriteJob
                {
                    Description = "asking for the kinds of filters of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSourceFilterKindList, null)],
                    ReadResponses = static responses => ((FilterKindListResponse)responses[0].ResponseData!).SourceFilterKinds,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the settings that a filter of a kind has until they are changed.
        /// </summary>
        /// <param name="kind">The kind, as <see cref="GetFilterKindsAsync"/> says it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The default settings, as a JSON object.</returns>
        /// <exception cref="ArgumentException"><paramref name="kind"/> is empty.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it does not have that kind (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidFilterKind"/>).</exception>
        /// <remarks>
        /// OBS Studio says the settings of a filter that were set, and leaves out the defaults: lay them over these for the whole picture (<see cref="ObsFilter.GetSettingsAsync"/> asks for the settings that a filter has).
        /// See <see cref="GetDefaultInputSettingsAsync"/> for the same for inputs.
        /// </remarks>
        public Task<JsonElement> GetDefaultFilterSettingsAsync(string kind, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(kind);
            return this._writer.ExecuteAsync<JsonElement>(
                new WriteJob
                {
                    Description = $"asking for the default settings of the filter kind '{kind}'",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSourceFilterDefaultSettings, new { filterKind = kind })],
                    ReadResponses = static responses => SettingsValue.From(((DefaultFilterSettingsResponse)responses[0].ResponseData!).DefaultFilterSettings).Element,
                },
                cancellationToken);
        }
    }
}
