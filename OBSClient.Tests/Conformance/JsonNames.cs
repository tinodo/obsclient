namespace OBSStudioClient.Tests.Conformance
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>Works out which JSON names a type of the client reads from, or writes to, obs-websocket.</summary>
    internal static class JsonNames
    {
        public static HashSet<string> Of(Type type)
        {
            return Properties(type).Keys.ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>Returns the type of the client's property for each JSON name of <paramref name="type"/>.</summary>
        public static Dictionary<string, Type> PropertyTypes(Type type)
        {
            return Properties(type).ToDictionary(pair => pair.Key, pair => pair.Value.PropertyType, StringComparer.Ordinal);
        }

        /// <summary>Returns the client's property for each JSON name of <paramref name="type"/>.</summary>
        public static Dictionary<string, PropertyInfo> Properties(Type type)
        {
            var properties = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                properties[property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property;
            }

            return properties;
        }
    }

    /// <summary>Turns a list of problems into one readable assertion.</summary>
    internal static class ConformanceAssert
    {
        public static void NoProblems(IReadOnlyCollection<string> problems, string headline)
        {
            Assert.True(
                problems.Count == 0,
                headline + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)));
        }
    }
}
