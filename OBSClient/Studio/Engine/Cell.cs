namespace OBSStudioClient.Studio.Engine
{
    /// <summary>
    /// A value of the model together with the number of the last message (see <c>ObsClient.LastIngress</c>) that told us the value.
    /// </summary>
    /// <remarks>
    /// An event is applied when it is processed, and events are processed in the order of their numbers, so the value of an event is
    /// always newer than what the cell holds. A read result is applied only when no event told the cell something after the read was
    /// sent: an event beats a read that was sent before it.
    /// </remarks>
    /// <typeparam name="T">The type of the value.</typeparam>
    internal struct Cell<T>
    {
        /// <summary>The value.</summary>
        public T Value;

        /// <summary>The number of the message that told the value, or the number that the read that told it was sent at.</summary>
        public long Seq;

        public Cell(T value, long seq)
        {
            this.Value = value;
            this.Seq = seq;
        }

        /// <summary>Applies the value of an event. Returns whether the value changed.</summary>
        public bool ApplyEvent(T value, long seq)
        {
            this.Seq = Math.Max(this.Seq, seq);
            if (EqualityComparer<T>.Default.Equals(this.Value, value))
            {
                return false;
            }

            this.Value = value;
            return true;
        }

        /// <summary>Applies the value that a read returned. Returns whether the value changed; a read that an event overtook changes nothing.</summary>
        /// <param name="value">The value.</param>
        /// <param name="sentSeq">The number of the last message that had arrived when the read was sent.</param>
        public bool ApplyRead(T value, long sentSeq)
        {
            if (this.Seq > sentSeq)
            {
                return false;
            }

            this.Seq = sentSeq;
            if (EqualityComparer<T>.Default.Equals(this.Value, value))
            {
                return false;
            }

            this.Value = value;
            return true;
        }
    }
}
