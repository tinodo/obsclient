﻿namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful CallVendorRequest request.
    /// </summary>
    public class CallVendorResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the vendor.
        /// </summary>
        [JsonPropertyName("vendorName")]
        public string VendorName { get; }

        /// <summary>
        /// Gets the type of the request, as defined by the vendor.
        /// </summary>
        [JsonPropertyName("requestType")]
        public string RequestType { get; }

        /// <summary>
        /// Gets the response data object, as defined by the vendor.
        /// </summary>
        [JsonPropertyName("responseData")]
        public JsonElement? ResponseData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallVendorResponse"/> class.
        /// </summary>
        /// <param name="vendorName">Name of the vendor.</param>
        /// <param name="requestType">Type of request.</param>
        /// <param name="responseData">The response data.</param>
        public CallVendorResponse(string vendorName, string requestType, JsonElement? responseData)
        {
            this.VendorName = vendorName;
            this.RequestType = requestType;
            this.ResponseData = responseData;
        }
    }
}
