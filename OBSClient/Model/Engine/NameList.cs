namespace OBSStudioClient.Model.Engine
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// A list of the names that OBS Studio keeps (its profiles, its scene collections), and the one that is current, as the model holds it: the order is the one that OBS Studio lists them in.
    /// </summary>
    internal sealed class NameList : IEquatable<NameList>
    {
        public NameList(string? current, IEnumerable<string> names)
        {
            this.Current = current;

            // A copy that nobody can change: it is shown to the application as it is.
            this.Names = new ReadOnlyCollection<string>([.. names]);
        }

        /// <summary>Gets the name of the current one, or null when OBS Studio did not say which.</summary>
        public string? Current { get; }

        /// <summary>Gets the names, in the order of OBS Studio.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>Gets whether OBS Studio lists the name.</summary>
        /// <param name="name">The name; the comparison is case sensitive, as OBS Studio's is.</param>
        public bool Contains(string name)
        {
            return this.Names.Contains(name, StringComparer.Ordinal);
        }

        /// <inheritdoc/>
        public bool Equals(NameList? other)
        {
            return other is not null
                && string.Equals(this.Current, other.Current, StringComparison.Ordinal)
                && this.Names.SequenceEqual(other.Names, StringComparer.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return this.Equals(obj as NameList);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(this.Current, StringComparer.Ordinal);
            foreach (var name in this.Names)
            {
                hash.Add(name, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
