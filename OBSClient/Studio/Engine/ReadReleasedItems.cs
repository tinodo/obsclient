namespace OBSStudioClient.Studio.Engine
{
    /// <summary>
    /// The scene items that a read found gone, whose removal the events have not told yet, and the sources that they used.
    /// </summary>
    /// <remarks>
    /// A read is ahead of the events. As far as the events go, a scene item that a read found gone is still there until the event that removes it
    /// arrives. The model does not count such a scene item among the ones that use a source, but it does not conclude from the count that OBS destroyed
    /// a source while one of them used it either: see <c>StudioEngine.HeldByRead</c>. The scene items that went with a scene that a read said OBS does not have
    /// are held too: the events about the sources that they showed (OBS removed one, and made another scene item that uses it) can still be on their way,
    /// and nothing is said of the scene items themselves. A scene item is let go of when its event arrives, and when the source that it used is destroyed.
    /// </remarks>
    internal sealed class ReadReleasedItems
    {
        private readonly Dictionary<ItemKey, Guid> _items = new();
        private readonly Dictionary<Guid, int> _users = new();

        /// <summary>Gets the number of scene items that are held.</summary>
        public int Count => this._items.Count;

        /// <summary>Holds a scene item that a read found gone.</summary>
        /// <param name="key">The scene item.</param>
        /// <param name="source">The source that it used.</param>
        public void Add(ItemKey key, Guid source)
        {
            if (this._items.TryGetValue(key, out var held))
            {
                this.Release(held);
            }

            this._items[key] = source;
            this._users[source] = this._users.GetValueOrDefault(source) + 1;
        }

        /// <summary>Lets go of a scene item: its removal arrived, or it does not matter any more.</summary>
        /// <param name="key">The scene item.</param>
        /// <returns>Whether it was held.</returns>
        public bool Remove(ItemKey key)
        {
            if (!this._items.Remove(key, out var source))
            {
                return false;
            }

            this.Release(source);
            return true;
        }

        /// <summary>Gets whether a scene item that is held used the source.</summary>
        /// <param name="source">The source.</param>
        /// <returns>True when one did.</returns>
        public bool Holds(Guid source)
        {
            return this._users.ContainsKey(source);
        }

        /// <summary>Lets go of the scene items of a scene.</summary>
        /// <param name="scene">The scene.</param>
        public void RemoveScene(Guid scene)
        {
            if (this._items.Count == 0)
            {
                return;
            }

            foreach (var key in this._items.Keys.Where(key => key.Scene == scene).ToArray())
            {
                this.Remove(key);
            }
        }

        /// <summary>Lets go of the scene items that used a source: OBS destroyed it, so what the events can still say about them does not matter any more.</summary>
        /// <param name="source">The source.</param>
        public void RemoveSource(Guid source)
        {
            if (!this._users.ContainsKey(source))
            {
                return;
            }

            foreach (var key in this._items.Where(item => item.Value == source).Select(item => item.Key).ToArray())
            {
                this.Remove(key);
            }
        }

        /// <summary>Lets go of everything.</summary>
        public void Clear()
        {
            this._items.Clear();
            this._users.Clear();
        }

        private void Release(Guid source)
        {
            if (this._users.TryGetValue(source, out var users))
            {
                if (users <= 1)
                {
                    this._users.Remove(source);
                }
                else
                {
                    this._users[source] = users - 1;
                }
            }
        }
    }
}
