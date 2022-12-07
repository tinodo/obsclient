namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class CallVendorRequestResponse : IResponse
    {
        [JsonPropertyName("vendorName")]
        public string VendorName { get; set; }

        [JsonPropertyName("requestType")]
        public string RequestType { get; set; }

        [JsonPropertyName("responseData")]
        public object? ResponseData { get; set; }

        public CallVendorRequestResponse(string vendorName, string requestType, object? responseData)
        {
            this.VendorName = vendorName;
            this.RequestType = requestType;
            this.ResponseData = responseData;
        }
    }
}
