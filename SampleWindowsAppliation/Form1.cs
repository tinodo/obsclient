namespace SampleWindowsAppliation
{
    using System.ComponentModel;
    using System.Diagnostics;
    using OBSStudioClient;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.MessageClasses;

    public partial class Form1 : Form
    {
        private OBSClient _client = new();
        private delegate void SafeListboxRemove(object itemToRemove, ListBox listBox);
        private delegate void SafeListboxRefresh(ListBox listBox);

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

        }

        private void PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
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
        }

        private Task ObsConnectionClosed(object? sender, ConnectionClosedEventArgs e)
        {
            MessageBox.Show($"{e.WebSocketCloseCode}: {e.WebSocketCloseDescription}", "Connection Closed");
            return Task.CompletedTask;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                //var i = _client.GetInputKindList();
                //foreach (var j in i)
                //{
                //    var result = _client.GetInputDefaultSettings("wasapi_input_capture");
                //}
                //var x = _client.GetOutputList();

                //var result = _client.GetOutputSettings(x[0].OutputName);
                //_client.SetOutputSettings(x[0].OutputName, new() { { "path", @"C:\Users\tinodo\Videos" } });
                //var result2 = _client.GetOutputSettings(x[0].OutputName);
                //MessageBox.Show(result.ToString());



                //var result1 = _client.GetVersion();
                //var result2 = _client.GetStats();
                //var result3 = _client.GetHotkeyList();
                //_client.TriggerHotkeyByKeySequence(Enums.ObsKey.OBS_KEY_A, Enums.KeyModifier.Control | Enums.KeyModifier.Shift);


                //var result = _client.GetSceneItemTransform("Presentation Scene", 2);

                //var result1 = _client.GetSceneItemList("Presentation Scene");
                //var result2 = _client.GetGroupSceneItemList("Group 1");
                //var result3 = _client.GetSceneItemId("Presentation Scene", "Presentation Screen");
                //var result4 = _client.CreateSceneItem("Presentation Scene", "Virtual Break");
                //_client.RemoveSceneItem("Presentation Scene", result4);
                //var result6 = _client.DuplicateSceneItem("Presentation Scene", 4);
                //var result7 = _client.GetSceneItemTransform("Presentation Scene", 4);
                //_client.SetSceneItemTransform("Presentation Scene", 4, result7);
                //var result9 = _client.GetSceneItemEnabled("Presentation Scene", 4);
                //_client.SetSceneItemEnabled("Presentation Scene", 4, false);
                //var result11 = _client.GetSceneItemLocked("Presentation Scene", 4);
                //_client.SetSceneItemLocked("Presentation Scene", 4, true);
                //var result13 = _client.GetSceneItemIndex("Presentation Scene", 4);
                //_client.SetSceneItemIndex("Presentation Scene", 4, 0);
                //var result15 = _client.GetSceneItemBlendMode("Presentation Scene", 4);
                //_client.SetSceneItemBlendMode("Presentation Scene", 4, Enums.BlendMode.OBS_BLEND_NORMAL);

                //List<RequestMessage> requests = new();
                //int? sleepFrames = null;
                //RequestMessage r1 = new(RequestType.StartVirtualCam, Guid.NewGuid().ToString());
                //RequestMessage r2 = new(RequestType.Sleep, Guid.NewGuid().ToString(), new { sleepMillis = 2000, sleepFrames});
                //RequestMessage r3 = new(RequestType.SetStudioModeEnabled, Guid.NewGuid().ToString(), new { studioModeEnabled = true });
                //requests.Add(r1);
                //requests.Add(r2);
                //requests.Add(r3);
                //_client.SendRequestBatch(RequestBatchExecutionType.SerialRealtime, requests.ToArray());
                //var result = _client.GetSourceFilter("Moveable Camera Intermediate Scene", "Move Camera Left Top");

                //_client.Reidentify(Enums.EventSubscription.None);
                //var geo = _client.GetQtGeometry(0, 0, 20, 599, 399, false, false);
                //var monitors = _client.GetMonitorList();
                ////var geo = _client.GetQtGeometry(1, -2000, 120, 999, 599, false, false, monitors[1].MonitorWidth);
                //var geo = OBSClient.GetGeometry(1, -2000, 200, 840, 480, false, false, monitors[1].MonitorWidth);
                //_client.OpenSourceProjectorWindow("Moveable Camera Intermediate Scene", geo);
                //_client.OpenSourceProjector("Moveable Camera Intermediate Scene", 1, geo);
                //geo = _client.GetQtGeometry(1, 100, 120, 499, 599, false, false, monitors[1].MonitorWidth);
                //_client.OpenSourceProjector("Moveable Camera Intermediate Scene", 1, geo);
                //geo = _client.GetQtGeometry(2, 100, 120, 499, 599, false, false, monitors[2].MonitorWidth);
                //_client.OpenSourceProjector("Moveable Camera Intermediate Scene", 1, geo);
                //geo = _client.GetQtGeometry(3, 100, 120, 499, 599, false, false, monitors[3].MonitorWidth);
                //_client.OpenSourceProjector("Moveable Camera Intermediate Scene", 1, geo);

                //var xxx = _client.GetPersistentData(Enums.Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE, "[General]");
                var result = _client.GetInputKindList();
                foreach (var ik in result)
                {
                    var result1 = _client.GetInputDefaultSettings(ik);
                }
            }
            catch (OBSResponseException error)
            {
                MessageBox.Show($"{error.ErrorCode}: {error.Message}", "OBS Response Exception");
            }
        }

        private void btnStartVirtualCam_Click(object sender, EventArgs e)
        {
            _client.StartVirtualCam();
        }

        private void btnStopVirtualCamera_Click(object sender, EventArgs e)
        {
            _client.StopVirtualCam();
        }

        private void btnToggleVirtualCamera_Click(object sender, EventArgs e)
        {
            var result = _client.ToggleVirtualCam();
            MessageBox.Show(result.ToString(), "ToggleVirtualCam");
        }

        private void btnGetVirtualCameraState_Click(object sender, EventArgs e)
        {
            var result = _client.GetVirtualCamStatus();
            MessageBox.Show(result.ToString(), "GetVirtualCamStatus");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _client.Disconnect();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            _client.SetStudioModeEnabled(true);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            _client.SetStudioModeEnabled(false);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            var result = _client.GetStudioModeEnabled();
            MessageBox.Show(result.ToString(), "GetStudioModeEnabled");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var monitors = _client.GetMonitorList();
            var result = string.Empty;
            foreach (var monitor in monitors)
            {
                result += $"{monitor.MonitorIndex}: {monitor.MonitorName} ({monitor.MonitorWidth}x{monitor.MonitorHeight}) at {monitor.MonitorPositionX},{monitor.MonitorPositionY}" + Environment.NewLine;
            }

            MessageBox.Show(result, "GetMonitorList");
        }

        private void btnGetVersion_Click(object sender, EventArgs e)
        {
            var version = _client.GetVersion();
            var result = $"OBS Studio {version.ObsVersion} ({version.Platform} - {version.PlatformDescription})" + Environment.NewLine;
            result += $"WebSockets {version.ObsWebSocketVersion} - RPC Version {version.RpcVersion}" + Environment.NewLine;
            result += $"Requests: {string.Join(", ", version.AvailableRequests)}" + Environment.NewLine;
            result += $"Image Formats: {string.Join(", ", version.SupportedImageFormats)}" + Environment.NewLine;
            MessageBox.Show(result, "GetVersion");
        }

        private void btnGetStats_Click(object sender, EventArgs e)
        {
            var stats = _client.GetStats();
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

        private void btnGetHotkeyList_Click(object sender, EventArgs e)
        {
            var hotkeyList = _client.GetHotkeyList();
            MessageBox.Show(string.Join(", ", hotkeyList), "GetHotkeyList");
        }

        private void btnGetSceneCollectionList_Click(object sender, EventArgs e)
        {
            var sceneCollectionList = _client.GetSceneCollectionList();
            var result = $"CurrentSceneCollectionName: {sceneCollectionList.CurrentSceneCollectionName}" + Environment.NewLine;
            result += $"SceneCollections: {string.Join(", ", sceneCollectionList.SceneCollections)}";
            MessageBox.Show(result, "GetSceneCollectionList");
        }

        private void btnGetProfileList_Click(object sender, EventArgs e)
        {
            var profileList = _client.GetProfileList();
            var result = $"CurrentProfileName: {profileList.CurrentProfileName}" + Environment.NewLine;
            result += $"Profiles: {string.Join(", ", profileList.Profiles)}";
            MessageBox.Show(result, "GetProfileList");
        }

        private void btnGetRecordDirectory_Click(object sender, EventArgs e)
        {
            var recordDirectory = _client.GetRecordDirectory();
            MessageBox.Show(recordDirectory, "GetRecordDirectory");
        }

        private void btnGetSceneList_Click(object sender, EventArgs e)
        {
            var sceneList = _client.GetSceneList();
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

        private void btnGetGroupList_Click(object sender, EventArgs e)
        {
            var groupList = _client.GetGroupList();
            MessageBox.Show(string.Join(", ", groupList), "GetGroupList");
            this.lbGroups.DataSource = groupList;
        }

        private void btnGetCurrentProgramScene_Click(object sender, EventArgs e)
        {
            var result = _client.GetCurrentProgramScene();
            MessageBox.Show(result, "GetCurrentProgramScene");
        }

        private void btnGetCurrentPreviewScene_Click(object sender, EventArgs e)
        {
            var result = _client.GetCurrentPreviewScene();
            MessageBox.Show(result, "GetCurrentPreviewScene");
        }

        private void btnGetInputList_Click(object sender, EventArgs e)
        {
            var inputs = _client.GetInputList();
            var result = "Inputs:" + Environment.NewLine;
            foreach (var input in inputs)
            {
                result += $"Input name: {input.InputName}, Input Kind: {input.InputKind}, Unversioned Input Kind: {input.UnversionedInputKind}" + Environment.NewLine;
            }

            MessageBox.Show(result, "GetInputList");
        }

        private void btnGetInputKindList_Click(object sender, EventArgs e)
        {
            var inputKindList = _client.GetInputKindList();
            MessageBox.Show("Input Kinds: " + string.Join(", ", inputKindList), "GetInputKindList");
        }

        private void btnGetSpecialInputs_Click(object sender, EventArgs e)
        {
            var specialInputs = _client.GetSpecialInputs();
            var result = $"Mic1: {specialInputs.Mic1}" + Environment.NewLine;
            result += $"Mic2: {specialInputs.Mic2}" + Environment.NewLine;
            result += $"Mic3: {specialInputs.Mic3}" + Environment.NewLine;
            result += $"Mic4: {specialInputs.Mic4}" + Environment.NewLine;
            result += $"Desktop1: {specialInputs.Desktop1}" + Environment.NewLine;
            result += $"Desktop2: {specialInputs.Desktop2}" + Environment.NewLine;
            MessageBox.Show(result, "GetSpecialInputs");
        }

        private void btnSetCurrentProgramScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                _client.SetCurrentProgramScene(scene.SceneName);
            }
        }

        private void btnSetCurrentPreviewScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                _client.SetCurrentPreviewScene(scene.SceneName);
            }
        }

        private void btnRemoveScene_Click(object sender, EventArgs e)
        {
            if (this.lbScenes.SelectedItem is not Scene scene)
            {
                MessageBox.Show("Select a Scene first.");
            }
            else
            {
                _client.RemoveScene(scene.SceneName);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.lbScenes.DisplayMember = "SceneName";
        }

        private void btnCreateScene_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.tbNameItem.Text))
            {
                MessageBox.Show("Enter an item name first.");
            }
            else
            {
                _client.CreateScene(this.tbNameItem.Text);
            }
        }

        private void btnSetSceneName_Click(object sender, EventArgs e)
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

            _client.SetSceneName(scene.SceneName, this.tbNameItem.Text);
        }
    }
}