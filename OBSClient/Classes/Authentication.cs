namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Authentication
    {
        [JsonPropertyName("challenge")]
        public string Challenge { get; }

        [JsonPropertyName("salt")]
        public string Salt { get; }

        [JsonConstructor]
        public Authentication(string challenge, string salt)
        {
            Challenge = challenge;
            Salt = salt;
        }
    }
}
