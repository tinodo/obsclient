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
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                names.Add(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name));
            }

            return names;
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
