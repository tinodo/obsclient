namespace OBSStudioClient.Model
{
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// The filters of a scene or an input, from the first (index 0) to the last, which can be found by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name of a filter is unique within its source, so a name finds one filter. With <see cref="ObsModelOptions.IgnoreCase"/> a name that differs only by case matches too, when there
    /// is no exact match: two filters that match then are an <see cref="ObsAmbiguousNameException"/>.
    /// </para>
    /// <para>
    /// The collection is a snapshot of which filters there are, and in which order: it is safe to use from any thread; ask the source for its filters again to see what changed.
    /// The names are looked up as the filters have them now.
    /// </para>
    /// </remarks>
    public sealed class ObsFilters : IReadOnlyList<ObsFilter>
    {
        private readonly ObsSource _source;
        private readonly ObsFilter[] _filters;
        private readonly bool _ignoreCase;

        internal ObsFilters(ObsSource source, ObsFilter[] filters, bool ignoreCase)
        {
            this._source = source;
            this._filters = filters;
            this._ignoreCase = ignoreCase;
        }

        /// <summary>
        /// Gets the number of filters.
        /// </summary>
        public int Count => this._filters.Length;

        /// <summary>
        /// Gets the filter at a position in this collection, which is its <see cref="ObsFilter.Index"/> once the model is up to date: 0 is the first.
        /// </summary>
        /// <param name="index">The position.</param>
        public ObsFilter this[int index] => this._filters[index];

        /// <summary>
        /// Gets the filter with a name.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        /// <exception cref="ObsNotFoundException">The source has no filter with that name. The exception suggests names that are close.</exception>
        /// <exception cref="ObsAmbiguousNameException">Case is ignored, and more than one filter differs from the name only by case. The exception lists them.</exception>
        public ObsFilter this[string name]
        {
            get
            {
                var count = NameLookup.Count(this._filters, NameOf, name, this._ignoreCase, out var first);
                return count == 1 ? first! : throw NameLookup.Failure(this._filters, NameOf, Describe, "filter", name, count, this._ignoreCase, this._source.Label);
            }
        }

        /// <summary>
        /// Gets the filter with a name, if there is exactly one.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>Whether there is a filter with the name.</returns>
        /// <exception cref="ObsAmbiguousNameException">Case is ignored, and more than one filter differs from the name only by case.</exception>
        public bool TryGet(string name, [MaybeNullWhen(false)] out ObsFilter filter)
        {
            var count = NameLookup.Count(this._filters, NameOf, name, this._ignoreCase, out var first);
            if (count > 1)
            {
                throw NameLookup.Failure(this._filters, NameOf, Describe, "filter", name, count, this._ignoreCase, this._source.Label);
            }

            filter = first;
            return count == 1;
        }

        /// <summary>
        /// Gets the filter with a name, or null when there is none.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        /// <exception cref="ObsAmbiguousNameException">Case is ignored, and more than one filter differs from the name only by case.</exception>
        public ObsFilter? Find(string name)
        {
            return this.TryGet(name, out var filter) ? filter : null;
        }

        /// <summary>
        /// Gets whether there is a filter with a name.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        public bool Contains(string name)
        {
            return NameLookup.Count(this._filters, NameOf, name, this._ignoreCase, out _) > 0;
        }

        /// <summary>
        /// Gets the filter with exactly this name (case sensitive), or null: for the code that waits for a filter that it made.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        internal ObsFilter? FindExact(string name)
        {
            foreach (var filter in this._filters)
            {
                if (string.Equals(filter.Name, name, StringComparison.Ordinal))
                {
                    return filter;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public IEnumerator<ObsFilter> GetEnumerator()
        {
            return ((IEnumerable<ObsFilter>)this._filters).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        // Whether the collection holds these filters, in this order.
        internal bool HoldsExactly(ObsFilter[] other)
        {
            if (this._filters.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < other.Length; i++)
            {
                if (!ReferenceEquals(this._filters[i], other[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NameOf(ObsFilter filter)
        {
            return filter.Name;
        }

        private static string Describe(ObsFilter filter)
        {
            return $"'{filter.Name}' (index {filter.Index})";
        }
    }
}
