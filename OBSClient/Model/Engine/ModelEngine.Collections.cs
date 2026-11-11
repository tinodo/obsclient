namespace OBSStudioClient.Model.Engine
{
    internal sealed partial class ModelEngine
    {
        /// <summary>
        /// Gets whether OBS Studio has changed its scene collection since the read was taken: its answer is about a collection that the model no longer holds, so it will not
        /// be applied, and nobody need wait for it.
        /// </summary>
        /// <param name="ticket">The read.</param>
        public bool IsOutdated(ReadTicket ticket)
        {
            return ticket.Epoch != this._epoch;
        }

        // OBS Studio begins to change its scene collection (CurrentSceneCollectionChanging, which names the collection that it leaves). From now until it says that it has done so, it answers every request with 207 and
        // sends nothing (obs-websocket 5.4.0 and later), and what it does send is not about the collection that the model will hold. So nothing is asked and nothing is
        // taken from the events, and the answer to a read that is under way is not applied (Owns). The model keeps what it holds, for the application to read as the last that
        // was known, and says that it is being read (the state): it is of the collection that OBS Studio is leaving.
        private void OnCollectionChanging(string? left)
        {
            if (this._switchShownByRead is { } shown)
            {
                // The event of a switch that a read showed already comes late: the model is of the collection that it went to, and has nothing to wait for.
                if (left is not null && string.Equals(left, shown.From, StringComparison.Ordinal))
                {
                    return;
                }

                this._switchShownByRead = null;
            }

            if (this._switching)
            {
                return;
            }

            this._switching = true;
            this._state = ModelEngineState.Synchronizing;
        }

        // The event that says that OBS Studio has changed its scene collection (CurrentSceneCollectionChanged, which names the collection that it loaded). It is the end of the switch that
        // CurrentSceneCollectionChanging began, unless a read showed the switch before the events came and this is the event of that one: the model is of the collection already.
        private void OnCollectionChangedEvent(string? loaded)
        {
            if (this._switchShownByRead is { } shown)
            {
                this._switchShownByRead = null;
                if (loaded is not null && string.Equals(loaded, shown.To, StringComparison.Ordinal))
                {
                    return;
                }
            }

            this.OnCollectionChanged();
        }

        // OBS Studio has changed its scene collection (CurrentSceneCollectionChanged): this is the end of the switch, and it is also what tells the model when the event that
        // begins one was not seen (the connection was made in the middle of it, or the event was lost). The collection that the model held is not loaded any more, and the
        // one that is, is read as the first on a connection is: the scenes and inputs of the old one are gone (the application is told so, by the objects that it holds), and
        // nothing of it is merged into the new one. A scene with the same UUID in both is the same scene when the collection is loaded again, but it is a new object.
        private void OnCollectionChanged()
        {
            this.ForgetCollection();
            this._state = ModelEngineState.Synchronizing;

            // Studio mode is not a part of the collection, but it is read again with the rest.
            foreach (var scope in this.AllScopes())
            {
                scope.InFlight = false;
                scope.Attempts = 0;
                scope.NotBefore = TimeSpan.Zero;
                scope.Dirty = true;
            }

            this._previewScope.Dirty = false;
        }

        // What an answer to the list of the scene collections says about the collection that is loaded. The first answer after a connection or a switch says which collection the scenes and the inputs of the
        // model are of. An answer that names another collection later is a switch that the events did not tell of (they were lost, or came late): the model holds the collection that OBS Studio left, and
        // what it holds is read as it is after the event that says that OBS Studio changed its collection (OnCollectionChanged), so that the model is not taken for complete (and a call that waits for the
        // switch is not done) before it is of the collection that is loaded. A collection that was renamed is the same collection, and nothing is read again for it: its old name is not listed any more,
        // and the name that it has now was not listed before.
        private void NoticeSwitch(NameList? held, NameList read)
        {
            if (read.Current is not { } current)
            {
                return;
            }

            var loaded = this._loadedCollection;
            this._loadedCollection = current;
            if (loaded is null || string.Equals(loaded, current, StringComparison.Ordinal))
            {
                return;
            }

            if (!read.Contains(loaded) && held is not null && !held.Contains(current))
            {
                return;
            }

            this.OnCollectionChanged();

            // The events of this switch are about it, and are not a second one when they come.
            this._switchShownByRead = (loaded, current);
        }

        // The model of the collection that OBS Studio has left is dropped, and the model is of the next collection: the reads that were taken for the old one are outdated.
        // It is also what a new connection does when the one before it ended in the middle of a switch (OnSessionStarted): OBS Studio has loaded a collection since, maybe the
        // one that the model holds (the same UUIDs), and its sources are new ones all the same.
        private void ForgetCollection()
        {
            this._switching = false;
            this._epoch++;
            this._loadedCollection = null;
            this._switchShownByRead = null;
            this._scenes.Clear();
            this._inputs.Clear();
            this._refs.Clear();
            this._removedUnknown.Clear();
            this._gone.Clear();
            this._goneItems.Clear();
            this._readReleased.Clear();
            this._unresolved.Clear();
            this._programUuid = new Cell<Guid?>(null, 0);

            // The preview scene and the transitions are those of the collection: the new one has its own. What the engine knew about a transition that was running is not true any more.
            this._previewUuid = new Cell<Guid?>(null, 0);
            this._transition = new Cell<TransitionInfo?>(null, 0);
            this._transitionDuration = new Cell<int?>(null, 0);
            this._transitionRunning = new Cell<bool>(false, 0);

            // The canvases are saved with the scene collection: the new one has its own, and the main canvas is read again with them.
            this._canvases = new Cell<CanvasList?>(null, 0);
            this._removedCanvases.Clear();
            this._scenesRead = this._inputsRead = this._programRead = this._studioRead = this._previewRead = this._transitionRead = this._canvasesRead = false;
            this._lastCleanup = this._now;
        }
    }
}
