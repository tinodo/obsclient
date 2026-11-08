namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// The scene items that a read found gone, whose removal the events have not told yet, and the sources that they used.
    /// </summary>
    /// <remarks>
    /// A read is ahead of the events. As far as the events go, a scene item that a read found gone is still there until the event that removes it
    /// arrives. The model does not count such a scene item among the ones that use a source, but it does not conclude from the count that OBS destroyed
    /// a source while one of them used it either: see <c>ModelEngine.HeldByRead</c>. The scene items that went with a scene that a read said OBS does not have
    /// are held too: the events about the sources that they showed (OBS removed one, and made another scene item that uses it) can still be on their way,
    /// and nothing is said of the scene items themselves. A scene item is let go of when its event arrives, when the source that it used is destroyed, when an
    /// event removes the scene that it was in (the removal of a scene item that came before arrived before that event), and after a time: the event does not
    /// always come (nothing is said of the scene items of a scene that OBS removed and keeps alive, or that went), and what the events could still tell arrives
    /// within seconds.
    /// <para>
    /// An event that lets go of the last scene item that is held for a source does not end the doubt about the source. The events that were on their way when the
    /// read was taken can still follow, among them the scene item that OBS made for the source meanwhile, and a read of a list says that OBS has the source, not
    /// who uses it. So for as long as the hold would have lasted the source is <em>settling</em> (<see cref="Settling"/>): what a read says is not judged by
    /// the count of the scene items that use it, and the time ends the doubt (<see cref="Prune"/>). Settling does not stop the decisions that are made at a place
    /// in the events (the removal of a scene item that the model knows): there the count is right.
    /// </para>
    /// </remarks>
    internal sealed class ReadReleasedItems
    {
        private readonly Dictionary<ItemKey, (Guid Source, TimeSpan At)> _items = new();
        private readonly Dictionary<Guid, int> _users = new();
        private readonly Dictionary<Guid, TimeSpan> _settling = new();

        /// <summary>Gets the number of scene items that are held.</summary>
        public int Count => this._items.Count;

        /// <summary>Gets the number of sources that are settling.</summary>
        public int SettlingCount => this._settling.Count;

        /// <summary>Holds a scene item that a read found gone.</summary>
        /// <param name="key">The scene item.</param>
        /// <param name="source">The source that it used.</param>
        /// <param name="now">The time.</param>
        public void Add(ItemKey key, Guid source, TimeSpan now)
        {
            if (this._items.TryGetValue(key, out var held))
            {
                this.Release(held.Source);
            }

            this._items[key] = (source, now);
            this._users[source] = this._users.GetValueOrDefault(source) + 1;
        }

        /// <summary>Lets go of a scene item because its removal arrived: the source that it used settles when no other scene item that is held used it.</summary>
        /// <param name="key">The scene item.</param>
        /// <returns>Whether it was held.</returns>
        public bool Remove(ItemKey key)
        {
            return this.RemoveOne(key, settle: true, out _);
        }

        /// <summary>Gets whether a scene item that is held used the source.</summary>
        /// <param name="source">The source.</param>
        /// <returns>True when one did.</returns>
        public bool Holds(Guid source)
        {
            return this._users.ContainsKey(source);
        }

        /// <summary>Gets whether the last scene item that was held for the source was let go of by an event, and the time for it is not up.</summary>
        /// <param name="source">The source.</param>
        /// <returns>True when the source is settling.</returns>
        public bool Settling(Guid source)
        {
            return this._settling.ContainsKey(source);
        }

        /// <summary>Lets go of the scene items of a scene because an event removed it: the sources that they used settle.</summary>
        /// <param name="scene">The scene.</param>
        public void RemoveScene(Guid scene)
        {
            if (this._items.Count == 0)
            {
                return;
            }

            foreach (var key in this._items.Keys.Where(key => key.Scene == scene).ToArray())
            {
                this.RemoveOne(key, settle: true, out _);
            }
        }

        /// <summary>Lets go of the scene items that used a source: OBS destroyed it, so what the events can still say about them does not matter any more.</summary>
        /// <param name="source">The source.</param>
        public void RemoveSource(Guid source)
        {
            this._settling.Remove(source);
            if (!this._users.ContainsKey(source))
            {
                return;
            }

            foreach (var key in this._items.Where(item => item.Value.Source == source).Select(item => item.Key).ToArray())
            {
                this.RemoveOne(key, settle: false, out _);
            }
        }

        /// <summary>Lets go of what was held, and of what was settling, since before a time.</summary>
        /// <param name="before">The time: a scene item that was held at a time before it goes, and a source whose last scene item was held at a time before it stops settling.</param>
        /// <param name="freed">Gets the sources that nothing is held for any more and that are not settling, when the caller wants them (the time is up for them).</param>
        /// <returns>The number of scene items that were let go of, and of sources that stopped settling.</returns>
        public int Prune(TimeSpan before, List<Guid>? freed = null)
        {
            if (this._items.Count == 0 && this._settling.Count == 0)
            {
                return 0;
            }

            var reported = freed is null ? null : new HashSet<Guid>(freed);
            var old = this._items.Where(item => item.Value.At < before).Select(item => item.Key).ToArray();
            var letGo = old.Length;
            foreach (var key in old)
            {
                this.RemoveOne(key, settle: false, out var source);
                if (source is { } uuid)
                {
                    // The time for the last scene item that is held for the source is up, and it is judged now: whatever settled it before is over too.
                    if (this._settling.Remove(uuid))
                    {
                        letGo++;
                    }

                    if (reported is not null && reported.Add(uuid))
                    {
                        freed!.Add(uuid);
                    }
                }
            }

            var expired = this._settling.Where(entry => entry.Value < before).Select(entry => entry.Key).ToArray();
            foreach (var uuid in expired)
            {
                this._settling.Remove(uuid);
                if (!this._users.ContainsKey(uuid) && reported is not null && reported.Add(uuid))
                {
                    freed!.Add(uuid);
                }
            }

            return letGo + expired.Length;
        }

        /// <summary>Lets go of everything.</summary>
        public void Clear()
        {
            this._items.Clear();
            this._users.Clear();
            this._settling.Clear();
        }

        // Lets go of a scene item. When that was the last scene item that is held for its source, the source is in freed, and it settles when an event let go of the
        // scene item (settle): for as long as the scene item was held, counted from the read that took it away.
        private bool RemoveOne(ItemKey key, bool settle, out Guid? freed)
        {
            freed = null;
            if (!this._items.Remove(key, out var held))
            {
                return false;
            }

            this.Release(held.Source);
            if (!this._users.ContainsKey(held.Source))
            {
                freed = held.Source;
                if (settle)
                {
                    this._settling[held.Source] = this._settling.TryGetValue(held.Source, out var at) && at > held.At ? at : held.At;
                }
            }

            return true;
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
