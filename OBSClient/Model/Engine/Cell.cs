namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// A value of the model together with the numbers (see <c>ObsClient.LastIngress</c>) that tell how new it is: the number of the newest event that
    /// set it, and the number at which the newest read that set it was sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio does not say in which order its events and its responses were made, only in which order they arrived; and a read says how things were
    /// at some moment after it was sent. So an event is newer than a read when it arrived after the read was sent, and not otherwise: a read that
    /// was sent after an event arrived already shows what that event did (or what came after it). The cell keeps the two numbers apart, and so
    /// does not depend on the order in which the engine is handed the events and the results of reads.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the value.</typeparam>
    internal struct Cell<T>
    {
        /// <summary>The value.</summary>
        public T Value;

        /// <summary>The number of the newest event (or change that the model derived from an event) that set the value.</summary>
        public long EventSeq;

        /// <summary>The number that the newest read that set the value was sent at.</summary>
        public long ReadSeq;

        /// <summary>Makes a cell with a value that an event told, or that is a default that nothing told yet (number 0).</summary>
        public Cell(T value, long eventSeq)
        {
            this.Value = value;
            this.EventSeq = eventSeq;
            this.ReadSeq = 0;
        }

        /// <summary>Makes a cell with a value that a read returned.</summary>
        /// <param name="value">The value.</param>
        /// <param name="sentSeq">The number of the last message that had arrived when the read was sent.</param>
        public static Cell<T> FromRead(T value, long sentSeq)
        {
            return new Cell<T> { Value = value, EventSeq = 0, ReadSeq = sentSeq };
        }

        /// <summary>
        /// Applies the value of an event. Returns whether the value changed. An event that a read already covers (one that was sent after the event
        /// arrived: the numbers are equal or the read's is higher) changes nothing: the read is newer.
        /// </summary>
        public bool ApplyEvent(T value, long seq)
        {
            if (seq <= this.ReadSeq)
            {
                return false;
            }

            return this.Set(value, seq);
        }

        /// <summary>
        /// Applies a value that the model derives itself from a message: the position of an object that moved because another one came or went. It
        /// is newer than what the cell holds, whatever read the cell was set by, because the model knows what that message did.
        /// </summary>
        public bool ApplyDerived(T value, long seq)
        {
            return this.Set(value, seq);
        }

        /// <summary>
        /// Applies the value that a read returned. Returns whether the value changed; a read that an event overtook changes nothing, and neither does
        /// one that is older than a read that was applied.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="sentSeq">The number of the last message that had arrived when the read was sent.</param>
        public bool ApplyRead(T value, long sentSeq)
        {
            if (this.EventSeq > sentSeq || sentSeq < this.ReadSeq)
            {
                return false;
            }

            this.ReadSeq = sentSeq;
            if (EqualityComparer<T>.Default.Equals(this.Value, value))
            {
                return false;
            }

            this.Value = value;
            return true;
        }

        private bool Set(T value, long seq)
        {
            this.EventSeq = Math.Max(this.EventSeq, seq);
            if (EqualityComparer<T>.Default.Equals(this.Value, value))
            {
                return false;
            }

            this.Value = value;
            return true;
        }
    }
}
