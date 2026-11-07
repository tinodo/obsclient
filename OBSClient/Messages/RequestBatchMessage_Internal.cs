namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;

    public sealed partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request of any type, with its data as it is. The studio layer builds its reads from the requests that its engine asks for.
        /// </summary>
        /// <param name="requestType">The type of the request.</param>
        /// <param name="requestData">The data of the request, or null.</param>
        internal void AddRaw(RequestType requestType, object? requestData)
        {
            this._requests.Add(new RequestMessage(requestType, requestData));
        }
    }
}
