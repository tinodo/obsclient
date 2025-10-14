namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful CreateRecordChapter request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FiltersResponse"/> class.
    /// </remarks>
    /// <param name="chapterName">Name of the new chapter.</param>
    public class CreateRecordChapterResponse(string? chapterName) : IResponse
    {
        /// <summary>
        /// Gets the name of the new chapter.
        /// </summary>
        [JsonPropertyName("chapterName")]
        public string? ChapterName { get; } = chapterName;
    }
}
