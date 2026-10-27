namespace OBSStudioClient.Classes
{
    /// <summary>
    /// Identifies a source to obs-websocket: by its name or by its UUID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every input and every scene is a source, so wherever a method asks for a <see cref="SourceRef"/> you can also pass an
    /// <see cref="InputRef"/>, a <see cref="SceneRef"/>, an <see cref="Input"/>, a <see cref="Scene"/> or the <see cref="SceneItem"/> that
    /// shows the source. A name (<c>"Webcam"</c>) and a <see cref="Guid"/> convert implicitly as well. A UUID stays the same when the
    /// source is renamed.
    /// </para>
    /// <para>
    /// <c>default(SourceRef)</c> identifies nothing; the methods refuse it with an <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    public readonly struct SourceRef : IEquatable<SourceRef>
    {
        private SourceRef(string? name, Guid? uuid)
        {
            this.Name = name;
            this.Uuid = uuid;
        }

        /// <summary>
        /// Gets the name of the source, or null when the source is identified by its UUID.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the UUID of the source, or null when the source is identified by its name.
        /// </summary>
        public Guid? Uuid { get; }

        /// <summary>
        /// Gets a value indicating whether this identifies a source (<c>default(SourceRef)</c> does not).
        /// </summary>
        public bool IsSpecified => this.Name is not null || this.Uuid.HasValue;

        /// <summary>
        /// Identifies a source by its name.
        /// </summary>
        /// <param name="name">The name of the source.</param>
        /// <returns>A reference to the source.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public static SourceRef FromName(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return new SourceRef(name, null);
        }

        /// <summary>
        /// Identifies a source by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the source.</param>
        /// <returns>A reference to the source.</returns>
        /// <exception cref="ArgumentException"><paramref name="uuid"/> is <see cref="Guid.Empty"/>.</exception>
        public static SourceRef FromUuid(Guid uuid)
        {
            if (uuid == Guid.Empty)
            {
                throw new ArgumentException("A UUID cannot be empty.", nameof(uuid));
            }

            return new SourceRef(null, uuid);
        }

        /// <summary>
        /// Identifies a source by its name.
        /// </summary>
        /// <param name="name">The name of the source.</param>
        public static implicit operator SourceRef(string name) => FromName(name);

        /// <summary>
        /// Identifies a source by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the source.</param>
        public static implicit operator SourceRef(Guid uuid) => FromUuid(uuid);

        /// <summary>
        /// Identifies the source that an input is.
        /// </summary>
        /// <param name="input">The input.</param>
        public static implicit operator SourceRef(InputRef input) => input.IsSpecified ? Of(input.Name, input.Uuid) : default;

        /// <summary>
        /// Identifies the source that a scene is.
        /// </summary>
        /// <param name="scene">The scene.</param>
        public static implicit operator SourceRef(SceneRef scene) => scene.IsSpecified ? Of(scene.Name, scene.Uuid) : default;

        /// <summary>
        /// Identifies the source that an input that OBS Studio reported is.
        /// </summary>
        /// <param name="input">The input.</param>
        public static implicit operator SourceRef(Input input) => (InputRef)input;

        /// <summary>
        /// Identifies the source that a scene that OBS Studio reported is.
        /// </summary>
        /// <param name="scene">The scene.</param>
        public static implicit operator SourceRef(Scene scene) => (SceneRef)scene;

        /// <summary>
        /// Identifies the source that a scene item shows, by its UUID (or by its name when OBS Studio did not report a UUID).
        /// </summary>
        /// <param name="sceneItem">The scene item.</param>
        public static implicit operator SourceRef(SceneItem sceneItem)
        {
            ArgumentNullException.ThrowIfNull(sceneItem);
            return sceneItem.SourceUuid is { } uuid && uuid != Guid.Empty ? FromUuid(uuid) : FromName(sceneItem.SourceName);
        }

        /// <summary>
        /// Determines whether two references are the same: the same name or the same UUID.
        /// </summary>
        public static bool operator ==(SourceRef left, SourceRef right) => left.Equals(right);

        /// <summary>
        /// Determines whether two references are different.
        /// </summary>
        public static bool operator !=(SourceRef left, SourceRef right) => !left.Equals(right);

        /// <inheritdoc/>
        /// <remarks>A reference by name never equals a reference by UUID, even when they identify the same source.</remarks>
        public bool Equals(SourceRef other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal) && this.Uuid == other.Uuid;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is SourceRef other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Name, this.Uuid);

        /// <summary>
        /// Gets the name or the UUID of the source, as text.
        /// </summary>
        /// <returns>The name or the UUID; an empty string for <c>default(SourceRef)</c>.</returns>
        public override string ToString() => this.Name ?? this.Uuid?.ToString() ?? string.Empty;

        internal void EnsureSpecified(string parameterName)
        {
            if (!this.IsSpecified)
            {
                throw new ArgumentException("The source has to be given by name or by UUID.", parameterName);
            }
        }

        private static SourceRef Of(string? name, Guid? uuid) => new(name, uuid);
    }
}
