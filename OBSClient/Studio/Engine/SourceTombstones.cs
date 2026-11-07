namespace OBSStudioClient.Studio.Engine
{
    /// <summary>
    /// The kind of source that a tombstone is about: the list that says whether OBS has done with it is the list of that kind.
    /// </summary>
    internal enum SourceKind
    {
        /// <summary>An input: the list of inputs says.</summary>
        Input,

        /// <summary>A scene: the list of scenes says.</summary>
        Scene,
    }

    /// <summary>
    /// The sources that OBS destroyed (or that it removed before the model knew them), which the model must not take for live again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A message that is older than the removal (a read that was sent before it, the second copy of a creation event, a list that lags) can show the source,
    /// and it must not bring it back. OBS goes on listing a source that it removed and keeps alive for something that nothing shows (a global audio input
    /// that is assigned to a channel), and nothing tells that from one that it destroyed: for such a source the tombstone is what keeps it from coming
    /// back, for as long as OBS keeps it.
    /// </para>
    /// <para>
    /// So a tombstone is not let go of for its age. It is let go of for a time after the list of its kind, read after the tombstone was made, did not have
    /// the source (OBS is done with it, and a source does not come back: the UUID is not used again). The messages that are late arrive within that time.
    /// When no list is read for another reason, a tombstone that is old gets one read (see <see cref="NeedsConfirmation"/>), so what is remembered is what
    /// OBS keeps and what was removed lately, and not what was removed since the connection began.
    /// </para>
    /// </remarks>
    internal sealed class SourceTombstones
    {
        private readonly Dictionary<Guid, Entry> _entries = new();

        /// <summary>Gets the number of tombstones.</summary>
        public int Count => this._entries.Count;

        /// <summary>Gets whether there is a tombstone for the source.</summary>
        /// <param name="uuid">The UUID of the source.</param>
        /// <returns>True when there is.</returns>
        public bool Contains(Guid uuid)
        {
            return this._entries.ContainsKey(uuid);
        }

        /// <summary>Makes a tombstone for a source, or renews it.</summary>
        /// <param name="uuid">The UUID of the source.</param>
        /// <param name="kind">The kind of the source.</param>
        /// <param name="seq">The number of the message that the removal is known by (the one that told it, or the one that a read that decided it was sent at).</param>
        /// <param name="now">The time.</param>
        public void Add(Guid uuid, SourceKind kind, long seq, TimeSpan now)
        {
            this._entries[uuid] = new Entry(kind, seq, now);
        }

        /// <summary>Removes the tombstone of a source.</summary>
        /// <param name="uuid">The UUID of the source.</param>
        /// <returns>Whether there was one.</returns>
        public bool Remove(Guid uuid)
        {
            return this._entries.Remove(uuid);
        }

        /// <summary>Removes all tombstones.</summary>
        public void Clear()
        {
            this._entries.Clear();
        }

        /// <summary>
        /// A list of one kind has been read: it says which of the tombstones of that kind OBS is done with. A tombstone that was made before the read was sent
        /// and whose source the list does not have is confirmed (OBS has done with the source, and a UUID is not used again while the scene collection stays
        /// loaded); one whose source the list has is not (it is kept alive, or the list lags behind the removal).
        /// </summary>
        /// <param name="kind">The kind of the list.</param>
        /// <param name="listed">The sources that the list has, those that were removed too.</param>
        /// <param name="sentSeq">The number of the last message that had arrived when the read was sent.</param>
        /// <param name="now">The time.</param>
        public void ApplyList(SourceKind kind, HashSet<Guid> listed, long sentSeq, TimeSpan now)
        {
            foreach (var pair in this._entries)
            {
                var entry = pair.Value;
                if (entry.Kind != kind || entry.Seq > sentSeq)
                {
                    // Made after the read was sent: it says nothing about this one.
                    continue;
                }

                if (listed.Contains(pair.Key))
                {
                    entry.ConfirmedAt = null;
                    entry.CheckedAt = now;
                }
                else
                {
                    entry.ConfirmedAt ??= now;
                }
            }
        }

        /// <summary>
        /// Gets whether a list of one kind should be read to confirm the tombstones that were made long ago and that no list confirmed, and that no list
        /// was checked against for as long.
        /// </summary>
        /// <param name="kind">The kind of the list.</param>
        /// <param name="before">The time: a tombstone that was made, or checked against a list, at a time before it is long ago.</param>
        /// <returns>True when a list should be read.</returns>
        public bool NeedsConfirmation(SourceKind kind, TimeSpan before)
        {
            foreach (var entry in this._entries.Values)
            {
                if (entry.Kind == kind && entry.ConfirmedAt is null && entry.CheckedAt < before)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Lets go of the tombstones that were confirmed before a time.</summary>
        /// <param name="before">The time: a tombstone that was confirmed at a time before it goes.</param>
        /// <returns>The number of tombstones that were let go of.</returns>
        public int Prune(TimeSpan before)
        {
            List<Guid>? old = null;
            foreach (var pair in this._entries)
            {
                if (pair.Value.ConfirmedAt is { } confirmed && confirmed < before)
                {
                    (old ??= []).Add(pair.Key);
                }
            }

            if (old is null)
            {
                return 0;
            }

            foreach (var uuid in old)
            {
                this._entries.Remove(uuid);
            }

            return old.Count;
        }

        private sealed class Entry(SourceKind kind, long seq, TimeSpan now)
        {
            public SourceKind Kind { get; } = kind;

            public long Seq { get; } = seq;

            public TimeSpan? ConfirmedAt { get; set; }

            // When the tombstone was made, or a list that had the source was read last.
            public TimeSpan CheckedAt { get; set; } = now;
        }
    }
}
