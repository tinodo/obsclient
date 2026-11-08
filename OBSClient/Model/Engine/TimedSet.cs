namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// A set whose members are remembered for as long as they are needed: each has the time at which it was added, and the old ones can be let go of.
    /// </summary>
    /// <typeparam name="T">The type of the members.</typeparam>
    internal sealed class TimedSet<T>
        where T : notnull
    {
        private readonly Dictionary<T, TimeSpan> _members = new();

        /// <summary>Gets the number of members.</summary>
        public int Count => this._members.Count;

        /// <summary>Gets whether the set has the member.</summary>
        /// <param name="member">The member.</param>
        /// <returns>True when it has.</returns>
        public bool Contains(T member)
        {
            return this._members.ContainsKey(member);
        }

        /// <summary>Adds a member, or renews it when the set has it already: it is remembered from now on.</summary>
        /// <param name="member">The member.</param>
        /// <param name="now">The time.</param>
        public void Add(T member, TimeSpan now)
        {
            this._members[member] = now;
        }

        /// <summary>Removes a member.</summary>
        /// <param name="member">The member.</param>
        /// <returns>Whether the set had it.</returns>
        public bool Remove(T member)
        {
            return this._members.Remove(member);
        }

        /// <summary>Removes all members.</summary>
        public void Clear()
        {
            this._members.Clear();
        }

        /// <summary>Lets go of the members that were added before a time.</summary>
        /// <param name="before">The time: a member that was added at a time before it goes. One that was added at it stays.</param>
        /// <returns>The number of members that were let go of.</returns>
        public int Prune(TimeSpan before)
        {
            List<T>? old = null;
            foreach (var member in this._members)
            {
                if (member.Value < before)
                {
                    (old ??= []).Add(member.Key);
                }
            }

            if (old is null)
            {
                return 0;
            }

            foreach (var member in old)
            {
                this._members.Remove(member);
            }

            return old.Count;
        }
    }
}
