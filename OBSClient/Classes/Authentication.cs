namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Authentication.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Authentication"/> class.
    /// </remarks>
    /// <param name="challenge">The challenge to be signed.</param>
    /// <param name="salt">The salt to use using signing.</param>
    [method: JsonConstructor]
    public class Authentication(string challenge, string salt)
    {
        /// <summary>
        /// Gets the challenge to be signed.
        /// </summary>
        [JsonPropertyName("challenge")]
        public string Challenge { get; } = challenge;

        /// <summary>
        /// Gets the salt to use using signing.
        /// </summary>
        [JsonPropertyName("salt")]
        public string Salt { get; } = salt;
    }
}
