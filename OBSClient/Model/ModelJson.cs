namespace OBSStudioClient.Model
{
    using System.Text.Json;

    /// <summary>
    /// Makes the JSON objects that OBS Studio takes as data (the settings of an output, the data of a custom event, the request of a vendor) from what an application passes: an anonymous
    /// object, a dictionary, a JSON element.
    /// </summary>
    internal static class ModelJson
    {
        /// <summary>
        /// Writes a value as a JSON object.
        /// </summary>
        /// <param name="value">What the application passed.</param>
        /// <param name="parameter">The name of the parameter that it was passed as, for the message.</param>
        /// <param name="what">What the object is, for the message: "the settings of an output".</param>
        /// <param name="example">An example of such an object, for the message.</param>
        /// <returns>The object.</returns>
        /// <exception cref="ArgumentException">The value is not an object, or can not be written as JSON.</exception>
        public static JsonElement AsObject(object value, string parameter, string what, string example)
        {
            // The parameter is an object so that new { gamma = 0.5 } can be passed: a token that was meant for cancellationToken would be taken for it, and this is what the call says then.
            if (value is CancellationToken)
            {
                throw new ArgumentException($"A CancellationToken was passed as {what}. Pass it by name (cancellationToken: token) when {what} is left out.", parameter);
            }

            JsonElement element;
            try
            {
                element = value is JsonElement given ? given.Clone() : JsonSerializer.SerializeToElement(value, value.GetType());
            }
            catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
            {
                throw new ArgumentException($"{Capitalize(what)} can not be written as JSON: {ex.Message}", parameter, ex);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"{Capitalize(what)} is an object: the fields and their values, for example {example}.", parameter);
            }

            return element;
        }

        private static string Capitalize(string text)
        {
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
        }
    }
}
