namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SavedReplayPathResponseData : IResponseData
    {
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; set; }

        [JsonConstructor]
        public SavedReplayPathResponseData(string savedReplayPath)
        {
            this.SavedReplayPath = savedReplayPath;
        }
    }
}
