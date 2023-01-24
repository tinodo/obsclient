namespace SampleWindowsAppliation
{
    using System.ComponentModel;
    using System.Diagnostics;
    using OBSStudioClient;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.MessageClasses;

    public partial class Form1 : Form
    {
        private ObsClient _client = new();
        private delegate void SafeListboxRemove(object itemToRemove, ListBox listBox);
        private delegate void SafeListboxRefresh(ListBox listBox);
        private delegate void SafeUpdateTitle();
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

        private Task VirtualcamStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task VendorEvent(object? sender, VendorEventEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task StudioModeStateChanged(object? sender, StudioModeStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task StreamStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SourceFilterRemoved(object? sender, SourceFilterRemovedEventArgs e)
        {            
            return Task.CompletedTask;
        }

        private Task SourceFilterNameChanged(object? sender, SourceFilterNameChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SourceFilterListReindexed(object? sender, SourceFiltersEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SourceFilterEnableStateChanged(object? sender, SourceFilterEnableStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SourceFilterCreated(object? sender, SourceFilterCreatedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ScreenshotSaved(object? sender, ScreenshotSavedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneTransitionVideoEnded(object? sender, TransitionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneTransitionStarted(object? sender, TransitionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneTransitionEnded(object? sender, TransitionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneRemoved(object? sender, SceneModifiedEventArgs e)
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

            return Task.CompletedTask;
        }

        private Task SceneNameChanged(object? sender, SceneNameChangedEventArgs e)
        {
            MessageBox.Show($"Scene '{e.OldSceneName}' was renamed to '{e.SceneName}'. Refresh the Scenes List using Get Scene List.");
            return Task.CompletedTask;
        }

        private Task SceneListChanged(object? sender, SceneListEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemTransformChanged(object? sender, SceneItemTransformChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemSelected(object? sender, SceneItemSelectedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemRemoved(object? sender, SceneItemRemovedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemLockStateChanged(object? sender, SceneItemLockStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemListReindexed(object? sender, SceneItemListReindexedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemEnableStateChanged(object? sender, SceneItemEnableStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneItemCreated(object? sender, SceneItemCreatedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task SceneCreated(object? sender, SceneModifiedEventArgs e)
        {
            MessageBox.Show($"Scene '{e.SceneName}' created. Refresh the Scenes List using Get Scene List.");
            return Task.CompletedTask;
        }

        private Task SceneCollectionListChanged(object? sender, SceneCollectionListEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ReplayBufferStateChanged(object? sender, OutputStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ReplayBufferSaved(object? sender, ReplayBufferSavedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task RecordStateChanged(object? sender, RecordStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ProfileListChanged(object? sender, ProfileListEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task MediaInputPlaybackStarted(object? sender, InputNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task MediaInputPlaybackEnded(object? sender, InputNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task MediaInputActionTriggered(object? sender, MediaInputActionTriggeredEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputVolumeMeters(object? sender, InputVolumeMetersEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputVolumeChanged(object? sender, InputVolumeChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputShowStateChanged(object? sender, InputShowStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputRemoved(object? sender, InputNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputNameChanged(object? sender, InputNameChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputMuteStateChanged(object? sender, InputMuteStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputCreated(object? sender, InputCreatedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputAudioTracksChanged(object? sender, InputAudioTracksChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputAudioSyncOffsetChanged(object? sender, InputAudioSyncOffsetChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputAudioMonitorTypeChanged(object? sender, InputAudioMonitorTypeChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputAudioBalanceChanged(object? sender, InputAudioBalanceChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task InputActiveStateChanged(object? sender, InputActiveStateChangedEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task ExitStarted(object? sender)
        {
            return Task.CompletedTask;
        }

        private Task CustomEvent(object? sender, CustomEventEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentSceneTransitionDurationChanged(object? sender, TransitionDurationEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentSceneTransitionChanged(object? sender, TransitionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentSceneCollectionChanging(object? sender, SceneCollectionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentSceneCollectionChanged(object? sender, SceneCollectionNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentProgramSceneChanged(object? sender, SceneNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentProfileChanging(object? sender, ProfileNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentProfileChanged(object? sender, ProfileNameEventArgs e)
        {
            return Task.CompletedTask;
        }

        private Task CurrentPreviewSceneChanged(object? sender, SceneNameEventArgs e)
        {
            return Task.CompletedTask;
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

            //Application.DoEvents();
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

        private async void button1_Click(object sender, EventArgs e)
        {
            var result = await _client.ConnectAsync(this.tbPassword.Text, this.tbHostname.Text, Convert.ToInt32(this.nudPort.Value));
            if (!result)
            {
                MessageBox.Show("Could not connect.");
            }
            _client.RequestTimeout = 1000;
            //List<RequestMessage> requests = new()
            //{
            //    new RequestMessage(RequestType.GetSourceFilterList, "Azure Hero Blue", new { sourceName = "Azure Hero Blue" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Survey", new { sourceName = "Survey" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Random Azure Video", new { sourceName = "Random Azure Video" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Timer", new { sourceName = "Timer" }),
            //    new RequetMessage(RequestType.GetSourceFilterList, "Break Background", new { sourceName = "Break Background" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Break Text", new { sourceName = "Break Text" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Course Title", new { sourceName = "Course Title" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "QR", new { sourceName = "QR" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Green Screen Effects Scene", new { sourceName = "Green Screen Effects Scene" }),
            //    new RequestMessage(RequestType.GetSourceFilterList, "Mic and Censor Beep", new { sourceName = "Mic and Censor Beep" })
            //};
            //var filters = await _client.SendRequestBatch(RequestBatchExecutionType.SerialFrame, requests.ToArray());

            Thread.Sleep(1000);
            var xxx = await _client.GetSourceFilter("Timer Music", "Audio Monitor");
            var yyy = await _client.GetSourceFilterDefaultSettings("audio_monitor");
            var zzz = await _client.GetSourceFilterList("Timer Music");

            var scenes = await _client.GetSceneList();
            foreach (var scene in scenes.Scenes)
            {
                var sceneItems = await _client.GetSceneItemList(scene.SceneName);
                var sceneFilters = await _client.GetSourceFilterList(scene.SceneName);
                foreach (var sceneFilter in sceneFilters)
                {
                    var sf = await _client.GetSourceFilter(scene.SceneName, sceneFilter.FilterName);
                    //var sss = await _client.GetSceneItemEnabled(scene.SceneName, sceneFilter.FilterIndex);
                }

                foreach (var sceneItem in sceneItems)
                {
                    var sourceItems = await _client.GetSourceFilterList(sceneItem.SourceName);
                    var sss2 = await _client.GetSceneItemEnabled(scene.SceneName, sceneItem.SceneItemId);
                    try
                    {
                        var tt = await _client.GetInputMute(sceneItem.SourceName);
                    }
                    catch { }
                    //foreach (var sourceItem in sourceItems)
                    //{
                    //    var sf2 = await _client.GetSourceFilter(sceneItem.SourceName, sourceItem.FilterName);
                    //}
                }
            }
        }

        private Task ObsConnectionClosed(object? sender, ConnectionClosedEventArgs e)
        {
            MessageBox.Show($"{e.WebSocketCloseCode}: {e.WebSocketCloseDescription}", "Connection Closed");
            return Task.CompletedTask;
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
            MessageBox.Show(recordDirectory, "GetRecordDirectory");
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
    }
}