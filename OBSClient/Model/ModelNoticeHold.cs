namespace OBSStudioClient.Model
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Keeps the PropertyChanged notifications of the steps in which the model is not complete (it is being read), so that they are raised once when it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While the model is read, the objects that the application holds change (the value of an input that was muted in the meantime, the name of a scene,
    /// the state of an object that OBS Studio removed): an application that is bound to them has to be told, and it is told once the model is complete,
    /// once for each property of each object, whatever the number of steps that changed it. The records for <see cref="ObsModel.Changed"/> are not kept:
    /// <see cref="ObsModel.Resynchronized"/> says that everything may have changed.
    /// </para>
    /// <para>
    /// What is held is bounded, as the notifications that wait for the handlers are (<see cref="ObsModelOptions.MaxPendingNotifications"/>): a model that
    /// cannot be read (the reads fail, again and again) while OBS Studio goes on making and removing objects would otherwise hold every one of them, through the
    /// properties that changed, for as long as it takes. When more would be held, nothing is kept (and what was held is let go of), and the release says so:
    /// the application is then told that everything may have changed, which covers what was not kept.
    /// </para>
    /// </remarks>
    internal sealed class ModelNoticeHold
    {
        private readonly int _capacity;
        private readonly List<(object Entity, string Property)> _held = new();
        private readonly HashSet<(object Entity, string Property)> _seen = new(Comparer.Instance);
        private bool _overflowed;

        /// <summary>Initializes a new instance of the <see cref="ModelNoticeHold"/> class.</summary>
        /// <param name="capacity">The number of properties that may be held.</param>
        public ModelNoticeHold(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            this._capacity = capacity;
        }

        /// <summary>Gets the number of properties that are held.</summary>
        public int Count => this._held.Count;

        /// <summary>Gets whether more properties changed than may be held: none are kept, and the release says so.</summary>
        public bool Overflowed => this._overflowed;

        /// <summary>Keeps the notifications of a step that are about a property of an object. The others are dropped.</summary>
        /// <param name="notices">The notifications of the step.</param>
        public void Hold(IReadOnlyList<ModelNotice> notices)
        {
            if (this._overflowed)
            {
                return;
            }

            foreach (var notice in notices)
            {
                // What is told at once is not held (see ModelNotice.Immediate).
                if (!notice.Immediate && notice.Entity is { } entity && notice.Property is { } property && this._seen.Add((entity, property)))
                {
                    if (this._held.Count >= this._capacity)
                    {
                        // What is not kept is covered by what the release tells; and what was kept is let go of, with the objects that it refers to.
                        this._overflowed = true;
                        this._held.Clear();
                        this._seen.Clear();
                        return;
                    }

                    this._held.Add((entity, property));
                }
            }
        }

        /// <summary>Hands over what is held, in the order in which it was first held, and forgets it.</summary>
        /// <param name="raise">Receives a notification of a property, without a record for <see cref="ObsModel.Changed"/>.</param>
        /// <param name="onlyModel">Whether only the properties of the <see cref="ObsModel"/> itself are handed over.</param>
        /// <returns>True when more changed than could be held: nothing was handed over, and the application has to be told that everything may have changed.</returns>
        public bool Release(Action<ModelNotice> raise, bool onlyModel)
        {
            var overflowed = this._overflowed;
            if (!overflowed)
            {
                foreach (var (entity, property) in this._held)
                {
                    if (!onlyModel || entity is ObsModel or ObsOutput or ObsTransitions or ObsProfiles or ObsSceneCollections or ObsCanvases)
                    {
                        raise(new ModelNotice(null, entity, property));
                    }
                }
            }

            this.Clear();
            return overflowed;
        }

        /// <summary>Forgets what is held, without handing it over: the model has stopped, and nothing will raise it.</summary>
        public void Clear()
        {
            this._held.Clear();
            this._seen.Clear();
            this._overflowed = false;
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
