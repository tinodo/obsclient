namespace OBSStudioClient.Classes
{
    /// <summary>
    /// Identifies an input to obs-websocket: by its name or by its UUID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wherever a method asks for an <see cref="InputRef"/> you can pass a name (<c>"Microphone"</c>), a <see cref="Guid"/> or an
    /// <see cref="Input"/> that OBS Studio gave you: they convert implicitly. A UUID stays the same when the input is renamed.
    /// </para>
    /// <para>
    /// <c>default(InputRef)</c> identifies nothing; the methods refuse it with an <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    public readonly struct InputRef : IEquatable<InputRef>
    {
        private InputRef(string? name, Guid? uuid)
        {
            this.Name = name;
            this.Uuid = uuid;
        }

        /// <summary>
        /// Gets the name of the input, or null when the input is identified by its UUID.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the UUID of the input, or null when the input is identified by its name.
        /// </summary>
        public Guid? Uuid { get; }

        /// <summary>
        /// Gets a value indicating whether this identifies an input (<c>default(InputRef)</c> does not).
        /// </summary>
        public bool IsSpecified => this.Name is not null || this.Uuid.HasValue;

        /// <summary>
        /// Identifies an input by its name.
        /// </summary>
        /// <param name="name">The name of the input.</param>
        /// <returns>A reference to the input.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public static InputRef FromName(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return new InputRef(name, null);
        }

        /// <summary>
        /// Identifies an input by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the input.</param>
        /// <returns>A reference to the input.</returns>
        /// <exception cref="ArgumentException"><paramref name="uuid"/> is <see cref="Guid.Empty"/>.</exception>
        public static InputRef FromUuid(Guid uuid)
        {
            if (uuid == Guid.Empty)
            {
                throw new ArgumentException("A UUID cannot be empty.", nameof(uuid));
            }

            return new InputRef(null, uuid);
        }

        /// <summary>
        /// Identifies an input by its name.
        /// </summary>
        /// <param name="name">The name of the input.</param>
        public static implicit operator InputRef(string name) => FromName(name);

        /// <summary>
        /// Identifies an input by its UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the input.</param>
        public static implicit operator InputRef(Guid uuid) => FromUuid(uuid);

        /// <summary>
        /// Identifies an input that OBS Studio reported, by its UUID (or by its name when OBS Studio did not report a UUID).
        /// </summary>
        /// <param name="input">The input.</param>
        public static implicit operator InputRef(Input input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return input.InputUuid is { } uuid && uuid != Guid.Empty ? FromUuid(uuid) : FromName(input.InputName);
        }

        /// <summary>
        /// Determines whether two references are the same: the same name or the same UUID.
        /// </summary>
        public static bool operator ==(InputRef left, InputRef right) => left.Equals(right);

        /// <summary>
        /// Determines whether two references are different.
        /// </summary>
        public static bool operator !=(InputRef left, InputRef right) => !left.Equals(right);

        /// <inheritdoc/>
        /// <remarks>A reference by name never equals a reference by UUID, even when they identify the same input.</remarks>
        public bool Equals(InputRef other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal) && this.Uuid == other.Uuid;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is InputRef other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Name, this.Uuid);

        /// <summary>
        /// Gets the name or the UUID of the input, as text.
        /// </summary>
        /// <returns>The name or the UUID; an empty string for <c>default(InputRef)</c>.</returns>
        public override string ToString() => this.Name ?? this.Uuid?.ToString() ?? string.Empty;

        internal void EnsureSpecified(string parameterName)
        {
            if (!this.IsSpecified)
            {
                throw new ArgumentException("The input has to be given by name or by UUID.", parameterName);
            }
        }
    }
}
