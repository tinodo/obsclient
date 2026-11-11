namespace OBSStudioClient.Model.Engine
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// A canvas of OBS Studio, as the model holds it.
    /// </summary>
    /// <param name="Name">The name of the canvas.</param>
    /// <param name="Uuid">The UUID of the canvas.</param>
    /// <param name="Flags">What the canvas is, and does.</param>
    internal sealed record CanvasInfo(string Name, Guid Uuid, ObsCanvasFlags Flags);

    /// <summary>
    /// The canvases of OBS Studio, in the order in which it lists them, as the model holds them.
    /// </summary>
    internal sealed class CanvasList : IEquatable<CanvasList>
    {
        public CanvasList(IEnumerable<CanvasInfo> canvases)
        {
            // A copy that nobody can change: it is shown to the application as it is.
            this.Canvases = new ReadOnlyCollection<CanvasInfo>([.. canvases]);
        }

        /// <summary>Gets the canvases, in the order of OBS Studio.</summary>
        public IReadOnlyList<CanvasInfo> Canvases { get; }

        /// <summary>Gets whether OBS Studio lists the canvas.</summary>
        /// <param name="uuid">The UUID of the canvas.</param>
        public bool Contains(Guid uuid)
        {
            return this.Canvases.Any(canvas => canvas.Uuid == uuid);
        }

        /// <summary>Gets the list without a canvas.</summary>
        /// <param name="uuid">The UUID of the canvas.</param>
        public CanvasList Without(Guid uuid)
        {
            return new CanvasList(this.Canvases.Where(canvas => canvas.Uuid != uuid));
        }

        /// <inheritdoc/>
        public bool Equals(CanvasList? other)
        {
            return other is not null && this.Canvases.SequenceEqual(other.Canvases);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return this.Equals(obj as CanvasList);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var canvas in this.Canvases)
            {
                hash.Add(canvas);
            }

            return hash.ToHashCode();
        }
    }
}
