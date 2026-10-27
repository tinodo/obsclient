namespace OBSStudioClient.Classes
{
    /// <summary>
    /// Identifies a scene to obs-websocket: by its name or by its UUID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wherever a method asks for a <see cref="SceneRef"/> you can pass a name (<c>"Game"</c>), a <see cref="Guid"/> or a
    /// <see cref="Scene"/> that OBS Studio gave you: they convert implicitly. A UUID stays the same when the scene is renamed.
    /// </para>
    /// <para>
    /// <c>default(SceneRef)</c> identifies nothing; the methods refuse it with an <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    public readonly struct SceneRef : IEquatable<SceneRef>
    {
        private SceneRef(string? name, Guid? uuid)
        {
            this.Name = name;
            this.Uuid = uuid;
        }

        /// <summary>
        /// Gets the name of the scene, or null when the scene is identified by its UUID.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the UUID of the scene, or null when the scene is identified by its name.
        /// </summary>
        public Guid? Uuid { get; }

        /// <summary>
        /// Gets a value indicating whether this identifies a scene (<c>default(SceneRef)</c> does not).
        /// </summary>
        public bool IsSpecified => this.Name is not null || this.Uuid.HasValue;

        /// <summary>
        /// Identifies a scene by its name.
        /// </summary>
        /// <param name="name">The name of the scene.</param>
        /// <returns>A reference to the scene.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public static SceneRef FromName(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return new SceneRef(name, null);
        }

        /// <summary>
        /// Identifies a scene by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the scene.</param>
        /// <returns>A reference to the scene.</returns>
        /// <exception cref="ArgumentException"><paramref name="uuid"/> is <see cref="Guid.Empty"/>.</exception>
        public static SceneRef FromUuid(Guid uuid)
        {
            if (uuid == Guid.Empty)
            {
                throw new ArgumentException("A UUID cannot be empty.", nameof(uuid));
            }

            return new SceneRef(null, uuid);
        }

        /// <summary>
        /// Identifies a scene by its name.
        /// </summary>
        /// <param name="name">The name of the scene.</param>
        public static implicit operator SceneRef(string name) => FromName(name);

        /// <summary>
        /// Identifies a scene by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the scene.</param>
        public static implicit operator SceneRef(Guid uuid) => FromUuid(uuid);

        /// <summary>
        /// Identifies a scene that OBS Studio reported, by its UUID.
        /// </summary>
        /// <param name="scene">The scene.</param>
        public static implicit operator SceneRef(Scene scene)
        {
            ArgumentNullException.ThrowIfNull(scene);
            return scene.SceneUuid != Guid.Empty ? FromUuid(scene.SceneUuid) : FromName(scene.SceneName);
        }

        /// <summary>
        /// Determines whether two references are the same: the same name or the same UUID.
        /// </summary>
        public static bool operator ==(SceneRef left, SceneRef right) => left.Equals(right);

        /// <summary>
        /// Determines whether two references are different.
        /// </summary>
        public static bool operator !=(SceneRef left, SceneRef right) => !left.Equals(right);

        /// <inheritdoc/>
        /// <remarks>A reference by name never equals a reference by UUID, even when they identify the same scene.</remarks>
        public bool Equals(SceneRef other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal) && this.Uuid == other.Uuid;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is SceneRef other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Name, this.Uuid);

        /// <summary>
        /// Gets the name or the UUID of the scene, as text.
        /// </summary>
        /// <returns>The name or the UUID; an empty string for <c>default(SceneRef)</c>.</returns>
        public override string ToString() => this.Name ?? this.Uuid?.ToString() ?? string.Empty;

        internal void EnsureSpecified(string parameterName)
        {
            if (!this.IsSpecified)
            {
                throw new ArgumentException("The scene has to be given by name or by UUID.", parameterName);
            }
        }
    }
}
