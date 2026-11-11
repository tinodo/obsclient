namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// The profiles of the simulated OBS Studio: which one is current, the names of all, and what a switch, a new profile and a removed one announce. A profile does not belong to a scene collection:
    /// switching the collection leaves the profiles as they are.
    /// </summary>
    internal sealed partial class SimObs
    {
        // How long later a task of the user interface that was held up by the loading of a scene collection is tried again.
        private static readonly TimeSpan UiPollInterval = TimeSpan.FromMilliseconds(1);

        private readonly List<string> _profileNames = ["Untitled"];
        private readonly Queue<(TimeSpan At, Action Run)> _uiTasks = new();
        private string _currentProfile = "Untitled";
        private TimeSpan _lastUiTaskAt;

        /// <summary>Gets the name of the profile that OBS Studio has loaded.</summary>
        public string CurrentProfile => _currentProfile;

        /// <summary>Gets the names of the profiles, the current one included, in the order in which OBS Studio lists them.</summary>
        /// <remarks>OBS Studio sorts them by name, ignoring case, in the language of the system (OBSBasic_Profiles.cpp, updateSortedProfiles); the simulation compares the characters, ignoring case.</remarks>
        public IReadOnlyList<string> Profiles => _profileNames.Order(StringComparer.OrdinalIgnoreCase).ThenBy(name => name, StringComparer.Ordinal).ToArray();

        // What a request or the user does when it switches to another profile: OBS Studio says that it is leaving the profile that is current, and that the other is current now, at the same
        // moment (measured with OBS Studio 33.0.0: both events of a switch came in the same millisecond).
        internal void SwitchProfile(string name)
        {
            var changing = Emit("CurrentProfileChanging", Obj(("profileName", _currentProfile)), -1, TimeSpan.Zero);
            _currentProfile = name;
            Emit("CurrentProfileChanged", Obj(("profileName", name)), changing, TimeSpan.Zero);
        }

        // OBS Studio makes a profile and switches to it. It lists the profile, and says a little later that it is the current one (it resets its video and its outputs in between): there is no
        // CurrentProfileChanging for it (measured). A profile of that name that exists is not made again (the request that asked refused it, but one that was asked for just before may have made it).
        internal void MakeProfile(string name)
        {
            if (_profileNames.Contains(name, StringComparer.Ordinal))
            {
                return;
            }

            _profileNames.Add(name);
            AddProfileData(name);
            _currentProfile = name;
            var listed = Emit("ProfileListChanged", Obj(("profiles", ProfileList())), -1, TimeSpan.Zero);
            Emit("CurrentProfileChanged", Obj(("profileName", name)), listed, Quirks.ProfileActivationTime.Sample(Random));
        }

        // OBS Studio removes a profile that is not the current one, and lists the others. Nothing happens to a profile that is not there (it was removed meanwhile).
        internal void DropProfile(string name)
        {
            if (string.Equals(name, _currentProfile, StringComparison.Ordinal) || !_profileNames.Remove(name))
            {
                return;
            }

            RemoveProfileData(name);
            Emit("ProfileListChanged", Obj(("profiles", ProfileList())), -1, TimeSpan.Zero);
        }

        // The user answers the question of OBS Studio ("remove the profile?") with yes, for the profile that is current. OBS Studio makes the profile that comes first by the bytes of its name
        // (it keeps its profiles in a std::map) the current one, and removes the one that it leaves. It says CurrentProfileChanging and CurrentProfileChanged, and nothing about the list:
        // on_actionRemoveProfile_triggered does not send ProfileListChanged (OBSBasic_Profiles.cpp, OBS Studio 33.0.0).
        internal void DropCurrentProfile()
        {
            if (_profileNames.Count < 2)
            {
                return;
            }

            var next = _profileNames.Where(name => !string.Equals(name, _currentProfile, StringComparison.Ordinal)).Order(StringComparer.Ordinal).First();
            var changing = Emit("CurrentProfileChanging", Obj(("profileName", _currentProfile)), -1, TimeSpan.Zero);
            _profileNames.Remove(_currentProfile);
            RemoveProfileData(_currentProfile);
            _currentProfile = next;
            Emit("CurrentProfileChanged", Obj(("profileName", next)), changing, TimeSpan.Zero);
        }

        // Something that the user interface of OBS Studio does a moment after a request was answered, as a change of its own. The user interface does its tasks one after the other, in the order in
        // which they were queued (a Qt queued call), and not while OBS Studio loads a scene collection (it is busy then). A task does not belong to the collection that was loaded when it was
        // asked for: it is not lost when the collection changes.
        internal void ScheduleUi(TimeSpan delay, Action action)
        {
            var at = Clock.Now + delay;
            if (at < _lastUiTaskAt)
            {
                at = _lastUiTaskAt;
            }

            _lastUiTaskAt = at;
            _uiTasks.Enqueue((at, action));
            _actions.Enqueue(RunUi, (at, _actionSequence++));
        }

        private void RunUi()
        {
            while (_uiTasks.TryPeek(out var task) && task.At <= Clock.Now)
            {
                if (!_ready)
                {
                    _actions.Enqueue(RunUi, (Clock.Now + UiPollInterval, _actionSequence++));
                    return;
                }

                _uiTasks.Dequeue();
                RunChange(task.Run);
            }
        }

        private JsonArray ProfileList()
        {
            return Arr(Profiles.Select(name => (JsonNode?)name));
        }

        private SimNames ProfilesSnapshot()
        {
            return new SimNames(_currentProfile, Profiles);
        }

        // What the profiles have to be: there is always one, it is the current one, and the names are all different.
        private void FindProfileProblems(List<string> problems)
        {
            if (_profileNames.Count == 0)
            {
                problems.Add("There is no profile.");
            }

            if (!_profileNames.Contains(_currentProfile, StringComparer.Ordinal))
            {
                problems.Add($"The current profile \"{_currentProfile}\" is not one of the profiles.");
            }

            if (_profileNames.Distinct(StringComparer.Ordinal).Count() != _profileNames.Count)
            {
                problems.Add("Two profiles have the same name.");
            }
        }
    }
}
