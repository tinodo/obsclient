namespace OBSStudioClient.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    /// <summary>
    /// A canvas of OBS Studio: a surface that scenes are drawn on, with a video of its own. OBS Studio has a main canvas, and a plugin (or a scene collection that a plugin saved) can make more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <see cref="ObsCanvas"/> is a snapshot, like <see cref="ObsTransition"/>: it does not change, and <see cref="ObsCanvases.All"/> holds another object for a canvas that was renamed. What the
    /// model keeps of a canvas is what OBS Studio announces: its name, its UUID (which never changes) and its flags (which are set when it is made). Its video settings are not announced, so they are asked for
    /// when you need them (<see cref="GetVideoSettingsAsync"/>).
    /// </para>
    /// <para>
    /// The model holds the scenes of the main canvas (<see cref="ObsModel.Scenes"/>). The scenes of the other canvases are not in the model yet: ask for them with the plain client
    /// (<c>obs.Client.GetSceneList(canvas.Uuid)</c>).
    /// </para>
    /// </remarks>
    public sealed class ObsCanvas
    {
        private readonly ObsModel _model;

        internal ObsCanvas(ObsModel model, string name, Guid uuid, ObsCanvasFlags flags)
        {
            this._model = model;
            this.Name = name;
            this.Uuid = uuid;
            this.Flags = flags;
        }

        /// <summary>
        /// Gets the name of the canvas. The main canvas is called <c>Main</c> (it can not be renamed).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the UUID of the canvas, which stays the same for as long as the canvas lives. The UUID of the main canvas is the same in every OBS Studio.
        /// </summary>
        public Guid Uuid { get; }

        /// <summary>
        /// Gets what the canvas is, and does.
        /// </summary>
        public ObsCanvasFlags Flags { get; }

        /// <summary>
        /// Gets whether this is the main canvas.
        /// </summary>
        public bool IsMain => this.Flags.HasFlag(ObsCanvasFlags.Main);

        /// <summary>
        /// Asks OBS Studio for the video settings of the canvas: its base resolution, its output resolution and its frame rate.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The settings at the moment that OBS Studio answered; or null when the canvas has no video (a canvas that was made without one says nothing of it).</returns>
        /// <exception cref="ObsRemovedException">The canvas is not there any more: OBS Studio does not list it, or said that it removed it, or OBS Studio changed its scene collection.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the video of a canvas, so the model does not keep it: ask when you need it. The request is made in its turn, after the changes that were asked for before it
        /// (see <see cref="ObsSceneItem.SetEnabledAsync"/>). For the main canvas this is what <see cref="ObsModel.GetVideoSettingsAsync"/> says.
        /// </remarks>
        public async Task<ObsVideoSettings?> GetVideoSettingsAsync(CancellationToken cancellationToken = default)
        {
            var uuid = this.Uuid;
            var answer = await this._model.Writer.ExecuteAsync<Lookup>(
                new WriteJob
                {
                    Description = $"asking for the video settings of the canvas '{this.Name}'",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetCanvasList, null)],
                    ReadResponses = responses => Find(((CanvasListResponse)responses[0].ResponseData!).Canvases, uuid),
                },
                cancellationToken).ConfigureAwait(false);

            // OBS Studio goes on listing a canvas that it removed for as long as something holds on to it, and says that it removed it only once (CanvasRemoved): the model does not list it then.
            return answer.Found && this._model.Canvases.TryGet(uuid, out _) ? answer.Video : throw new ObsRemovedException($"canvas '{this.Name}'", ObsObjectState.Gone);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.IsMain ? $"Canvas '{this.Name}' (main)" : $"Canvas '{this.Name}'";
        }

        private static Lookup Find(Canvas[] canvases, Guid uuid)
        {
            foreach (var canvas in canvases)
            {
                if (canvas.CanvasUuid == uuid)
                {
                    return new Lookup(true, ToVideoSettings(canvas.CanvasVideoSettings));
                }
            }

            return new Lookup(false, null);
        }

        // obs-websocket says every field or none (a canvas that has no video mix says null for all of them).
        private static ObsVideoSettings? ToVideoSettings(CanvasVideoSettings? video)
        {
            if (video is not { FpsNumerator: { } numerator, FpsDenominator: { } denominator, BaseWidth: { } baseWidth, BaseHeight: { } baseHeight, OutputWidth: { } outputWidth, OutputHeight: { } outputHeight })
            {
                return null;
            }

            return new ObsVideoSettings((int)Math.Round(numerator), (int)Math.Round(denominator), baseWidth, baseHeight, outputWidth, outputHeight);
        }

        private readonly record struct Lookup(bool Found, ObsVideoSettings? Video);
    }
}
