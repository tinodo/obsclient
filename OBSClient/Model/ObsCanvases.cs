namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// The canvases of OBS Studio: the main canvas, and the ones that plugins made (or that a scene collection saved).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same object for as long as the <see cref="ObsModel"/> lives (<see cref="ObsModel.Canvases"/>), and says with <see cref="INotifyPropertyChanged"/> when <see cref="All"/> and <see cref="Main"/> change:
    /// when a canvas is made or removed, or renamed (OBS Studio says so with an event for each), and when OBS Studio changes its scene collection (a scene collection saves its canvases, so the list is read again
    /// with the rest). The canvases are <see cref="ObsCanvas"/> snapshots: the list is another one when anything about the canvases changed, and a canvas that was renamed is a new object with the same
    /// <see cref="ObsCanvas.Uuid"/>.
    /// </para>
    /// <para>
    /// The list is empty until OBS Studio has said, and for an OBS Studio whose obs-websocket does not know canvases (before 5.7.0, see <see cref="ObsModel.WebSocketVersion"/>).
    /// </para>
    /// </remarks>
    public sealed class ObsCanvases : INotifyPropertyChanged
    {
        private static readonly State Empty = new(null, [], Array.AsReadOnly(Array.Empty<ObsCanvas>()));

        private readonly ObsModel _model;
        private State _state = Empty;

        internal ObsCanvases(ObsModel model)
        {
            this._model = model;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the canvases, in the order in which OBS Studio lists them. It is another list when the canvases change, and it is empty until OBS Studio has said.
        /// </summary>
        public IReadOnlyList<ObsCanvas> All => Volatile.Read(ref this._state).View;

        /// <summary>
        /// Gets the main canvas, or null until OBS Studio has said (and for an OBS Studio that does not know canvases).
        /// </summary>
        public ObsCanvas? Main => Array.Find(Volatile.Read(ref this._state).Items, canvas => canvas.IsMain);

        /// <summary>
        /// Gets the canvas with a name.
        /// </summary>
        /// <param name="name">The name. Names are case sensitive, as they are in OBS Studio (see <see cref="ObsModelOptions.IgnoreCase"/>).</param>
        /// <exception cref="ObsNotFoundException">There is no canvas with that name. The exception suggests names that are close.</exception>
        /// <exception cref="ObsAmbiguousNameException">More than one canvas matches (only possible when case is ignored).</exception>
        public ObsCanvas this[string name]
        {
            get
            {
                var items = Volatile.Read(ref this._state).Items;
                var ignoreCase = this._model.IgnoreCase;
                var count = NameLookup.Count(items, NameOf, name, ignoreCase, out var first);
                return count == 1 ? first! : throw NameLookup.Failure(items, NameOf, Describe, "canvas", name, count, ignoreCase);
            }
        }

        /// <summary>
        /// Gets the canvas with a UUID.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <exception cref="ObsNotFoundException">There is no canvas with that UUID.</exception>
        public ObsCanvas this[Guid uuid] => this.TryGet(uuid, out var found) ? found : throw new ObsNotFoundException("canvas", uuid.ToString(), []);

        /// <summary>
        /// Gets the canvas with a name, if there is one.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="canvas">The canvas.</param>
        /// <returns>Whether there is a canvas with the name.</returns>
        /// <exception cref="ObsAmbiguousNameException">More than one canvas matches (only possible when case is ignored).</exception>
        public bool TryGet(string name, [MaybeNullWhen(false)] out ObsCanvas canvas)
        {
            var items = Volatile.Read(ref this._state).Items;
            var ignoreCase = this._model.IgnoreCase;
            var count = NameLookup.Count(items, NameOf, name, ignoreCase, out var first);
            if (count > 1)
            {
                throw NameLookup.Failure(items, NameOf, Describe, "canvas", name, count, ignoreCase);
            }

            canvas = first;
            return count == 1;
        }

        /// <summary>
        /// Gets the canvas with a UUID, if there is one.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <param name="canvas">The canvas.</param>
        /// <returns>Whether there is a canvas with the UUID.</returns>
        public bool TryGet(Guid uuid, [MaybeNullWhen(false)] out ObsCanvas canvas)
        {
            canvas = Array.Find(Volatile.Read(ref this._state).Items, candidate => candidate.Uuid == uuid);
            return canvas is not null;
        }

        // What the model holds is brought in line here, and not through the view: the objects stay the same, so a canvas that did not change keeps its snapshot.
        internal bool Set(CanvasList? list, out IReadOnlyList<ObsCanvas> before, out ObsCanvas? mainBefore)
        {
            var state = Volatile.Read(ref this._state);
            before = state.View;
            mainBefore = Array.Find(state.Items, canvas => canvas.IsMain);
            if (state.List is null ? list is null : list is not null && state.List.Equals(list))
            {
                return false;
            }

            var items = list is null
                ? []
                : list.Canvases.Select(info => Array.Find(state.Items, held => held.Uuid == info.Uuid && held.Name == info.Name && held.Flags == info.Flags) ?? new ObsCanvas(this._model, info.Name, info.Uuid, info.Flags)).ToArray();
            Volatile.Write(ref this._state, list is null ? Empty : new State(list, items, Array.AsReadOnly(items)));
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this._model.Raise(this.PropertyChanged, this, propertyName);
        }

        private static string NameOf(ObsCanvas canvas)
        {
            return canvas.Name;
        }

        private static string Describe(ObsCanvas canvas)
        {
            return $"canvas '{canvas.Name}' ({canvas.Uuid})";
        }

        private sealed record State(CanvasList? List, ObsCanvas[] Items, IReadOnlyList<ObsCanvas> View);
    }
}
