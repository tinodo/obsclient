namespace OBSStudioClient.Messages
{
    using System.Text.Json;

    /// <summary>
    /// Turns the data of a request into the JSON that is sent to obs-websocket.
    /// </summary>
    internal static class RequestPayload
    {
        /// <summary>
        /// Serializes the data of a request.
        /// </summary>
        /// <param name="data">The data: an object whose public properties are the fields of the request, or null.</param>
        /// <returns>The JSON object, or null when there is nothing to send (obs-websocket treats no data and an empty object alike).</returns>
        internal static JsonElement? ToElement(object? data)
        {
            if (data is null)
            {
                return null;
            }

            var element = JsonSerializer.SerializeToElement(data, data.GetType());
            return element.ValueKind == JsonValueKind.Object && !element.EnumerateObject().MoveNext() ? null : element;
        }
    }
}
