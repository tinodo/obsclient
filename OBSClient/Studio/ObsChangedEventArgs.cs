namespace OBSStudioClient.Studio
{
    /// <summary>
    /// Describes one change of the model of OBS Studio that an <see cref="ObsStudio"/> keeps.
    /// </summary>
    /// <remarks>
    /// The changes are raised in the order in which they happened, one at a time. When a handler runs, the objects can already show a state
    /// that is newer than the change that it handles: the properties are always current, a change is a notification.
    /// </remarks>
    public sealed class ObsChangedEventArgs : EventArgs
    {
        internal ObsChangedEventArgs(ObsChangeKind kind, object? entity, string? propertyName = null, object? oldValue = null, object? newValue = null)
        {
            this.Kind = kind;
            this.Entity = entity;
            this.PropertyName = propertyName;
            this.OldValue = oldValue;
            this.NewValue = newValue;
        }

        /// <summary>
        /// Gets what kind of change it is.
        /// </summary>
        public ObsChangeKind Kind { get; }

        /// <summary>
        /// Gets the <see cref="ObsScene"/>, <see cref="ObsInput"/> or <see cref="ObsSceneItem"/> that changed, or null for a change of the <see cref="ObsStudio"/> itself.
        /// </summary>
        public object? Entity { get; }

        /// <summary>
        /// Gets the name of the property that changed, for <see cref="ObsChangeKind.PropertyChanged"/>.
        /// </summary>
        public string? PropertyName { get; }

        /// <summary>
        /// Gets the value that the property had, when it is known.
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// Gets the value that the property has now.
        /// </summary>
        public object? NewValue { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            var what = this.Entity switch
            {
                ObsSceneItem item => $"scene item {item.Id} of '{item.Scene.Name}'",
                ObsSource source => $"{(source is ObsScene ? "scene" : "input")} '{source.Name}'",
                _ => "the studio",
            };
            return this.Kind == ObsChangeKind.PropertyChanged ? $"{what}: {this.PropertyName} {Show(this.OldValue)} -> {Show(this.NewValue)}" : $"{this.Kind}: {what}";
        }

        // A list is shown by what is in it: the name of its type tells nothing.
        private static string Show(object? value)
        {
            return value switch
            {
                null => "(nothing)",
                IEnumerable<ObsSource> sources => $"[{string.Join(", ", sources.Select(source => source.Name))}]",
                IEnumerable<ObsSceneItem> items => $"[{string.Join(", ", items.Select(item => item.Source.Name))}]",
                _ => value.ToString() ?? string.Empty,
            };
        }
    }
}
