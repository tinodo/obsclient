namespace OBSStudioClient.Tests.Conformance
{
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Creates arguments for calling any request method of the client (or request builder of a batch) through reflection.
    /// </summary>
    internal static class ArgumentFactory
    {
        /// <summary>
        /// Creates a value for every parameter of <paramref name="method"/>. Every optional field of the request gets
        /// a value, so the request that is sent shows which fields the method can reach.
        /// </summary>
        /// <param name="method">The method to create the arguments for.</param>
        /// <param name="nullParameterIndex">The index of a parameter that gets null instead of a value.</param>
        public static object?[] Create(MethodInfo method, int? nullParameterIndex = null)
        {
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                arguments[index] = index == nullParameterIndex ? null : CreateValue(parameters[index].ParameterType, 0);
            }

            return arguments;
        }

        /// <summary>Returns the indexes of the parameters of <paramref name="method"/> that accept null.</summary>
        public static IReadOnlyList<int> NullableParameterIndexes(MethodInfo method)
        {
            var context = new NullabilityInfoContext();
            var parameters = method.GetParameters();
            var indexes = new List<int>();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (context.Create(parameters[index]).WriteState == NullabilityState.Nullable)
                {
                    indexes.Add(index);
                }
            }

            return indexes;
        }

        /// <summary>Describes a method for use in a failure message.</summary>
        public static string Describe(MethodInfo method)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));
            return $"{method.DeclaringType?.Name}.{method.Name}({parameters})";
        }

        private static object? CreateValue(Type type, int depth)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string))
            {
                return "test";
            }

            if (underlying == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            if (underlying == typeof(bool))
            {
                return true;
            }

            if (underlying == typeof(int))
            {
                return 1;
            }

            if (underlying == typeof(long))
            {
                return 1L;
            }

            if (underlying == typeof(float))
            {
                return 0.5f;
            }

            if (underlying == typeof(double))
            {
                return 0.5d;
            }

            if (underlying.IsEnum)
            {
                return FirstValue(underlying);
            }

            if (underlying == typeof(Dictionary<string, object>))
            {
                return new Dictionary<string, object> { ["key"] = "value" };
            }

            if (underlying == typeof(JsonElement))
            {
                return JsonSerializer.SerializeToElement(new { key = "value" });
            }

            if (underlying.IsArray)
            {
                var elementType = underlying.GetElementType()!;
                var array = Array.CreateInstance(elementType, 1);
                array.SetValue(CreateValue(elementType, depth + 1), 0);
                return array;
            }

            if (depth >= 3)
            {
                return null;
            }

            var constructor = underlying
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(candidate => candidate.GetParameters().Length)
                .FirstOrDefault();

            return constructor?.Invoke(constructor.GetParameters().Select(parameter => CreateValue(parameter.ParameterType, depth + 1)).ToArray());
        }

        private static object FirstValue(Type enumType)
        {
            var values = Enum.GetValues(enumType).Cast<object>().ToList();
            return values.FirstOrDefault(value => Convert.ToInt64(value) > 0) ?? values[0];
        }
    }
}
