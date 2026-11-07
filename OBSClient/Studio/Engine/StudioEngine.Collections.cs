namespace OBSStudioClient.Studio.Engine
{
    internal sealed partial class StudioEngine
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

        // OBS Studio begins to change its scene collection (CurrentSceneCollectionChanging). From now until it says that it has done so, it answers every request with 207 and
        // sends nothing (obs-websocket 5.4.0 and later), and what it does send is not about the collection that the model will hold. So nothing is asked and nothing is
        // taken from the events, and the answer to a read that is under way is not applied (Owns). The model keeps what it holds, for the application to read as the last that
        // was known, and says that it is being read (the state): it is of the collection that OBS Studio is leaving.
        private void OnCollectionChanging()
        {
            if (this._switching)
            {
                return;
            }

            this._switching = true;
            this._state = StudioEngineState.Synchronizing;
        }

        // OBS Studio has changed its scene collection (CurrentSceneCollectionChanged): this is the end of the switch, and it is also what tells the model when the event that
        // begins one was not seen (the connection was made in the middle of it, or the event was lost). The collection that the model held is not loaded any more, and the
        // one that is, is read as the first on a connection is: the scenes and inputs of the old one are gone (the application is told so, by the objects that it holds), and
        // nothing of it is merged into the new one. A scene with the same UUID in both is the same scene when the collection is loaded again, but it is a new object.
        private void OnCollectionChanged()
        {
            this.ForgetCollection();
            this._state = StudioEngineState.Synchronizing;

            // Studio mode is not a part of the collection, but it is read again with the rest.
            foreach (var scope in this.AllScopes())
            {
                scope.InFlight = false;
                scope.Attempts = 0;
                scope.NotBefore = TimeSpan.Zero;
                scope.Dirty = true;
            }
        }

        // The model of the collection that OBS Studio has left is dropped, and the model is of the next collection: the reads that were taken for the old one are outdated.
        // It is also what a new connection does when the one before it ended in the middle of a switch (OnSessionStarted): OBS Studio has loaded a collection since, maybe the
        // one that the model holds (the same UUIDs), and its sources are new ones all the same.
        private void ForgetCollection()
        {
            this._switching = false;
            this._epoch++;
            this._scenes.Clear();
            this._inputs.Clear();
            this._refs.Clear();
            this._removedUnknown.Clear();
            this._gone.Clear();
            this._goneItems.Clear();
            this._readReleased.Clear();
            this._unresolved.Clear();
            this._programUuid = new Cell<Guid?>(null, 0);
            this._scenesRead = this._inputsRead = this._programRead = this._studioRead = false;
            this._lastCleanup = this._now;
        }
    }
}
