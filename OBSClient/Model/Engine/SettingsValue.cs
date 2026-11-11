namespace OBSStudioClient.Model.Engine
{
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// The settings of an input or a filter as OBS Studio says them: a JSON object with the values that were set, and not the defaults of the kind (the events and the reads
    /// leave them out). Two values are equal when they hold the same fields with equal values, whatever the order of the fields: that is what a cell compares to find out that the
    /// settings changed.
    /// </summary>
    internal sealed class SettingsValue : IEquatable<SettingsValue>
    {
        private SettingsValue(JsonElement element)
        {
            this.Element = element;
        }

        /// <summary>Gets the settings with no field.</summary>
        public static SettingsValue Empty { get; } = new(JsonDocument.Parse("{}").RootElement);

        /// <summary>Gets the settings as a JSON object. It belongs to the value for as long as the value lives, and nobody disposes of its document.</summary>
        public JsonElement Element { get; }

        /// <summary>Makes the settings from what the client read: the fields of the object, by name.</summary>
        /// <param name="fields">The fields (the values are JSON elements), or null for none.</param>
        public static SettingsValue From(IReadOnlyDictionary<string, object>? fields)
        {
            return fields is null || fields.Count == 0 ? Empty : new SettingsValue(JsonSerializer.SerializeToElement(fields));
        }

        /// <summary>Makes the settings from a JSON object. It is copied, so the value does not depend on the document of the element.</summary>
        /// <param name="element">The object.</param>
        public static SettingsValue From(JsonElement element)
        {
            return new SettingsValue(element.Clone());
        }

        /// <summary>Gets whether two JSON values are the same: the same fields with the same values (in any order), the same elements in the same order, the same text, the same number.</summary>
        public static bool DeepEquals(JsonElement left, JsonElement right)
        {
            if (left.ValueKind != right.ValueKind)
            {
                return false;
            }

            switch (left.ValueKind)
            {
                case JsonValueKind.Object:
                    var count = 0;
                    foreach (var property in left.EnumerateObject())
                    {
                        count++;
                        if (!right.TryGetProperty(property.Name, out var other) || !DeepEquals(property.Value, other))
                        {
                            return false;
                        }
                    }

                    return count == Count(right);
                case JsonValueKind.Array:
                    if (left.GetArrayLength() != right.GetArrayLength())
                    {
                        return false;
                    }

                    using (var rightItems = right.EnumerateArray().GetEnumerator())
                    {
                        foreach (var item in left.EnumerateArray())
                        {
                            rightItems.MoveNext();
                            if (!DeepEquals(item, rightItems.Current))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                case JsonValueKind.String:
                    return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
                case JsonValueKind.Number:
                    return NumbersEqual(left, right);
                default:
                    return true;
            }
        }

        /// <summary>
        /// Gets whether a JSON object has everything that another one holds: every field of the subset is in it, with the value that the subset gives (an object that is a value is
        /// looked at the same way, so a field that is set inside it is enough). A field of the subset that is null asks for nothing: OBS Studio does not keep a null, and leaves the field
        /// that it was laid over as it is.
        /// </summary>
        /// <param name="whole">What OBS Studio holds.</param>
        /// <param name="subset">What was asked for.</param>
        public static bool Contains(JsonElement whole, JsonElement subset)
        {
            if (subset.ValueKind == JsonValueKind.Object && whole.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in subset.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    if (!whole.TryGetProperty(property.Name, out var found) || !Contains(found, property.Value))
                    {
                        return false;
                    }
                }

                return true;
            }

            return DeepEquals(whole, subset);
        }

        /// <summary>
        /// Gets a JSON value without its fields that are null, at any depth: that is what OBS Studio keeps of settings that it is given (it does not keep a null), so it is what settings that
        /// were given have to equal when OBS Studio has taken all its own settings back to nothing first.
        /// </summary>
        /// <param name="element">The value.</param>
        public static JsonElement WithoutNulls(JsonElement element)
        {
            var node = JsonNode.Parse(element.GetRawText());
            Strip(node);
            return JsonSerializer.SerializeToElement(node);
        }

        private static void Strip(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject fields:
                    foreach (var name in fields.Where(field => field.Value is null).Select(field => field.Key).ToArray())
                    {
                        fields.Remove(name);
                    }

                    foreach (var field in fields)
                    {
                        Strip(field.Value);
                    }

                    break;
                case JsonArray items:
                    foreach (var item in items)
                    {
                        Strip(item);
                    }

                    break;
            }
        }

        /// <inheritdoc/>
        public bool Equals(SettingsValue? other)
        {
            return ReferenceEquals(this, other) || (other is not null && DeepEquals(this.Element, other.Element));
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return this.Equals(obj as SettingsValue);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Hash(this.Element);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Element.GetRawText();
        }

        private static int Count(JsonElement element)
        {
            var count = 0;
            foreach (var unused in element.EnumerateObject())
            {
                count++;
            }

            return count;
        }

        // 1 and 1.0 are the same number; a number that does not fit a decimal or a double is compared by its text.
        private static bool NumbersEqual(JsonElement left, JsonElement right)
        {
            if (left.GetRawText() == right.GetRawText())
            {
                return true;
            }

            if (left.TryGetDecimal(out var leftDecimal) && right.TryGetDecimal(out var rightDecimal))
            {
                return leftDecimal == rightDecimal;
            }

            return left.TryGetDouble(out var leftDouble) && right.TryGetDouble(out var rightDouble) && leftDouble == rightDouble;
        }

        private static int Hash(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    // The order of the fields does not count.
                    var fields = 17;
                    foreach (var property in element.EnumerateObject())
                    {
                        fields ^= HashCode.Combine(property.Name, Hash(property.Value));
                    }

                    return fields;
                case JsonValueKind.Array:
                    var items = 19;
                    foreach (var item in element.EnumerateArray())
                    {
                        items = HashCode.Combine(items, Hash(item));
                    }

                    return items;
                case JsonValueKind.String:
                    return element.GetString()!.GetHashCode(StringComparison.Ordinal);
                case JsonValueKind.Number:
                    return element.TryGetDouble(out var number) ? number.GetHashCode() : element.GetRawText().GetHashCode(StringComparison.Ordinal);
                default:
                    return (int)element.ValueKind;
            }
        }
    }
}
