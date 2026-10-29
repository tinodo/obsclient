namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Input"/> class.
    /// </remarks>
    /// <param name="inputKind">The input kind.</param>
    /// <param name="inputName">The input name.</param>
    /// <param name="unversionedInputKind">The unversioned input kind.</param>
    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    [method: JsonConstructor]
    public sealed class Input(string inputKind, string inputName, string unversionedInputKind)
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; } = unversionedInputKind;

        /// <summary>
        /// Gets the uuid of the input.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> when OBS Studio does not send it. It is an <see langword="init"/> property, rather
        /// than a constructor parameter, to keep the public constructor of this class unchanged.
        /// </remarks>
        [JsonPropertyName("inputUuid")]
        public Guid? InputUuid { get; init; }

        /// <summary>
        /// Gets the capabilities of the input kind, as a bitflag value. See <c>obs_source_info.output_flags</c> in the libobs documentation.
        /// </summary>
        /// <remarks>
        /// Introduced in obs-websocket 5.6.0; <see langword="null"/> when OBS Studio does not send it.
        /// A flags value of 32 bits, which is why it is an unsigned number (and not CLS-compliant).
        /// </remarks>
        [JsonPropertyName("inputKindCaps")]
        [CLSCompliant(false)]
        public uint? InputKindCaps { get; init; }
    }
}