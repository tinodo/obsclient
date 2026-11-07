namespace OBSStudioClient.Studio
{
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// The scenes or the inputs of OBS Studio, which can be found by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A collection is a snapshot of which objects there are, and in which order: it does not change while you look at it, and it is safe to use
    /// from any thread. Ask the <see cref="ObsStudio"/> for the collection again to see what changed (or react to <see cref="ObsStudio.Changed"/>).
    /// </para>
    /// <para>
    /// A name is looked up exactly, and case-sensitively, as OBS Studio does, against the names that the objects have now. With
    /// <see cref="ObsStudioOptions.IgnoreCase"/> a name that differs only by case also matches, when there is no exact match and that leaves one object.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the objects: <see cref="ObsScene"/> or <see cref="ObsInput"/>.</typeparam>
    public sealed class ObsNamedCollection<T> : IReadOnlyList<T>
        where T : ObsSource
    {
        private readonly T[] _items;
        private readonly string _kind;
        private readonly bool _ignoreCase;

        internal ObsNamedCollection(string kind, T[] items, bool ignoreCase)
        {
            this._kind = kind;
            this._items = items;
            this._ignoreCase = ignoreCase;
        }

        /// <summary>
        /// Gets the number of objects.
        /// </summary>
        public int Count => this._items.Length;

        /// <summary>
        /// Gets the object at a position. For scenes, that is their position in the list of scenes of OBS Studio.
        /// </summary>
        /// <param name="index">The position.</param>
        public T this[int index] => this._items[index];

        /// <summary>
        /// Gets the object with a name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ObsNotFoundException">There is no object with that name. The exception suggests names that are close.</exception>
        /// <exception cref="ObsAmbiguousNameException">More than one object matches (only possible when case is ignored).</exception>
        public T this[string name]
        {
            get
            {
                var count = NameLookup.Count(this._items, NameOf, name, this._ignoreCase, out var first);
                return count == 1 ? first! : throw NameLookup.Failure(this._items, NameOf, Describe, this._kind, name, count, this._ignoreCase);
            }
        }

        /// <summary>
        /// Gets the object with a UUID.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <exception cref="ObsNotFoundException">There is no object with that UUID.</exception>
        public T this[Guid uuid] => this.TryGet(uuid, out var found) ? found : throw new ObsNotFoundException(this._kind, uuid.ToString(), []);

        /// <summary>
        /// Gets the object with a name, if there is one.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The object.</param>
        /// <returns>Whether there is an object with the name.</returns>
        /// <exception cref="ObsAmbiguousNameException">More than one object matches (only possible when case is ignored).</exception>
        public bool TryGet(string name, [MaybeNullWhen(false)] out T value)
        {
            var count = NameLookup.Count(this._items, NameOf, name, this._ignoreCase, out var first);
            if (count > 1)
            {
                throw NameLookup.Failure(this._items, NameOf, Describe, this._kind, name, count, this._ignoreCase);
            }

            value = first;
            return count == 1;
        }

        /// <summary>
        /// Gets the object with a UUID, if there is one.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <param name="value">The object.</param>
        /// <returns>Whether there is an object with the UUID.</returns>
        public bool TryGet(Guid uuid, [MaybeNullWhen(false)] out T value)
        {
            foreach (var item in this._items)
            {
                if (item.Uuid == uuid)
                {
                    value = item;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Gets the object with a name, or null.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ObsAmbiguousNameException">More than one object matches (only possible when case is ignored).</exception>
        public T? Find(string name)
        {
            return this.TryGet(name, out var found) ? found : null;
        }

        /// <summary>
        /// Gets all objects with a name. Without <see cref="ObsStudioOptions.IgnoreCase"/> that is none or one.
        /// </summary>
        /// <param name="name">The name.</param>
        public IReadOnlyList<T> FindAll(string name)
        {
            return NameLookup.All(this._items, NameOf, name, this._ignoreCase);
        }

        /// <summary>
        /// Gets whether there is an object with a name.
        /// </summary>
        /// <param name="name">The name.</param>
        public bool Contains(string name)
        {
            return NameLookup.Count(this._items, NameOf, name, this._ignoreCase, out _) > 0;
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this._items).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        // Whether the collection holds these objects, in this order.
        internal bool HoldsExactly(T[] other)
        {
            if (this._items.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < other.Length; i++)
            {
                if (!ReferenceEquals(this._items[i], other[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NameOf(T item)
        {
            return item.Name;
        }

        private static string Describe(T item)
        {
            return $"'{item.Name}' ({item.Uuid})";
        }
    }
}
