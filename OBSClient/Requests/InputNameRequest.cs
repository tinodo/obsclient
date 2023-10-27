namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an inputName.
    /// </summary>
    public class InputNameRequest
    {
        /// <summary>
        /// Gets or sets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputNameRequest"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        [JsonConstructor]
        public InputNameRequest(string inputName)
        {
            this.InputName = inputName;
        }
    }
}
