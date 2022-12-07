namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SavedReplayPathResponse : IResponse
    {
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; set; }

        [JsonConstructor]
        public SavedReplayPathResponse(string savedReplayPath)
        {
            this.SavedReplayPath = savedReplayPath;
        }
    }
}
