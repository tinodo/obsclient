namespace SampleWindowsAppliation
{
    using System.ComponentModel;
    using System.Diagnostics;
    using OBSStudioClient;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Events;

    public partial class Form1 : Form
    {
        private readonly ObsClient _client = new();
        
        private delegate void SafeListboxRemove(object itemToRemove, ListBox listBox);
        
        private delegate void SafeListboxRefresh(ListBox listBox);
        
        private delegate void SafeUpdateTitle();
        
        private delegate void SafeUpdateRecordState(string text);
        
        private delegate void SafeUpdateRecordFolder(string text);
        
        private const string Title = "Obs Client";

        public Form1()
        {
            InitializeComponent();
            _client.ConnectionClosed += ObsConnectionClosed;
            _client.CurrentPreviewSceneChanged += CurrentPreviewSceneChanged;
            _client.CurrentProfileChanged += CurrentProfileChanged;
            _client.CurrentProfileChanging += CurrentProfileChanging;
            _client.CurrentProgramSceneChanged += CurrentProgramSceneChanged;
            _client.CurrentSceneCollectionChanged += CurrentSceneCollectionChanged;
            _client.CurrentSceneCollectionChanging += CurrentSceneCollectionChanging;
            _client.CurrentSceneTransitionChanged += CurrentSceneTransitionChanged;
            _client.CurrentSceneTransitionDurationChanged += CurrentSceneTransitionDurationChanged;
            _client.CustomEvent += CustomEvent;
            _client.ExitStarted += ExitStarted;
            _client.InputActiveStateChanged += InputActiveStateChanged;
            _client.InputAudioBalanceChanged += InputAudioBalanceChanged;
            _client.InputAudioMonitorTypeChanged += InputAudioMonitorTypeChanged;
            _client.InputAudioSyncOffsetChanged += InputAudioSyncOffsetChanged;
            _client.InputAudioTracksChanged += InputAudioTracksChanged;
            _client.InputCreated += InputCreated;
            _client.InputMuteStateChanged += InputMuteStateChanged;
            _client.InputNameChanged += InputNameChanged;
            _client.InputRemoved += InputRemoved;
            _client.InputShowStateChanged += InputShowStateChanged;
            _client.InputVolumeChanged += InputVolumeChanged;
            _client.InputVolumeMeters += InputVolumeMeters;
            _client.MediaInputActionTriggered += MediaInputActionTriggered;
            _client.MediaInputPlaybackEnded += MediaInputPlaybackEnded;
            _client.MediaInputPlaybackStarted += MediaInputPlaybackStarted;
            _client.ProfileListChanged += ProfileListChanged;
            _client.RecordStateChanged += RecordStateChanged;
            _client.ReplayBufferSaved += ReplayBufferSaved;
            _client.ReplayBufferStateChanged += ReplayBufferStateChanged;
            _client.SceneCollectionListChanged += SceneCollectionListChanged;
            _client.SceneCreated += SceneCreated;
            _client.SceneItemCreated += SceneItemCreated;
            _client.SceneItemEnableStateChanged += SceneItemEnableStateChanged;
            _client.SceneItemListReindexed += SceneItemListReindexed;
            _client.SceneItemLockStateChanged += SceneItemLockStateChanged;
            _client.SceneItemRemoved += SceneItemRemoved;
            _client.SceneItemSelected += SceneItemSelected;
            _client.SceneItemTransformChanged += SceneItemTransformChanged;
            _client.SceneListChanged += SceneListChanged;
            _client.SceneNameChanged += SceneNameChanged;
            _client.SceneRemoved += SceneRemoved;
            _client.SceneTransitionEnded += SceneTransitionEnded;
            _client.SceneTransitionStarted += SceneTransitionStarted;
            _client.SceneTransitionVideoEnded += SceneTransitionVideoEnded;
            _client.ScreenshotSaved += ScreenshotSaved;
            _client.SourceFilterCreated += SourceFilterCreated;
            _client.SourceFilterEnableStateChanged += SourceFilterEnableStateChanged;
            _client.SourceFilterListReindexed += SourceFilterListReindexed;
            _client.SourceFilterNameChanged += SourceFilterNameChanged;
            _client.SourceFilterRemoved += SourceFilterRemoved;
            _client.StreamStateChanged += StreamStateChanged;
            _client.StudioModeStateChanged += StudioModeStateChanged;
            _client.VendorEvent += VendorEvent;
            _client.VirtualcamStateChanged += VirtualcamStateChanged;

            _client.PropertyChanged += PropertyChanged;

            this.UpdateTitle();
        }

        private void PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ConnectionState")
            {
                Task.Run(() => this.UpdateTitle());
            }

            Debug.WriteLine($"{e.PropertyName} - {_client.ConnectionState}");
        }

        private void VirtualcamStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
        }

        private void VendorEvent(object? sender, VendorEventEventArgs e)
        {
        }

        private void StudioModeStateChanged(object? sender, StudioModeStateChangedEventArgs e)
        {
        }

        private void StreamStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
        }

        private void SourceFilterRemoved(object? sender, SourceFilterRemovedEventArgs e)
        {
        }

        private void SourceFilterNameChanged(object? sender, SourceFilterNameChangedEventArgs e)
        {
        }

        private void SourceFilterListReindexed(object? sender, SourceFiltersEventArgs e)
        {
        }

        private void SourceFilterEnableStateChanged(object? sender, SourceFilterEnableStateChangedEventArgs e)
        {
        }

        private void SourceFilterCreated(object? sender, SourceFilterCreatedEventArgs e)
        {
        }

        private void ScreenshotSaved(object? sender, ScreenshotSavedEventArgs e)
        {
        }

        private void SceneTransitionVideoEnded(object? sender, TransitionNameEventArgs e)
        {
        }

        private void SceneTransitionStarted(object? sender, TransitionNameEventArgs e)
        {
        }

        private void SceneTransitionEnded(object? sender, TransitionNameEventArgs e)
        {
        }

        private void SceneRemoved(object? sender, SceneModifiedEventArgs e)
        {
            Scene? scene = null;
            foreach (Scene s in this.lbScenes.Items)
            {
                if (s.SceneName == e.SceneName)
                {
                    scene = s;
                    break;
                }
            }

            if (scene != null)
            {
                this.RemoveItemFromListbox(scene, this.lbScenes);
            }
        }

        private void SceneNameChanged(object? sender, SceneNameChangedEventArgs e)
        {
            MessageBox.Show($"Scene '{e.OldSceneName}' was renamed to '{e.SceneName}'. Refresh the Scenes List using Get Scene List.");
        }

        private void SceneListChanged(object? sender, SceneListEventArgs e)
        {
        }

        private void SceneItemTransformChanged(object? sender, SceneItemTransformChangedEventArgs e)
        {
        }

        private void SceneItemSelected(object? sender, SceneItemSelectedEventArgs e)
        {
        }

        private void SceneItemRemoved(object? sender, SceneItemRemovedEventArgs e)
        {
        }

        private void SceneItemLockStateChanged(object? sender, SceneItemLockStateChangedEventArgs e)
        {
        }

        private void SceneItemListReindexed(object? sender, SceneItemListReindexedEventArgs e)
        {
        }

        private void SceneItemEnableStateChanged(object? sender, SceneItemEnableStateChangedEventArgs e)
        {
        }

        private void SceneItemCreated(object? sender, SceneItemCreatedEventArgs e)
        {
        }

        private void SceneCreated(object? sender, SceneModifiedEventArgs e)
        {
            MessageBox.Show($"Scene '{e.SceneName}' created. Refresh the Scenes List using Get Scene List.");
        }

        private void SceneCollectionListChanged(object? sender, SceneCollectionListEventArgs e)
        {
        }

        private void ReplayBufferStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
        }

        private void ReplayBufferSaved(object? sender, ReplayBufferSavedEventArgs e)
        {
        }

        private void RecordStateChanged(object? sender, RecordStateChangedEventArgs e)
        {
            var active = e.OutputActive ? "Active" : "Inactive";
            RefreshRecordState($"{e.OutputState} ({active})");
            RefreshRecordFolder(e.OutputPath);
        }

        private void ProfileListChanged(object? sender, ProfileListEventArgs e)
        {
        }

        private void MediaInputPlaybackStarted(object? sender, InputNameEventArgs e)
        {
        }

        private void MediaInputPlaybackEnded(object? sender, InputNameEventArgs e)
        {
        }

        private void MediaInputActionTriggered(object? sender, MediaInputActionTriggeredEventArgs e)
        {
        }

        private void InputVolumeMeters(object? sender, InputVolumeMetersEventArgs e)
        {
        }

        private void InputVolumeChanged(object? sender, InputVolumeChangedEventArgs e)
        {
        }

        private void InputShowStateChanged(object? sender, InputShowStateChangedEventArgs e)
        {
        }

        private void InputRemoved(object? sender, InputNameEventArgs e)
        {
        }

        private void InputNameChanged(object? sender, InputNameChangedEventArgs e)
        {
        }

        private void InputMuteStateChanged(object? sender, InputMuteStateChangedEventArgs e)
        {
        }

        private void InputCreated(object? sender, InputCreatedEventArgs e)
        {
        }

        private void InputAudioTracksChanged(object? sender, InputAudioTracksChangedEventArgs e)
        {
        }

        private void InputAudioSyncOffsetChanged(object? sender, InputAudioSyncOffsetChangedEventArgs e)
        {
        }

        private void InputAudioMonitorTypeChanged(object? sender, InputAudioMonitorTypeChangedEventArgs e)
        {
        }

        private void InputAudioBalanceChanged(object? sender, InputAudioBalanceChangedEventArgs e)
        {
        }

        private void InputActiveStateChanged(object? sender, InputActiveStateChangedEventArgs e)
        {
        }

        private void ExitStarted(object? sender, EventArgs e)
        {
        }

        private void CustomEvent(object? sender, CustomEventEventArgs e)
        {
        }

        private void CurrentSceneTransitionDurationChanged(object? sender, TransitionDurationEventArgs e)
        {
        }

        private void CurrentSceneTransitionChanged(object? sender, TransitionNameEventArgs e)
        {
        }

        private void CurrentSceneCollectionChanging(object? sender, SceneCollectionNameEventArgs e)
        {
        }

        private void CurrentSceneCollectionChanged(object? sender, SceneCollectionNameEventArgs e)
        {
        }

        private void CurrentProgramSceneChanged(object? sender, SceneNameEventArgs e)
        {
            MessageBox.Show(e.SceneName, "CurrentProgramSceneChanged");
        }

        private void CurrentProfileChanging(object? sender, ProfileNameEventArgs e)
        {
        }

        private void CurrentProfileChanged(object? sender, ProfileNameEventArgs e)
        {
        }

        private void CurrentPreviewSceneChanged(object? sender, SceneNameEventArgs e)
        {
        }

        private void RemoveItemFromListbox(object itemToRemove, ListBox listBox)
        {
            if (itemToRemove is null)
            {
                return;
            }

            if (listBox.InvokeRequired)
            {
                var d = new SafeListboxRemove(RemoveItemFromListbox);
                listBox.Invoke(d, new object[] { itemToRemove, listBox });
            }
            else
            {
                listBox.Items.Remove(itemToRemove);
            }
        }

        private void UpdateTitle()
        {
            if (this.InvokeRequired)
            {
                var d = new SafeUpdateTitle(this.UpdateTitle);
                this.Invoke(d);
            }
            else
            {
                this.Text = $"{Title} - {_client.ConnectionState}";
            }
        }

        private void RefreshListbox(ListBox listBox)
        {
            if (listBox.InvokeRequired)
            {
                var d = new SafeListboxRefresh(RefreshListbox);
                listBox.Invoke(d, new object[] { listBox });
            }
            else
            {
                listBox.Refresh();
            }
        }

        private void RefreshRecordState(string text)
        {
            if (lblRecordState.InvokeRequired)
            {
                var d = new SafeUpdateRecordState(RefreshRecordState);
                lblRecordState.Invoke(d, new object[] { text });
            }
            else
            {
                lblRecordState.Text = text;
            }
        }

        private void RefreshRecordFolder(string text)
        {
            if (tbRecordFolder.InvokeRequired)
            {
                var d = new SafeUpdateRecordFolder(RefreshRecordFolder);
                tbRecordFolder.Invoke(d, new object[] { text });
            }
            else
            {
                tbRecordFolder.Text = text;
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var result = await _client.ConnectAsync(this.cbAutoReconnect.Checked, this.tbPassword.Text, this.tbHostname.Text, Convert.ToInt32(this.nudPort.Value));
            if (!result)
            {
                MessageBox.Show("Could not connect.");
            }
        }

        private void ObsConnectionClosed(object? sender, ConnectionClosedEventArgs e)
        {
            MessageBox.Show($"{e.WebSocketCloseCode}: {e.WebSocketCloseDescription}", "Connection Closed");
        }

        private async void btnStartVirtualCam_Click(object sender, EventArgs e)
        {
            await _client.StartVirtualCam();
        }

        private async void btnStopVirtualCamera_Click(object sender, EventArgs e)
        {
            await _client.StopVirtualCam();
        }

        private async void btnToggleVirtualCamera_Click(object sender, EventArgs e)
        {
            var result = await _client.ToggleVirtualCam();
            MessageBox.Show(result.ToString(), "ToggleVirtualCam");
        }

        private async void btnGetVirtualCameraState_Click(object sender, EventArgs e)
        {
            var result = await _client.GetVirtualCamStatus();
            MessageBox.Show(result.ToString(), "GetVirtualCamStatus");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _client.Disconnect();
        }

        private async void btnStartStudioMode_Click(object sender, EventArgs e)
        {
            await _client.SetStudioModeEnabled(true);
        }

        private async void btnStopStudioMode_Click(object sender, EventArgs e)
        {
            await _client.SetStudioModeEnabled(false);
        }

        private async void btnGetStudioModeEnabled_Click(object sender, EventArgs e)
        {
            var result = await _client.GetStudioModeEnabled();
            MessageBox.Show(result.ToString(), "GetStudioModeEnabled");
        }

        private async void btnGetMonitorList_Click(object sender, EventArgs e)
        {
            var monitors = await _client.GetMonitorList();
            var result = string.Empty;
            foreach (var monitor in monitors)
            {
                result += $"{monitor.MonitorIndex}: {monitor.MonitorName} ({monitor.MonitorWidth}x{monitor.MonitorHeight}) at {monitor.MonitorPositionX},{monitor.MonitorPositionY}" + Environment.NewLine;
            }

            MessageBox.Show(result, "GetMonitorList");
        }

        private async void btnGetVersion_Click(object sender, EventArgs e)
        {
            var version = await _client.GetVersion();
            var result = $"OBS Studio {version.ObsVersion} ({version.Platform} - {version.PlatformDescription})" + Environment.NewLine;
            result += $"WebSockets {version.ObsWebSocketVersion} - RPC Version {version.RpcVersion}" + Environment.NewLine;
            result += $"Requests: {string.Join(", ", version.AvailableRequests)}" + Environment.NewLine;
            result += $"Image Formats: {string.Join(", ", version.SupportedImageFormats)}" + Environment.NewLine;
            MessageBox.Show(result, "GetVersion");
        }

        private async void btnGetStats_Click(object sender, EventArgs e)
        {
            var stats = await _client.GetStats();
            var result = $"ActiveFps: {stats.ActiveFps}" + Environment.NewLine;
            result += $"AvailableDiskSpace: {stats.AvailableDiskSpace}" + Environment.NewLine;
            result += $"AverageFrameRenderTime: {stats.AverageFrameRenderTime}" + Environment.NewLine;
            result += $"CpuUsage: {stats.CpuUsage}" + Environment.NewLine;
            result += $"MemoryUsage: {stats.MemoryUsage}" + Environment.NewLine;
            result += $"OutputSkippedFrames: {stats.OutputSkippedFrames}" + Environment.NewLine;
            result += $"OutputTotalFrames: {stats.OutputTotalFrames}" + Environment.NewLine;
            result += $"RenderSkippedFrames: {stats.RenderSkippedFrames}" + Environment.NewLine;
            result += $"RenderTotalFrames: {stats.RenderTotalFrames}" + Environment.NewLine;
            result += $"WebSocketSessionIncomingMessages: {stats.WebSocketSessionIncomingMessages}" + Environment.NewLine;
            result += $"WebSocketSessionOutgoingMessages: {stats.WebSocketSessionOutgoingMessages}" + Environment.NewLine;
            MessageBox.Show(result, "GetStats");
        }

        private async void btnGetHotkeyList_Click(object sender, EventArgs e)
        {
            var hotkeyList = await _client.GetHotkeyList();
            MessageBox.Show(string.Join(", ", hotkeyList), "GetHotkeyList");
        }

        private async void btnGetSceneCollectionList_Click(object sender, EventArgs e)
        {
            var sceneCollectionList = await _client.GetSceneCollectionList();
            var result = $"CurrentSceneCollectionName: {sceneCollectionList.CurrentSceneCollectionName}" + Environment.NewLine;
            result += $"SceneCollections: {string.Join(", ", sceneCollectionList.SceneCollections)}";
            MessageBox.Show(result, "GetSceneCollectionList");
        }

        private async void btnGetProfileList_Click(object sender, EventArgs e)
        {
            var profileList = await _client.GetProfileList();
            var result = $"CurrentProfileName: {profileList.CurrentProfileName}" + Environment.NewLine;
            result += $"Profiles: {string.Join(", ", profileList.Profiles)}";
            MessageBox.Show(result, "GetProfileList");
        }

        private async void btnGetRecordDirectory_Click(object sender, EventArgs e)
        {
            var recordDirectory = await _client.GetRecordDirectory();
            RefreshRecordFolder(recordDirectory);
        }

        private async void btnGetSceneList_Click(object sender, EventArgs e)
        {
            var sceneList = await _client.GetSceneList();
            this.lbScenes.Items.Clear();
            var result = $"CurrentPreviewSceneName: {sceneList.CurrentPreviewSceneName}" + Environment.NewLine;
            result += $"CurrentProgramSceneName: {sceneList.CurrentProgramSceneName}" + Environment.NewLine;
            result += $"Scenes:" + Environment.NewLine;
            foreach (var scene in sceneList.Scenes)
            {
                this.lbScenes.Items.Add(scene);
                result += $"{scene.SceneIndex}: {scene.SceneName}" + Environment.NewLine;
            }

            MessageBox.Show(result, "GetSceneList");
        }

        private async void btnGetGroupList_Click(object sender, EventArgs e)
        {
            var groupList = await _client.GetGroupList();
            MessageBox.Show(string.Join(", ", groupList), "GetGroupList");
            this.lbGroups.DataSource = groupList;
        }

        private async void btnGetCurrentProgramScene_Click(object sender, EventArgs e)
        {
            var result = await _client.GetCurrentProgramScene();
            MessageBox.Show(result, "GetCurrentProgramScene");
        }

        private async void btnGetCurrentPreviewScene_Click(object sender, EventArgs e)
        {
            var result = await _client.GetCurrentPreviewScene();
            MessageBox.Show(result, "GetCurrentPreviewScene");
        }

        private async void btnGetInputList_Click(object sender, EventArgs e)
        {
            var inputs = await _client.GetInputList();
            var result = "Inputs:" + Environment.NewLine;
            foreach (var input in inputs)
            {
                result += $"Input name: {input.InputName}, Input Kind: {input.InputKind}, Unversioned Input Kind: {input.UnversionedInputKind}" + Environment.NewLine;
            }

            MessageBox.Show(result, "GetInputList");
        }

        private async void btnGetInputKindList_Click(object sender, EventArgs e)
        {
            var inputKindList = await _client.GetInputKindList();
            MessageBox.Show("Input Kinds: " + string.Join(", ", inputKindList), "GetInputKindList");
        }

        private async void btnGetSpecialInputs_Click(object sender, EventArgs e)
        {
            var specialInputs = await _client.GetSpecialInputs();
            var result = $"Mic1: {specialInputs.Mic1}" + Environment.NewLine;
            result += $"Mic2: {specialInputs.Mic2}" + Environment.NewLine;
            result += $"Mic3: {specialInputs.Mic3}" + Environment.NewLine;
            result += $"Mic4: {specialInputs.Mic4}" + Environment.NewLine;
            result += $"Desktop1: {specialInputs.Desktop1}" + Environment.NewLine;
            result += $"Desktop2: {specialInputs.Desktop2}" + Environment.NewLine;
            MessageBox.Show(result, "GetSpecialInputs");
        }

        private async void btnSetCurrentProgramScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                await _client.SetCurrentProgramScene(scene.SceneName);
            }
        }

        private async void btnSetCurrentPreviewScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                await _client.SetCurrentPreviewScene(scene.SceneName);
            }
        }

        private async void btnRemoveScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                await _client.RemoveScene(scene.SceneName);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.lbScenes.DisplayMember = "SceneName";
        }

        private async void btnCreateScene_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.tbNameItem.Text))
            {
                MessageBox.Show("Enter an item name first.");
            }
            else
            {
                await _client.CreateScene(this.tbNameItem.Text);
            }
        }

        private async void btnSetSceneName_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.tbNameItem.Text))
            {
                MessageBox.Show("Enter an item name first.");
                return;
            }

            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
                return;
            }

            await _client.SetSceneName(scene.SceneName, this.tbNameItem.Text);
        }

        private async void bnGetStreamStatus_Click(object sender, EventArgs e)
        {
            var response = await _client.GetStreamStatus();
            var result = string.Empty;
            result += $"OutputActive: {response.OutputActive}" + Environment.NewLine;
            result += $"OutputBytes: {response.OutputBytes}" + Environment.NewLine;
            result += $"OutputCongestion: {response.OutputCongestion}" + Environment.NewLine;
            result += $"OutputDuration: {response.OutputDuration}" + Environment.NewLine;
            result += $"OutputReconnecting: {response.OutputReconnecting}" + Environment.NewLine;
            result += $"OutputSkippedFrames: {response.OutputSkippedFrames}" + Environment.NewLine;
            result += $"OutputTimecode: {response.OutputTimecode}" + Environment.NewLine;
            result += $"OutputTotalFrames: {response.OutputTotalFrames}" + Environment.NewLine;
            MessageBox.Show(result, "GetStreamStatus");
        }

        private async void btnGetRecordStatus_Click(object sender, EventArgs e)
        {
            var response = await _client.GetRecordStatus();
            var result = string.Empty;
            result += $"OutputActive: {response.OutputActive}" + Environment.NewLine;
            result += $"OutputBytes: {response.OutputBytes}" + Environment.NewLine;
            result += $"OutputDuration: {response.OutputDuration}" + Environment.NewLine;
            result += $"OutputPaused: {response.OutputPaused}" + Environment.NewLine;
            result += $"OutputTimecode: {response.OutputTimecode}" + Environment.NewLine;
            MessageBox.Show(result, "GetRecordStatus");
        }

        private async void btnToggleRecord_Click(object sender, EventArgs e)
        {
            var result = await _client.ToggleRecord();
            MessageBox.Show(result.ToString(), "ToggleRecord");
        }

        private async void btnStartRecord_Click(object sender, EventArgs e)
        {
            await _client.StartRecord();
        }

        private async void btnStopRecord_Click(object sender, EventArgs e)
        {
            var result = await _client.StopRecord();
            MessageBox.Show(result, "StopRecord");
        }

        private async void btnToggleRecordPause_Click(object sender, EventArgs e)
        {
            var result = await _client.ToggleRecordPause();
            MessageBox.Show(result.ToString(), "ToggleRecordPause");
        }

        private async void btnPauseRecord_Click(object sender, EventArgs e)
        {
            await _client.PauseRecord();
        }

        private async void btnResumeRecord_Click(object sender, EventArgs e)
        {
            await _client.ResumeRecord();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await _client.SetRecordDirectory(this.tbRecordFolder.Text);
        }

        private void cbAutoReconnect_CheckedChanged(object sender, EventArgs e)
        {
            _client.AutoReconnect = this.cbAutoReconnect.Checked;
        }
    }
}