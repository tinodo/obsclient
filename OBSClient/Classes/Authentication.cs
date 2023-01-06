namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Authentication.
    /// </summary>
    public class Authentication
    {
        /// <summary>
        /// Gets the challenge to be signed.
        /// </summary>
        [JsonPropertyName("challenge")]
        public string Challenge { get; }

        /// <summary>
        /// Gets the salt to use using signing.
        /// </summary>
        [JsonPropertyName("salt")]
        public string Salt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Authentication"/> class.
        /// </summary>
        /// <param name="challenge">The challenge to be signed.</param>
        /// <param name="salt">The salt to use using signing.</param>
        [JsonConstructor]
        public Authentication(string challenge, string salt)
        {
            this.Challenge = challenge;
            this.Salt = salt;
        }
    }
}
