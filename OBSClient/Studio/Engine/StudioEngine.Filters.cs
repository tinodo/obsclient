namespace OBSStudioClient.Studio.Engine
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    // The filters of the scenes and the inputs. OBS gives a filter no UUID: it is found by its name within its source, and the events say the name of the source and not its UUID.
    // What the events say with certainty is applied at once (a filter that was enabled, a filter that was removed or renamed); a filter that was created, and the order of the filters,
    // are read, because the events do not say all that the model needs (the state of a new filter) or say it so that a read that was sent before them can undo them.
    internal sealed partial class StudioEngine
    {
        // How many times in a row OBS may say that it does not have a source that the model holds, when the filters of the source are read, before the model leaves it at that: the
        // removal of the source decides what becomes of it, and an answer that does not change must not keep the model from settling.
        private const int FiltersNotFoundAttempts = 3;

        private SourceNode? SourceByUuid(Guid uuid)
        {
            if (this._scenes.TryGetValue(uuid, out var scene))
            {
                return scene;
            }

            return this._inputs.TryGetValue(uuid, out var input) ? input : null;
        }

        // The sources that have a name, for the events of the filters, which say the name of the source only. Scenes and inputs share the namespace of OBS Studio from 31.1 (and
        // obs-websocket 5.7), and not before: when both have the name, both are looked at.
        private IEnumerable<SourceNode> SourcesNamed(string name)
        {
            foreach (var scene in this._scenes.Values)
            {
                if (scene.State != SourceState.Gone && string.Equals(scene.Name.Value, name, StringComparison.Ordinal))
                {
                    yield return scene;
                }
            }

            foreach (var input in this._inputs.Values)
            {
                if (input.State != SourceState.Gone && string.Equals(input.Name.Value, name, StringComparison.Ordinal))
                {
                    yield return input;
                }
            }
        }

        private void ApplyFiltersResponse(ReadTicket ticket, ReadRequest request, RequestResponseMessage? response, bool ok, bool notFound)
        {
            var source = this.SourceByUuid(request.Target);
            if (source is null)
            {
                // The source is gone: nothing to apply.
                return;
            }

            var scope = source.FiltersScope;
            if (ok && response!.ResponseData is FiltersResponse list)
            {
                scope.Attempts = 0;
                if (ticket.SentSeq < source.FiltersEventSeq)
                {
                    // An event has created, removed, renamed or moved a filter since the read was sent: it can list what the event took away, or by the name that the event changed. The
                    // event marked the part, so it is read again; this read is not applied.
                    scope.MarkDirty();
                    return;
                }

                this.ApplyFilters(source, list.Filters, ticket.SentSeq);
            }
            else if (notFound)
            {
                // OBS does not have the source (its removal may not have arrived yet): the lists decide what becomes of the source, and the filters are read again after a wait, a few
                // times, for as long as the source is there.
                if (source.State == SourceState.Live && scope.Attempts < FiltersNotFoundAttempts)
                {
                    (source is SceneNode ? this._scenesScope : this._inputsScope).MarkDirty();
                    this.Retry(scope);
                }
                else
                {
                    scope.Attempts = 0;
                }
            }
            else if (response is { RequestStatus.Result: false } && response.RequestStatus.Code != RequestStatusCode.NotReady)
            {
                // OBS Studio refuses the request for good (a simulation that does not know it): there are no filters to read.
                scope.Attempts = 0;
            }
            else
            {
                this.Retry(scope);
            }
        }

        // What a read says about the filters of a source that no event has changed since it was sent: the filters that are listed are in the model, and the ones that are not
        // listed are not. A value that an event changed after the read was sent is the event's (the cells see to that).
        private void ApplyFilters(SourceNode source, Filter[] filters, long sentSeq)
        {
            var listed = new HashSet<FilterNode>();
            foreach (var filter in filters.OrderBy(f => f.FilterIndex))
            {
                var node = source.FindFilter(filter.FilterName);
                if (node is null)
                {
                    node = new FilterNode(++this._nextFilterId, filter.FilterName, filter.FilterKind, filter.FilterIndex, filter.FilterEnabled, sentSeq);
                    source.Filters.Add(node);
                    this.Emit(StudioChangeKind.FilterAdded, source.Uuid, 0, filter.FilterName);
                }
                else
                {
                    node.Kind = filter.FilterKind;
                    var oldIndex = node.Index.Value;
                    if (node.Index.ApplyRead(filter.FilterIndex, sentSeq))
                    {
                        this.Emit(StudioChangeKind.FilterIndexChanged, source.Uuid, 0, node.Name.Value, oldIndex, node.Index.Value);
                    }

                    var oldEnabled = node.Enabled.Value;
                    if (node.Enabled.ApplyRead(filter.FilterEnabled, sentSeq))
                    {
                        this.Emit(StudioChangeKind.FilterEnabledChanged, source.Uuid, 0, node.Name.Value, oldEnabled, node.Enabled.Value);
                    }
                }

                listed.Add(node);
            }

            foreach (var node in source.Filters.ToArray())
            {
                if (!listed.Contains(node))
                {
                    source.Filters.Remove(node);
                    this.Emit(StudioChangeKind.FilterRemoved, source.Uuid, 0, node.Name.Value);
                }
            }
        }

        // A filter was created: it has no state that the event says (it is enabled when it is made, but the model does not take that for granted), so the filters are read.
        private void OnFilterCreated(SourceFilterCreatedEventArgs e)
        {
            this.FiltersChanged(e.SourceName);
        }

        private void OnFilterRemoved(SourceFilterRemovedEventArgs e)
        {
            foreach (var source in this.SourcesNamed(e.SourceName))
            {
                var node = source.FindFilter(e.FilterName);
                if (node is not null)
                {
                    source.Filters.Remove(node);
                    this.Emit(StudioChangeKind.FilterRemoved, source.Uuid, 0, node.Name.Value);

                    // The filters behind it move up by one.
                    foreach (var other in source.Filters)
                    {
                        if (other.Index.Value > node.Index.Value)
                        {
                            other.Index.ApplyDerived(other.Index.Value - 1, this._seq);
                        }
                    }
                }

                this.MarkFilters(source);
            }
        }

        private void OnFilterRenamed(SourceFilterNameChangedEventArgs e)
        {
            foreach (var source in this.SourcesNamed(e.SourceName))
            {
                var node = source.FindFilter(e.OldFilterName);
                if (node is not null)
                {
                    var old = node.Name.Value;
                    if (node.Name.ApplyEvent(e.FilterName, this._seq))
                    {
                        this.Emit(StudioChangeKind.FilterRenamed, source.Uuid, 0, node.Name.Value, old, node.Name.Value);
                    }
                }

                this.MarkFilters(source);
            }
        }

        private void OnFilterEnabledChanged(SourceFilterEnableStateChangedEventArgs e)
        {
            var sources = this.SourcesNamed(e.SourceName).ToArray();
            if (sources.Length > 1)
            {
                // A scene and an input have the name, and the event does not say which of them: both are read.
                foreach (var shared in sources)
                {
                    this.MarkFilters(shared);
                }

                return;
            }

            foreach (var source in sources)
            {
                var node = source.FindFilter(e.FilterName);
                if (node is null)
                {
                    // A filter that the model does not hold yet (its creation is being read): the read says whether it is enabled.
                    source.FiltersScope.MarkDirty();
                    continue;
                }

                var old = node.Enabled.Value;
                if (node.Enabled.ApplyEvent(e.FilterEnabled, this._seq))
                {
                    this.Emit(StudioChangeKind.FilterEnabledChanged, source.Uuid, 0, node.Name.Value, old, node.Enabled.Value);
                }
            }
        }

        // The filters of a source were put in another order. The event lists them all, so the positions are taken from it; what it lists that the model does not hold, and what
        // the model holds that it does not list, are for the read that follows.
        private void OnFiltersReindexed(SourceFiltersEventArgs e)
        {
            foreach (var source in this.SourcesNamed(e.SourceName))
            {
                foreach (var listed in e.Filters)
                {
                    var node = source.FindFilter(listed.FilterName);
                    if (node is not null)
                    {
                        var old = node.Index.Value;
                        if (node.Index.ApplyEvent(listed.FilterIndex, this._seq))
                        {
                            this.Emit(StudioChangeKind.FilterIndexChanged, source.Uuid, 0, node.Name.Value, old, node.Index.Value);
                        }
                    }
                }

                this.MarkFilters(source);
            }
        }

        private void FiltersChanged(string sourceName)
        {
            foreach (var source in this.SourcesNamed(sourceName))
            {
                this.MarkFilters(source);
            }
        }

        // An event changed the structure of the filters of a source: the read that is under way (if any) was sent before it, and a new one is wanted.
        private void MarkFilters(SourceNode source)
        {
            source.FiltersEventSeq = this._seq;
            source.FiltersScope.MarkDirty();
        }
    }
}
