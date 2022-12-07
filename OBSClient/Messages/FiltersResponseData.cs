namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class FiltersResponseData : IResponseData
    {
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; set; }

        public FiltersResponseData(Filter[] filters)
        {
            this.Filters = filters ?? Array.Empty<Filter>();
        }
    }
}
