namespace OBSStudioClient.Studio
{
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// The scene items of a scene, from the bottom (index 0) to the top, which can be found by the name of their source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene item is looked up by the name of the source that it shows. The same source can be in a scene more than once, so a name can match
    /// more than one scene item: the indexer then throws an <see cref="ObsAmbiguousNameException"/> that lists them, and
    /// <see cref="FindAll"/> returns all of them. Use <see cref="ById"/> to find one scene item by its id.
    /// </para>
    /// <para>
    /// The collection is a snapshot of which scene items there are, and in which order: it is safe to use from any thread; ask the scene for its
    /// items again to see what changed. The names are looked up as the sources have them now.
    /// </para>
    /// </remarks>
    public sealed class ObsSceneItems : IReadOnlyList<ObsSceneItem>
    {
        private readonly ObsScene _scene;
        private readonly ObsSceneItem[] _items;
        private readonly bool _ignoreCase;

        internal ObsSceneItems(ObsScene scene, ObsSceneItem[] items, bool ignoreCase)
        {
            this._scene = scene;
            this._items = items;
            this._ignoreCase = ignoreCase;
        }

        /// <summary>
        /// Gets the number of scene items.
        /// </summary>
        public int Count => this._items.Length;

        /// <summary>
        /// Gets the scene item at a position in this collection, which is its <see cref="ObsSceneItem.Index"/> once the model is up to date: 0 is the bottom.
        /// </summary>
        /// <param name="index">The position.</param>
        public ObsSceneItem this[int index] => this._items[index];

        /// <summary>
        /// Gets the scene item whose source has a name.
        /// </summary>
        /// <param name="sourceName">The name of the source: an input or a scene.</param>
        /// <exception cref="ObsNotFoundException">No scene item shows a source with that name. The exception suggests names that are close.</exception>
        /// <exception cref="ObsAmbiguousNameException">More than one scene item shows it. The exception lists them.</exception>
        public ObsSceneItem this[string sourceName]
        {
            get
            {
                var count = NameLookup.Count(this._items, NameOf, sourceName, this._ignoreCase, out var first);
                return count == 1 ? first! : throw NameLookup.Failure(this._items, NameOf, Describe, "scene item", sourceName, count, this._ignoreCase, $"scene '{this._scene.Name}'");
            }
        }

        /// <summary>
        /// Gets the scene item whose source has a name, if there is exactly one.
        /// </summary>
        /// <param name="sourceName">The name of the source.</param>
        /// <param name="item">The scene item.</param>
        /// <returns>Whether there is one scene item for the name.</returns>
        /// <exception cref="ObsAmbiguousNameException">More than one scene item shows it.</exception>
        public bool TryGet(string sourceName, [MaybeNullWhen(false)] out ObsSceneItem item)
        {
            var count = NameLookup.Count(this._items, NameOf, sourceName, this._ignoreCase, out var first);
            if (count > 1)
            {
                throw NameLookup.Failure(this._items, NameOf, Describe, "scene item", sourceName, count, this._ignoreCase, $"scene '{this._scene.Name}'");
            }

            item = first;
            return count == 1;
        }

        /// <summary>
        /// Gets all scene items whose source has a name, from the bottom to the top.
        /// </summary>
        /// <param name="sourceName">The name of the source.</param>
        public IReadOnlyList<ObsSceneItem> FindAll(string sourceName)
        {
            return NameLookup.All(this._items, NameOf, sourceName, this._ignoreCase);
        }

        /// <summary>
        /// Gets the scene item with an id.
        /// </summary>
        /// <param name="id">The id of the scene item.</param>
        /// <exception cref="ObsNotFoundException">The scene has no scene item with that id.</exception>
        public ObsSceneItem ById(int id)
        {
            foreach (var item in this._items)
            {
                if (item.Id == id)
                {
                    return item;
                }
            }

            throw new ObsNotFoundException("scene item with id", id.ToString(System.Globalization.CultureInfo.InvariantCulture), [], $"scene '{this._scene.Name}'");
        }

        /// <inheritdoc/>
        public IEnumerator<ObsSceneItem> GetEnumerator()
        {
            return ((IEnumerable<ObsSceneItem>)this._items).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        // Whether the collection holds these scene items, in this order.
        internal bool HoldsExactly(ObsSceneItem[] other)
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

        private static string NameOf(ObsSceneItem item)
        {
            return item.Source.Name;
        }

        private static string Describe(ObsSceneItem item)
        {
            return $"id {item.Id} (index {item.Index})";
        }
    }
}
