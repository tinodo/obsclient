namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the RecordFileChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RecordFileChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="newOutputPath">File name that the output has begun writing to.</param>
    [method: JsonConstructor]
    public class RecordFileChangedEventArgs(string newOutputPath) : EventArgs
    {
        /// <summary>
        /// Gets the file name that the output has begun writing to.
        /// </summary>
        [JsonPropertyName("newOutputPath")]
        public string NewOutputPath { get; } = newOutputPath;
    }
}
