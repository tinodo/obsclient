namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class FiltersResponse : IResponse
    {
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; set; }

        public FiltersResponse(Filter[] filters)
        {
            this.Filters = filters ?? Array.Empty<Filter>();
        }
    }
}
