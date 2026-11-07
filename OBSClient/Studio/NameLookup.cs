namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// How objects are found by name: an exact, case-sensitive match first; optionally one that ignores case, when there is no exact match;
    /// and when that does not leave exactly one object, a clear error, with suggestions when there is none and the candidates when there are several.
    /// </summary>
    internal static class NameLookup
    {
        /// <summary>
        /// Counts the objects with a name, and returns the first of them. It does not allocate: this is the path of a lookup that works.
        /// </summary>
        public static int Count<T>(T[] items, Func<T, string> nameOf, string requested, bool ignoreCase, out T? first)
        {
            ArgumentNullException.ThrowIfNull(requested);
            var count = 0;
            first = default;
            foreach (var item in items)
            {
                if (string.Equals(nameOf(item), requested, StringComparison.Ordinal) && count++ == 0)
                {
                    first = item;
                }
            }

            if (count > 0 || !ignoreCase)
            {
                return count;
            }

            foreach (var item in items)
            {
                if (string.Equals(nameOf(item), requested, StringComparison.OrdinalIgnoreCase) && count++ == 0)
                {
                    first = item;
                }
            }

            return count;
        }

        /// <summary>Returns the objects with a name: the exact matches, or when there are none and case is ignored, those that differ only by case.</summary>
        public static T[] All<T>(T[] items, Func<T, string> nameOf, string requested, bool ignoreCase)
        {
            ArgumentNullException.ThrowIfNull(requested);
            var found = items.Where(item => string.Equals(nameOf(item), requested, StringComparison.Ordinal)).ToArray();
            if (found.Length > 0 || !ignoreCase)
            {
                return found;
            }

            return items.Where(item => string.Equals(nameOf(item), requested, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        /// <summary>
        /// Makes the exception for a name that did not match exactly one object.
        /// </summary>
        /// <param name="items">The objects that were searched.</param>
        /// <param name="nameOf">Gets the name of an object.</param>
        /// <param name="describe">Describes an object, to tell the candidates apart.</param>
        /// <param name="kind">What the objects are, for the message.</param>
        /// <param name="requested">The name that was asked for.</param>
        /// <param name="count">The number of objects that matched.</param>
        /// <param name="ignoreCase">Whether case is ignored.</param>
        /// <param name="scope">Where the objects are, for the message.</param>
        public static ObsClientException Failure<T>(T[] items, Func<T, string> nameOf, Func<T, string> describe, string kind, string requested, int count, bool ignoreCase, string? scope = null)
        {
            if (count == 0)
            {
                return new ObsNotFoundException(kind, requested, Suggest(items.Select(nameOf), requested), scope);
            }

            return new ObsAmbiguousNameException(kind, requested, All(items, nameOf, requested, ignoreCase).Select(describe).ToArray(), scope);
        }

        /// <summary>Returns up to three of the names that are close to the one that was asked for, the closest first.</summary>
        public static IReadOnlyList<string> Suggest(IEnumerable<string> names, string requested)
        {
            var lowered = requested.ToLowerInvariant();
            var limit = Math.Max(2, requested.Length / 3);
            return names
                .Distinct(StringComparer.Ordinal)
                .Select(name => (Name: name, Distance: Distance(name.ToLowerInvariant(), lowered)))
                .Where(candidate => candidate.Distance <= limit)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .Take(3)
                .Select(candidate => candidate.Name)
                .ToArray();
        }

        // The number of single-character edits that turn one string into the other (Levenshtein).
        private static int Distance(string a, string b)
        {
            if (a.Length == 0)
            {
                return b.Length;
            }

            if (b.Length == 0)
            {
                return a.Length;
            }

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }
    }
}
