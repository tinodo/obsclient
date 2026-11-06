namespace OBSStudioClient.Studio
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Keeps the PropertyChanged notifications of the steps in which the model is not complete (it is being read), so that they are raised once when it is.
    /// </summary>
    /// <remarks>
    /// While the model is read, the objects that the application holds change (the value of an input that was muted in the meantime, the name of a scene,
    /// the state of an object that OBS Studio removed): an application that is bound to them has to be told, and it is told once the model is complete,
    /// once for each property of each object, whatever the number of steps that changed it. The records for <see cref="ObsStudio.Changed"/> are not kept:
    /// <see cref="ObsStudio.Resynchronized"/> says that everything may have changed.
    /// </remarks>
    internal sealed class StudioNoticeHold
    {
        private readonly List<(object Entity, string Property)> _held = new();
        private readonly HashSet<(object Entity, string Property)> _seen = new(Comparer.Instance);

        /// <summary>Gets the number of properties that are held.</summary>
        public int Count => this._held.Count;

        /// <summary>Keeps the notifications of a step that are about a property of an object. The others are dropped.</summary>
        /// <param name="notices">The notifications of the step.</param>
        public void Hold(IReadOnlyList<StudioNotice> notices)
        {
            foreach (var notice in notices)
            {
                if (notice.Entity is { } entity && notice.Property is { } property && this._seen.Add((entity, property)))
                {
                    this._held.Add((entity, property));
                }
            }
        }

        /// <summary>Hands over what is held, in the order in which it was first held, and forgets it.</summary>
        /// <param name="raise">Receives a notification of a property, without a record for <see cref="ObsStudio.Changed"/>.</param>
        /// <param name="onlyStudio">Whether only the properties of the <see cref="ObsStudio"/> itself are handed over.</param>
        public void Release(Action<StudioNotice> raise, bool onlyStudio)
        {
            foreach (var (entity, property) in this._held)
            {
                if (!onlyStudio || entity is ObsStudio)
                {
                    raise(new StudioNotice(null, entity, property));
                }
            }

            this._held.Clear();
            this._seen.Clear();
        }

        private sealed class Comparer : IEqualityComparer<(object Entity, string Property)>
        {
            public static readonly Comparer Instance = new();

            public bool Equals((object Entity, string Property) x, (object Entity, string Property) y)
            {
                return ReferenceEquals(x.Entity, y.Entity) && string.Equals(x.Property, y.Property, StringComparison.Ordinal);
            }

            public int GetHashCode((object Entity, string Property) obj)
            {
                return HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Entity), obj.Property);
            }
        }
    }
}
