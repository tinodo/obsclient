namespace SampleWindowsAppliation
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            button1 = new Button();
            tbHostname = new TextBox();
            nudPort = new NumericUpDown();
            tbPassword = new TextBox();
            btnStartVirtualCam = new Button();
            btnStopVirtualCamera = new Button();
            btnToggleVirtualCamera = new Button();
            btnGetVirtualCameraState = new Button();
            button3 = new Button();
            btnGetMonitorList = new Button();
            btnGetStudioModeEnabled = new Button();
            btnStopStudioMode = new Button();
            btnStartStudioMode = new Button();
            btnSleep = new Button();
            btnTriggerHotkeyByKeySequence = new Button();
            btnTriggerHotkeyByName = new Button();
            btnGetHotkeyList = new Button();
            btnCallVendorRequest = new Button();
            btnBroadcastCustomEvent = new Button();
            btnGetStats = new Button();
            btnGetVersion = new Button();
            btnGetProfileList = new Button();
            btnCreateSceneCollection = new Button();
            btnSetCurrentSceneCollection = new Button();
            btnGetSceneCollectionList = new Button();
            btnSetPersistentData = new Button();
            btnGetPersistentData = new Button();
            btnSetCurrentProfile = new Button();
            btnCreateProfile = new Button();
            btnRemoveProfile = new Button();
            btnGetProfileParameter = new Button();
            btnSetProfileParameter = new Button();
            btnGetVideoSettings = new Button();
            btnSetVideoSettings = new Button();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            tabControl1 = new TabControl();
            tpGeneralRequests = new TabPage();
            tpConfigRequests = new TabPage();
            btnGetRecordDirectory = new Button();
            btnSetStreamServiceSettings = new Button();
            btnGetStreamServiceSettings = new Button();
            tpSourcesRequests = new TabPage();
            btnSaveSourceScreenshot = new Button();
            btnSetSourceActive = new Button();
            btnGetSourceActive = new Button();
            tpScenesRequests = new TabPage();
            btnSetSceneSceneTransitionOverride = new Button();
            btnGetSceneSceneTransitionOverride = new Button();
            btnSetSceneName = new Button();
            btnRemoveScene = new Button();
            btnCreateScene = new Button();
            btnSetCurrentPreviewScene = new Button();
            btnGetCurrentPreviewScene = new Button();
            btnSetCurrentProgramScene = new Button();
            btnGetCurrentProgramScene = new Button();
            btnGetGroupList = new Button();
            btnGetSceneList = new Button();
            tpInputsRequests = new TabPage();
            btnToggleInputMute = new Button();
            btnSetInputMute = new Button();
            btnGetInputMute = new Button();
            btnSetInputSettings = new Button();
            btnGetInputSettings = new Button();
            btnGetInputDefaultSettings = new Button();
            btnSetInputName = new Button();
            btnRemoveInput = new Button();
            btnCreateInput = new Button();
            btnGetSpecialInputs = new Button();
            btnGetInputKindList = new Button();
            btnGetInputList = new Button();
            tpFiltersRequests = new TabPage();
            tbMediaInputsRequests = new TabPage();
            tpOutputsRequests = new TabPage();
            bnGetStreamStatus = new Button();
            tbRecordRequests = new TabPage();
            button2 = new Button();
            btnResumeRecord = new Button();
            btnPauseRecord = new Button();
            btnToggleRecordPause = new Button();
            btnStopRecord = new Button();
            btnStartRecord = new Button();
            btnToggleRecord = new Button();
            btnGetRecordStatus = new Button();
            tbSceneItemsRequests = new TabPage();
            tpStreamRequests = new TabPage();
            tpTransitionsRequests = new TabPage();
            tpUIRequests = new TabPage();
            btnOpenSourceProjectorWindow = new Button();
            button5 = new Button();
            btnOpenSourceProjectorOnMonitor = new Button();
            btnOpenVideoMixProjectorWindow = new Button();
            btnOpenVideoMixProjectorOnMonitor = new Button();
            btnOpenInputInteractDialog = new Button();
            btnOpenInputFiltersDialog = new Button();
            btnOpenInputPropertiesDialog = new Button();
            lbScenes = new ListBox();
            label4 = new Label();
            label5 = new Label();
            lbGroups = new ListBox();
            tbNameItem = new TextBox();
            label6 = new Label();
            label7 = new Label();
            lblRecordState = new Label();
            label8 = new Label();
            tbRecordFolder = new TextBox();
            ((System.ComponentModel.ISupportInitialize)nudPort).BeginInit();
            tabControl1.SuspendLayout();
            tpGeneralRequests.SuspendLayout();
            tpConfigRequests.SuspendLayout();
            tpSourcesRequests.SuspendLayout();
            tpScenesRequests.SuspendLayout();
            tpInputsRequests.SuspendLayout();
            tpOutputsRequests.SuspendLayout();
            tbRecordRequests.SuspendLayout();
            tpUIRequests.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(382, 8);
            button1.Name = "button1";
            button1.Size = new Size(150, 46);
            button1.TabIndex = 0;
            button1.Text = "Connect";
            button1.UseVisualStyleBackColor = true;
            button1.Click += this.button1_Click;
            // 
            // tbHostname
            // 
            tbHostname.Location = new Point(146, 15);
            tbHostname.Name = "tbHostname";
            tbHostname.Size = new Size(200, 39);
            tbHostname.TabIndex = 2;
            tbHostname.Text = "localhost";
            // 
            // nudPort
            // 
            nudPort.Location = new Point(146, 60);
            nudPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            nudPort.Name = "nudPort";
            nudPort.Size = new Size(200, 39);
            nudPort.TabIndex = 3;
            nudPort.Value = new decimal(new int[] { 4455, 0, 0, 0 });
            // 
            // tbPassword
            // 
            tbPassword.Location = new Point(146, 105);
            tbPassword.Name = "tbPassword";
            tbPassword.Size = new Size(200, 39);
            tbPassword.TabIndex = 4;
            tbPassword.UseSystemPasswordChar = true;
            // 
            // btnStartVirtualCam
            // 
            btnStartVirtualCam.Location = new Point(3, 108);
            btnStartVirtualCam.Name = "btnStartVirtualCam";
            btnStartVirtualCam.Size = new Size(340, 46);
            btnStartVirtualCam.TabIndex = 5;
            btnStartVirtualCam.Text = "Start Virtual Camera";
            btnStartVirtualCam.UseVisualStyleBackColor = true;
            btnStartVirtualCam.Click += this.btnStartVirtualCam_Click;
            // 
            // btnStopVirtualCamera
            // 
            btnStopVirtualCamera.Location = new Point(0, 160);
            btnStopVirtualCamera.Name = "btnStopVirtualCamera";
            btnStopVirtualCamera.Size = new Size(340, 46);
            btnStopVirtualCamera.TabIndex = 6;
            btnStopVirtualCamera.Text = "Stop Virtual Camera";
            btnStopVirtualCamera.UseVisualStyleBackColor = true;
            btnStopVirtualCamera.Click += this.btnStopVirtualCamera_Click;
            // 
            // btnToggleVirtualCamera
            // 
            btnToggleVirtualCamera.Location = new Point(3, 56);
            btnToggleVirtualCamera.Name = "btnToggleVirtualCamera";
            btnToggleVirtualCamera.Size = new Size(340, 46);
            btnToggleVirtualCamera.TabIndex = 7;
            btnToggleVirtualCamera.Text = "Toggle Virtual Camera";
            btnToggleVirtualCamera.UseVisualStyleBackColor = true;
            btnToggleVirtualCamera.Click += this.btnToggleVirtualCamera_Click;
            // 
            // btnGetVirtualCameraState
            // 
            btnGetVirtualCameraState.Location = new Point(3, 4);
            btnGetVirtualCameraState.Name = "btnGetVirtualCameraState";
            btnGetVirtualCameraState.Size = new Size(340, 46);
            btnGetVirtualCameraState.TabIndex = 8;
            btnGetVirtualCameraState.Text = "Get Virtual Camera Status";
            btnGetVirtualCameraState.UseVisualStyleBackColor = true;
            btnGetVirtualCameraState.Click += this.btnGetVirtualCameraState_Click;
            // 
            // button3
            // 
            button3.Location = new Point(382, 55);
            button3.Name = "button3";
            button3.Size = new Size(150, 46);
            button3.TabIndex = 9;
            button3.Text = "Disconnect";
            button3.UseVisualStyleBackColor = true;
            button3.Click += this.button3_Click;
            // 
            // btnGetMonitorList
            // 
            btnGetMonitorList.Location = new Point(3, 316);
            btnGetMonitorList.Name = "btnGetMonitorList";
            btnGetMonitorList.Size = new Size(340, 46);
            btnGetMonitorList.TabIndex = 13;
            btnGetMonitorList.Text = "Get Monitor List";
            btnGetMonitorList.UseVisualStyleBackColor = true;
            btnGetMonitorList.Click += this.btnGetMonitorList_Click;
            // 
            // btnGetStudioModeEnabled
            // 
            btnGetStudioModeEnabled.Location = new Point(3, 4);
            btnGetStudioModeEnabled.Name = "btnGetStudioModeEnabled";
            btnGetStudioModeEnabled.Size = new Size(340, 46);
            btnGetStudioModeEnabled.TabIndex = 12;
            btnGetStudioModeEnabled.Text = "Get Studio Mode Enabled";
            btnGetStudioModeEnabled.UseVisualStyleBackColor = true;
            btnGetStudioModeEnabled.Click += this.btnGetStudioModeEnabled_Click;
            // 
            // btnStopStudioMode
            // 
            btnStopStudioMode.Location = new Point(3, 108);
            btnStopStudioMode.Name = "btnStopStudioMode";
            btnStopStudioMode.Size = new Size(340, 46);
            btnStopStudioMode.TabIndex = 11;
            btnStopStudioMode.Text = "Stop Studio Mode";
            btnStopStudioMode.UseVisualStyleBackColor = true;
            btnStopStudioMode.Click += this.btnStopStudioMode_Click;
            // 
            // btnStartStudioMode
            // 
            btnStartStudioMode.Location = new Point(3, 56);
            btnStartStudioMode.Name = "btnStartStudioMode";
            btnStartStudioMode.Size = new Size(340, 46);
            btnStartStudioMode.TabIndex = 10;
            btnStartStudioMode.Text = "Start Studio Mode";
            btnStartStudioMode.UseVisualStyleBackColor = true;
            btnStartStudioMode.Click += this.btnStartStudioMode_Click;
            // 
            // btnSleep
            // 
            btnSleep.Enabled = false;
            btnSleep.Location = new Point(17, 330);
            btnSleep.Name = "btnSleep";
            btnSleep.Size = new Size(340, 46);
            btnSleep.TabIndex = 22;
            btnSleep.Text = "Sleep";
            btnSleep.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByKeySequence
            // 
            btnTriggerHotkeyByKeySequence.Enabled = false;
            btnTriggerHotkeyByKeySequence.Location = new Point(17, 278);
            btnTriggerHotkeyByKeySequence.Name = "btnTriggerHotkeyByKeySequence";
            btnTriggerHotkeyByKeySequence.Size = new Size(381, 46);
            btnTriggerHotkeyByKeySequence.TabIndex = 21;
            btnTriggerHotkeyByKeySequence.Text = "Trigger Hotkey By Key Sequence";
            btnTriggerHotkeyByKeySequence.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByName
            // 
            btnTriggerHotkeyByName.Enabled = false;
            btnTriggerHotkeyByName.Location = new Point(17, 226);
            btnTriggerHotkeyByName.Name = "btnTriggerHotkeyByName";
            btnTriggerHotkeyByName.Size = new Size(340, 46);
            btnTriggerHotkeyByName.TabIndex = 20;
            btnTriggerHotkeyByName.Text = "Trigger Hotkey By Name";
            btnTriggerHotkeyByName.UseVisualStyleBackColor = true;
            // 
            // btnGetHotkeyList
            // 
            btnGetHotkeyList.Location = new Point(17, 174);
            btnGetHotkeyList.Name = "btnGetHotkeyList";
            btnGetHotkeyList.Size = new Size(340, 46);
            btnGetHotkeyList.TabIndex = 19;
            btnGetHotkeyList.Text = "Get Hotkey List";
            btnGetHotkeyList.UseVisualStyleBackColor = true;
            btnGetHotkeyList.Click += this.btnGetHotkeyList_Click;
            // 
            // btnCallVendorRequest
            // 
            btnCallVendorRequest.Enabled = false;
            btnCallVendorRequest.Location = new Point(17, 122);
            btnCallVendorRequest.Name = "btnCallVendorRequest";
            btnCallVendorRequest.Size = new Size(340, 46);
            btnCallVendorRequest.TabIndex = 18;
            btnCallVendorRequest.Text = "Call Vendor Request";
            btnCallVendorRequest.UseVisualStyleBackColor = true;
            // 
            // btnBroadcastCustomEvent
            // 
            btnBroadcastCustomEvent.Enabled = false;
            btnBroadcastCustomEvent.Location = new Point(17, 70);
            btnBroadcastCustomEvent.Name = "btnBroadcastCustomEvent";
            btnBroadcastCustomEvent.Size = new Size(340, 46);
            btnBroadcastCustomEvent.TabIndex = 17;
            btnBroadcastCustomEvent.Text = "Broadcast Custom Event";
            btnBroadcastCustomEvent.UseVisualStyleBackColor = true;
            // 
            // btnGetStats
            // 
            btnGetStats.Location = new Point(194, 18);
            btnGetStats.Name = "btnGetStats";
            btnGetStats.Size = new Size(163, 46);
            btnGetStats.TabIndex = 16;
            btnGetStats.Text = "Get Stats";
            btnGetStats.UseVisualStyleBackColor = true;
            btnGetStats.Click += this.btnGetStats_Click;
            // 
            // btnGetVersion
            // 
            btnGetVersion.Location = new Point(17, 18);
            btnGetVersion.Name = "btnGetVersion";
            btnGetVersion.Size = new Size(163, 46);
            btnGetVersion.TabIndex = 15;
            btnGetVersion.Text = "Get Version";
            btnGetVersion.UseVisualStyleBackColor = true;
            btnGetVersion.Click += this.btnGetVersion_Click;
            // 
            // btnGetProfileList
            // 
            btnGetProfileList.Location = new Point(18, 278);
            btnGetProfileList.Name = "btnGetProfileList";
            btnGetProfileList.Size = new Size(340, 46);
            btnGetProfileList.TabIndex = 28;
            btnGetProfileList.Text = "Get Profile List";
            btnGetProfileList.UseVisualStyleBackColor = true;
            btnGetProfileList.Click += this.btnGetProfileList_Click;
            // 
            // btnCreateSceneCollection
            // 
            btnCreateSceneCollection.Enabled = false;
            btnCreateSceneCollection.Location = new Point(18, 226);
            btnCreateSceneCollection.Name = "btnCreateSceneCollection";
            btnCreateSceneCollection.Size = new Size(340, 46);
            btnCreateSceneCollection.TabIndex = 27;
            btnCreateSceneCollection.Text = "Create Scene Collection";
            btnCreateSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentSceneCollection
            // 
            btnSetCurrentSceneCollection.Enabled = false;
            btnSetCurrentSceneCollection.Location = new Point(18, 174);
            btnSetCurrentSceneCollection.Name = "btnSetCurrentSceneCollection";
            btnSetCurrentSceneCollection.Size = new Size(340, 46);
            btnSetCurrentSceneCollection.TabIndex = 26;
            btnSetCurrentSceneCollection.Text = "Set Current Scene Collection";
            btnSetCurrentSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneCollectionList
            // 
            btnGetSceneCollectionList.Location = new Point(18, 122);
            btnGetSceneCollectionList.Name = "btnGetSceneCollectionList";
            btnGetSceneCollectionList.Size = new Size(340, 46);
            btnGetSceneCollectionList.TabIndex = 25;
            btnGetSceneCollectionList.Text = "Get Scene Collection List";
            btnGetSceneCollectionList.UseVisualStyleBackColor = true;
            btnGetSceneCollectionList.Click += this.btnGetSceneCollectionList_Click;
            // 
            // btnSetPersistentData
            // 
            btnSetPersistentData.Enabled = false;
            btnSetPersistentData.Location = new Point(18, 70);
            btnSetPersistentData.Name = "btnSetPersistentData";
            btnSetPersistentData.Size = new Size(340, 46);
            btnSetPersistentData.TabIndex = 24;
            btnSetPersistentData.Text = "Set Persistent Data";
            btnSetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnGetPersistentData
            // 
            btnGetPersistentData.Enabled = false;
            btnGetPersistentData.Location = new Point(18, 18);
            btnGetPersistentData.Name = "btnGetPersistentData";
            btnGetPersistentData.Size = new Size(340, 46);
            btnGetPersistentData.TabIndex = 23;
            btnGetPersistentData.Text = "Get Persistent Data";
            btnGetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentProfile
            // 
            btnSetCurrentProfile.Enabled = false;
            btnSetCurrentProfile.Location = new Point(18, 330);
            btnSetCurrentProfile.Name = "btnSetCurrentProfile";
            btnSetCurrentProfile.Size = new Size(340, 46);
            btnSetCurrentProfile.TabIndex = 29;
            btnSetCurrentProfile.Text = "Set Current Profile";
            btnSetCurrentProfile.UseVisualStyleBackColor = true;
            // 
            // btnCreateProfile
            // 
            btnCreateProfile.Enabled = false;
            btnCreateProfile.Location = new Point(18, 382);
            btnCreateProfile.Name = "btnCreateProfile";
            btnCreateProfile.Size = new Size(340, 46);
            btnCreateProfile.TabIndex = 30;
            btnCreateProfile.Text = "Create Profile";
            btnCreateProfile.UseVisualStyleBackColor = true;
            // 
            // btnRemoveProfile
            // 
            btnRemoveProfile.Enabled = false;
            btnRemoveProfile.Location = new Point(364, 18);
            btnRemoveProfile.Name = "btnRemoveProfile";
            btnRemoveProfile.Size = new Size(340, 46);
            btnRemoveProfile.TabIndex = 31;
            btnRemoveProfile.Text = "Remove Profile";
            btnRemoveProfile.UseVisualStyleBackColor = true;
            // 
            // btnGetProfileParameter
            // 
            btnGetProfileParameter.Enabled = false;
            btnGetProfileParameter.Location = new Point(364, 70);
            btnGetProfileParameter.Name = "btnGetProfileParameter";
            btnGetProfileParameter.Size = new Size(340, 46);
            btnGetProfileParameter.TabIndex = 32;
            btnGetProfileParameter.Text = "Get Profile Parameter";
            btnGetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnSetProfileParameter
            // 
            btnSetProfileParameter.Enabled = false;
            btnSetProfileParameter.Location = new Point(364, 122);
            btnSetProfileParameter.Name = "btnSetProfileParameter";
            btnSetProfileParameter.Size = new Size(340, 46);
            btnSetProfileParameter.TabIndex = 33;
            btnSetProfileParameter.Text = "Set Profile Parameter";
            btnSetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnGetVideoSettings
            // 
            btnGetVideoSettings.Enabled = false;
            btnGetVideoSettings.Location = new Point(364, 174);
            btnGetVideoSettings.Name = "btnGetVideoSettings";
            btnGetVideoSettings.Size = new Size(340, 46);
            btnGetVideoSettings.TabIndex = 34;
            btnGetVideoSettings.Text = "Get Video Settings";
            btnGetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetVideoSettings
            // 
            btnSetVideoSettings.Enabled = false;
            btnSetVideoSettings.Location = new Point(364, 226);
            btnSetVideoSettings.Name = "btnSetVideoSettings";
            btnSetVideoSettings.Size = new Size(340, 46);
            btnSetVideoSettings.TabIndex = 35;
            btnSetVideoSettings.Text = "Set Video Settings";
            btnSetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 15);
            label1.Name = "label1";
            label1.Size = new Size(128, 32);
            label1.TabIndex = 16;
            label1.Text = "Hostname:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 62);
            label2.Name = "label2";
            label2.Size = new Size(61, 32);
            label2.TabIndex = 17;
            label2.Text = "Port:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 105);
            label3.Name = "label3";
            label3.Size = new Size(116, 32);
            label3.TabIndex = 18;
            label3.Text = "Password:";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tpGeneralRequests);
            tabControl1.Controls.Add(tpConfigRequests);
            tabControl1.Controls.Add(tpSourcesRequests);
            tabControl1.Controls.Add(tpScenesRequests);
            tabControl1.Controls.Add(tpInputsRequests);
            tabControl1.Controls.Add(tpFiltersRequests);
            tabControl1.Controls.Add(tbMediaInputsRequests);
            tabControl1.Controls.Add(tpOutputsRequests);
            tabControl1.Controls.Add(tbRecordRequests);
            tabControl1.Controls.Add(tbSceneItemsRequests);
            tabControl1.Controls.Add(tpStreamRequests);
            tabControl1.Controls.Add(tpTransitionsRequests);
            tabControl1.Controls.Add(tpUIRequests);
            tabControl1.Location = new Point(23, 261);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1257, 504);
            tabControl1.TabIndex = 19;
            // 
            // tpGeneralRequests
            // 
            tpGeneralRequests.Controls.Add(btnSleep);
            tpGeneralRequests.Controls.Add(btnGetVersion);
            tpGeneralRequests.Controls.Add(btnTriggerHotkeyByKeySequence);
            tpGeneralRequests.Controls.Add(btnGetStats);
            tpGeneralRequests.Controls.Add(btnTriggerHotkeyByName);
            tpGeneralRequests.Controls.Add(btnBroadcastCustomEvent);
            tpGeneralRequests.Controls.Add(btnGetHotkeyList);
            tpGeneralRequests.Controls.Add(btnCallVendorRequest);
            tpGeneralRequests.Location = new Point(8, 46);
            tpGeneralRequests.Name = "tpGeneralRequests";
            tpGeneralRequests.Padding = new Padding(3);
            tpGeneralRequests.Size = new Size(1241, 450);
            tpGeneralRequests.TabIndex = 0;
            tpGeneralRequests.Text = "General Requests";
            tpGeneralRequests.UseVisualStyleBackColor = true;
            // 
            // tpConfigRequests
            // 
            tpConfigRequests.Controls.Add(btnGetRecordDirectory);
            tpConfigRequests.Controls.Add(btnSetStreamServiceSettings);
            tpConfigRequests.Controls.Add(btnGetStreamServiceSettings);
            tpConfigRequests.Controls.Add(btnSetVideoSettings);
            tpConfigRequests.Controls.Add(btnGetPersistentData);
            tpConfigRequests.Controls.Add(btnGetVideoSettings);
            tpConfigRequests.Controls.Add(btnSetPersistentData);
            tpConfigRequests.Controls.Add(btnSetProfileParameter);
            tpConfigRequests.Controls.Add(btnGetSceneCollectionList);
            tpConfigRequests.Controls.Add(btnGetProfileParameter);
            tpConfigRequests.Controls.Add(btnSetCurrentSceneCollection);
            tpConfigRequests.Controls.Add(btnRemoveProfile);
            tpConfigRequests.Controls.Add(btnCreateSceneCollection);
            tpConfigRequests.Controls.Add(btnCreateProfile);
            tpConfigRequests.Controls.Add(btnGetProfileList);
            tpConfigRequests.Controls.Add(btnSetCurrentProfile);
            tpConfigRequests.Location = new Point(8, 46);
            tpConfigRequests.Name = "tpConfigRequests";
            tpConfigRequests.Padding = new Padding(3);
            tpConfigRequests.Size = new Size(1241, 450);
            tpConfigRequests.TabIndex = 1;
            tpConfigRequests.Text = "Config Requests";
            tpConfigRequests.UseVisualStyleBackColor = true;
            // 
            // btnGetRecordDirectory
            // 
            btnGetRecordDirectory.Location = new Point(364, 382);
            btnGetRecordDirectory.Name = "btnGetRecordDirectory";
            btnGetRecordDirectory.Size = new Size(340, 46);
            btnGetRecordDirectory.TabIndex = 38;
            btnGetRecordDirectory.Text = "Get Record Directory";
            btnGetRecordDirectory.UseVisualStyleBackColor = true;
            btnGetRecordDirectory.Click += this.btnGetRecordDirectory_Click;
            // 
            // btnSetStreamServiceSettings
            // 
            btnSetStreamServiceSettings.Enabled = false;
            btnSetStreamServiceSettings.Location = new Point(364, 330);
            btnSetStreamServiceSettings.Name = "btnSetStreamServiceSettings";
            btnSetStreamServiceSettings.Size = new Size(340, 46);
            btnSetStreamServiceSettings.TabIndex = 37;
            btnSetStreamServiceSettings.Text = "Set Stream Service Settings";
            btnSetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetStreamServiceSettings
            // 
            btnGetStreamServiceSettings.Enabled = false;
            btnGetStreamServiceSettings.Location = new Point(364, 278);
            btnGetStreamServiceSettings.Name = "btnGetStreamServiceSettings";
            btnGetStreamServiceSettings.Size = new Size(340, 46);
            btnGetStreamServiceSettings.TabIndex = 36;
            btnGetStreamServiceSettings.Text = "Get Stream Service Settings";
            btnGetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // tpSourcesRequests
            // 
            tpSourcesRequests.Controls.Add(btnSaveSourceScreenshot);
            tpSourcesRequests.Controls.Add(btnSetSourceActive);
            tpSourcesRequests.Controls.Add(btnGetSourceActive);
            tpSourcesRequests.Location = new Point(8, 46);
            tpSourcesRequests.Name = "tpSourcesRequests";
            tpSourcesRequests.Size = new Size(1241, 450);
            tpSourcesRequests.TabIndex = 2;
            tpSourcesRequests.Text = "Sources Requests";
            tpSourcesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSaveSourceScreenshot
            // 
            btnSaveSourceScreenshot.Enabled = false;
            btnSaveSourceScreenshot.Location = new Point(3, 107);
            btnSaveSourceScreenshot.Name = "btnSaveSourceScreenshot";
            btnSaveSourceScreenshot.Size = new Size(340, 46);
            btnSaveSourceScreenshot.TabIndex = 22;
            btnSaveSourceScreenshot.Text = "Save Source Screenshot";
            btnSaveSourceScreenshot.UseVisualStyleBackColor = true;
            // 
            // btnSetSourceActive
            // 
            btnSetSourceActive.Enabled = false;
            btnSetSourceActive.Location = new Point(3, 55);
            btnSetSourceActive.Name = "btnSetSourceActive";
            btnSetSourceActive.Size = new Size(340, 46);
            btnSetSourceActive.TabIndex = 21;
            btnSetSourceActive.Text = "Set Source Active";
            btnSetSourceActive.UseVisualStyleBackColor = true;
            // 
            // btnGetSourceActive
            // 
            btnGetSourceActive.Enabled = false;
            btnGetSourceActive.Location = new Point(3, 3);
            btnGetSourceActive.Name = "btnGetSourceActive";
            btnGetSourceActive.Size = new Size(340, 46);
            btnGetSourceActive.TabIndex = 20;
            btnGetSourceActive.Text = "Get Source Active";
            btnGetSourceActive.UseVisualStyleBackColor = true;
            // 
            // tpScenesRequests
            // 
            tpScenesRequests.Controls.Add(btnSetSceneSceneTransitionOverride);
            tpScenesRequests.Controls.Add(btnGetSceneSceneTransitionOverride);
            tpScenesRequests.Controls.Add(btnSetSceneName);
            tpScenesRequests.Controls.Add(btnRemoveScene);
            tpScenesRequests.Controls.Add(btnCreateScene);
            tpScenesRequests.Controls.Add(btnSetCurrentPreviewScene);
            tpScenesRequests.Controls.Add(btnGetCurrentPreviewScene);
            tpScenesRequests.Controls.Add(btnSetCurrentProgramScene);
            tpScenesRequests.Controls.Add(btnGetCurrentProgramScene);
            tpScenesRequests.Controls.Add(btnGetGroupList);
            tpScenesRequests.Controls.Add(btnGetSceneList);
            tpScenesRequests.Location = new Point(8, 46);
            tpScenesRequests.Name = "tpScenesRequests";
            tpScenesRequests.Size = new Size(1241, 450);
            tpScenesRequests.TabIndex = 3;
            tpScenesRequests.Text = "Scenes Requests";
            tpScenesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneSceneTransitionOverride
            // 
            btnSetSceneSceneTransitionOverride.Enabled = false;
            btnSetSceneSceneTransitionOverride.Location = new Point(351, 107);
            btnSetSceneSceneTransitionOverride.Name = "btnSetSceneSceneTransitionOverride";
            btnSetSceneSceneTransitionOverride.Size = new Size(460, 46);
            btnSetSceneSceneTransitionOverride.TabIndex = 30;
            btnSetSceneSceneTransitionOverride.Text = "Set Scene Scene Transition Override";
            btnSetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneSceneTransitionOverride
            // 
            btnGetSceneSceneTransitionOverride.Enabled = false;
            btnGetSceneSceneTransitionOverride.Location = new Point(351, 55);
            btnGetSceneSceneTransitionOverride.Name = "btnGetSceneSceneTransitionOverride";
            btnGetSceneSceneTransitionOverride.Size = new Size(460, 46);
            btnGetSceneSceneTransitionOverride.TabIndex = 29;
            btnGetSceneSceneTransitionOverride.Text = "Get Scene Scene Transition Override";
            btnGetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneName
            // 
            btnSetSceneName.Location = new Point(351, 3);
            btnSetSceneName.Name = "btnSetSceneName";
            btnSetSceneName.Size = new Size(340, 46);
            btnSetSceneName.TabIndex = 28;
            btnSetSceneName.Text = "Set Scene Name";
            btnSetSceneName.UseVisualStyleBackColor = true;
            btnSetSceneName.Click += this.btnSetSceneName_Click;
            // 
            // btnRemoveScene
            // 
            btnRemoveScene.Location = new Point(3, 367);
            btnRemoveScene.Name = "btnRemoveScene";
            btnRemoveScene.Size = new Size(340, 46);
            btnRemoveScene.TabIndex = 27;
            btnRemoveScene.Text = "Remove Scene";
            btnRemoveScene.UseVisualStyleBackColor = true;
            btnRemoveScene.Click += this.btnRemoveScene_Click;
            // 
            // btnCreateScene
            // 
            btnCreateScene.Location = new Point(3, 315);
            btnCreateScene.Name = "btnCreateScene";
            btnCreateScene.Size = new Size(340, 46);
            btnCreateScene.TabIndex = 26;
            btnCreateScene.Text = "Create Scene";
            btnCreateScene.UseVisualStyleBackColor = true;
            btnCreateScene.Click += this.btnCreateScene_Click;
            // 
            // btnSetCurrentPreviewScene
            // 
            btnSetCurrentPreviewScene.Location = new Point(3, 263);
            btnSetCurrentPreviewScene.Name = "btnSetCurrentPreviewScene";
            btnSetCurrentPreviewScene.Size = new Size(340, 46);
            btnSetCurrentPreviewScene.TabIndex = 25;
            btnSetCurrentPreviewScene.Text = "Set Current Preview Scene";
            btnSetCurrentPreviewScene.UseVisualStyleBackColor = true;
            btnSetCurrentPreviewScene.Click += this.btnSetCurrentPreviewScene_Click;
            // 
            // btnGetCurrentPreviewScene
            // 
            btnGetCurrentPreviewScene.Location = new Point(3, 211);
            btnGetCurrentPreviewScene.Name = "btnGetCurrentPreviewScene";
            btnGetCurrentPreviewScene.Size = new Size(340, 46);
            btnGetCurrentPreviewScene.TabIndex = 24;
            btnGetCurrentPreviewScene.Text = "Get Current Preview Scene";
            btnGetCurrentPreviewScene.UseVisualStyleBackColor = true;
            btnGetCurrentPreviewScene.Click += this.btnGetCurrentPreviewScene_Click;
            // 
            // btnSetCurrentProgramScene
            // 
            btnSetCurrentProgramScene.Location = new Point(3, 159);
            btnSetCurrentProgramScene.Name = "btnSetCurrentProgramScene";
            btnSetCurrentProgramScene.Size = new Size(340, 46);
            btnSetCurrentProgramScene.TabIndex = 23;
            btnSetCurrentProgramScene.Text = "Set Current Program Scene";
            btnSetCurrentProgramScene.UseVisualStyleBackColor = true;
            btnSetCurrentProgramScene.Click += this.btnSetCurrentProgramScene_Click;
            // 
            // btnGetCurrentProgramScene
            // 
            btnGetCurrentProgramScene.Location = new Point(3, 107);
            btnGetCurrentProgramScene.Name = "btnGetCurrentProgramScene";
            btnGetCurrentProgramScene.Size = new Size(340, 46);
            btnGetCurrentProgramScene.TabIndex = 22;
            btnGetCurrentProgramScene.Text = "Get Current Program Scene";
            btnGetCurrentProgramScene.UseVisualStyleBackColor = true;
            btnGetCurrentProgramScene.Click += this.btnGetCurrentProgramScene_Click;
            // 
            // btnGetGroupList
            // 
            btnGetGroupList.Location = new Point(3, 55);
            btnGetGroupList.Name = "btnGetGroupList";
            btnGetGroupList.Size = new Size(340, 46);
            btnGetGroupList.TabIndex = 21;
            btnGetGroupList.Text = "Get Group List";
            btnGetGroupList.UseVisualStyleBackColor = true;
            btnGetGroupList.Click += this.btnGetGroupList_Click;
            // 
            // btnGetSceneList
            // 
            btnGetSceneList.Location = new Point(3, 3);
            btnGetSceneList.Name = "btnGetSceneList";
            btnGetSceneList.Size = new Size(340, 46);
            btnGetSceneList.TabIndex = 20;
            btnGetSceneList.Text = "Get Scene List";
            btnGetSceneList.UseVisualStyleBackColor = true;
            btnGetSceneList.Click += this.btnGetSceneList_Click;
            // 
            // tpInputsRequests
            // 
            tpInputsRequests.Controls.Add(btnToggleInputMute);
            tpInputsRequests.Controls.Add(btnSetInputMute);
            tpInputsRequests.Controls.Add(btnGetInputMute);
            tpInputsRequests.Controls.Add(btnSetInputSettings);
            tpInputsRequests.Controls.Add(btnGetInputSettings);
            tpInputsRequests.Controls.Add(btnGetInputDefaultSettings);
            tpInputsRequests.Controls.Add(btnSetInputName);
            tpInputsRequests.Controls.Add(btnRemoveInput);
            tpInputsRequests.Controls.Add(btnCreateInput);
            tpInputsRequests.Controls.Add(btnGetSpecialInputs);
            tpInputsRequests.Controls.Add(btnGetInputKindList);
            tpInputsRequests.Controls.Add(btnGetInputList);
            tpInputsRequests.Location = new Point(8, 46);
            tpInputsRequests.Name = "tpInputsRequests";
            tpInputsRequests.Size = new Size(1241, 450);
            tpInputsRequests.TabIndex = 4;
            tpInputsRequests.Text = "Inputs Requests";
            tpInputsRequests.UseVisualStyleBackColor = true;
            // 
            // btnToggleInputMute
            // 
            btnToggleInputMute.Enabled = false;
            btnToggleInputMute.Location = new Point(351, 159);
            btnToggleInputMute.Name = "btnToggleInputMute";
            btnToggleInputMute.Size = new Size(340, 46);
            btnToggleInputMute.TabIndex = 32;
            btnToggleInputMute.Text = "Toggle Input Mute";
            btnToggleInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputMute
            // 
            btnSetInputMute.Enabled = false;
            btnSetInputMute.Location = new Point(351, 107);
            btnSetInputMute.Name = "btnSetInputMute";
            btnSetInputMute.Size = new Size(340, 46);
            btnSetInputMute.TabIndex = 31;
            btnSetInputMute.Text = "Set Input Mute";
            btnSetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnGetInputMute
            // 
            btnGetInputMute.Enabled = false;
            btnGetInputMute.Location = new Point(351, 55);
            btnGetInputMute.Name = "btnGetInputMute";
            btnGetInputMute.Size = new Size(340, 46);
            btnGetInputMute.TabIndex = 30;
            btnGetInputMute.Text = "Get Input Mute";
            btnGetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputSettings
            // 
            btnSetInputSettings.Enabled = false;
            btnSetInputSettings.Location = new Point(351, 3);
            btnSetInputSettings.Name = "btnSetInputSettings";
            btnSetInputSettings.Size = new Size(340, 46);
            btnSetInputSettings.TabIndex = 29;
            btnSetInputSettings.Text = "Set Input Settings";
            btnSetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputSettings
            // 
            btnGetInputSettings.Enabled = false;
            btnGetInputSettings.Location = new Point(3, 367);
            btnGetInputSettings.Name = "btnGetInputSettings";
            btnGetInputSettings.Size = new Size(340, 46);
            btnGetInputSettings.TabIndex = 28;
            btnGetInputSettings.Text = "Get Input Settings";
            btnGetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputDefaultSettings
            // 
            btnGetInputDefaultSettings.Enabled = false;
            btnGetInputDefaultSettings.Location = new Point(3, 315);
            btnGetInputDefaultSettings.Name = "btnGetInputDefaultSettings";
            btnGetInputDefaultSettings.Size = new Size(340, 46);
            btnGetInputDefaultSettings.TabIndex = 27;
            btnGetInputDefaultSettings.Text = "Get Input Default Settings";
            btnGetInputDefaultSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetInputName
            // 
            btnSetInputName.Enabled = false;
            btnSetInputName.Location = new Point(3, 263);
            btnSetInputName.Name = "btnSetInputName";
            btnSetInputName.Size = new Size(340, 46);
            btnSetInputName.TabIndex = 26;
            btnSetInputName.Text = "Set Input Name";
            btnSetInputName.UseVisualStyleBackColor = true;
            // 
            // btnRemoveInput
            // 
            btnRemoveInput.Enabled = false;
            btnRemoveInput.Location = new Point(3, 211);
            btnRemoveInput.Name = "btnRemoveInput";
            btnRemoveInput.Size = new Size(340, 46);
            btnRemoveInput.TabIndex = 25;
            btnRemoveInput.Text = "Remove Input";
            btnRemoveInput.UseVisualStyleBackColor = true;
            // 
            // btnCreateInput
            // 
            btnCreateInput.Enabled = false;
            btnCreateInput.Location = new Point(0, 159);
            btnCreateInput.Name = "btnCreateInput";
            btnCreateInput.Size = new Size(340, 46);
            btnCreateInput.TabIndex = 24;
            btnCreateInput.Text = "Create Input";
            btnCreateInput.UseVisualStyleBackColor = true;
            // 
            // btnGetSpecialInputs
            // 
            btnGetSpecialInputs.Location = new Point(0, 107);
            btnGetSpecialInputs.Name = "btnGetSpecialInputs";
            btnGetSpecialInputs.Size = new Size(340, 46);
            btnGetSpecialInputs.TabIndex = 23;
            btnGetSpecialInputs.Text = "Get Special Inputs";
            btnGetSpecialInputs.UseVisualStyleBackColor = true;
            btnGetSpecialInputs.Click += this.btnGetSpecialInputs_Click;
            // 
            // btnGetInputKindList
            // 
            btnGetInputKindList.Location = new Point(3, 55);
            btnGetInputKindList.Name = "btnGetInputKindList";
            btnGetInputKindList.Size = new Size(340, 46);
            btnGetInputKindList.TabIndex = 22;
            btnGetInputKindList.Text = "Get Input Kind List";
            btnGetInputKindList.UseVisualStyleBackColor = true;
            btnGetInputKindList.Click += this.btnGetInputKindList_Click;
            // 
            // btnGetInputList
            // 
            btnGetInputList.Location = new Point(3, 3);
            btnGetInputList.Name = "btnGetInputList";
            btnGetInputList.Size = new Size(340, 46);
            btnGetInputList.TabIndex = 21;
            btnGetInputList.Text = "Get Input List";
            btnGetInputList.UseVisualStyleBackColor = true;
            btnGetInputList.Click += this.btnGetInputList_Click;
            // 
            // tpFiltersRequests
            // 
            tpFiltersRequests.Location = new Point(8, 46);
            tpFiltersRequests.Name = "tpFiltersRequests";
            tpFiltersRequests.Size = new Size(1241, 450);
            tpFiltersRequests.TabIndex = 5;
            tpFiltersRequests.Text = "Filters Requests";
            tpFiltersRequests.UseVisualStyleBackColor = true;
            // 
            // tbMediaInputsRequests
            // 
            tbMediaInputsRequests.Location = new Point(8, 46);
            tbMediaInputsRequests.Name = "tbMediaInputsRequests";
            tbMediaInputsRequests.Size = new Size(1241, 450);
            tbMediaInputsRequests.TabIndex = 6;
            tbMediaInputsRequests.Text = "Media Inputs Requests";
            tbMediaInputsRequests.UseVisualStyleBackColor = true;
            // 
            // tpOutputsRequests
            // 
            tpOutputsRequests.Controls.Add(bnGetStreamStatus);
            tpOutputsRequests.Controls.Add(btnGetVirtualCameraState);
            tpOutputsRequests.Controls.Add(btnToggleVirtualCamera);
            tpOutputsRequests.Controls.Add(btnStartVirtualCam);
            tpOutputsRequests.Controls.Add(btnStopVirtualCamera);
            tpOutputsRequests.Location = new Point(8, 46);
            tpOutputsRequests.Name = "tpOutputsRequests";
            tpOutputsRequests.Size = new Size(1241, 450);
            tpOutputsRequests.TabIndex = 7;
            tpOutputsRequests.Text = "Outputs Requests";
            tpOutputsRequests.UseVisualStyleBackColor = true;
            // 
            // bnGetStreamStatus
            // 
            bnGetStreamStatus.Location = new Point(0, 212);
            bnGetStreamStatus.Name = "bnGetStreamStatus";
            bnGetStreamStatus.Size = new Size(340, 46);
            bnGetStreamStatus.TabIndex = 9;
            bnGetStreamStatus.Text = "Get Stream Status";
            bnGetStreamStatus.UseVisualStyleBackColor = true;
            bnGetStreamStatus.Click += this.bnGetStreamStatus_Click;
            // 
            // tbRecordRequests
            // 
            tbRecordRequests.Controls.Add(button2);
            tbRecordRequests.Controls.Add(btnResumeRecord);
            tbRecordRequests.Controls.Add(btnPauseRecord);
            tbRecordRequests.Controls.Add(btnToggleRecordPause);
            tbRecordRequests.Controls.Add(btnStopRecord);
            tbRecordRequests.Controls.Add(btnStartRecord);
            tbRecordRequests.Controls.Add(btnToggleRecord);
            tbRecordRequests.Controls.Add(btnGetRecordStatus);
            tbRecordRequests.Location = new Point(8, 46);
            tbRecordRequests.Name = "tbRecordRequests";
            tbRecordRequests.Size = new Size(1241, 450);
            tbRecordRequests.TabIndex = 8;
            tbRecordRequests.Text = "Record Requests";
            tbRecordRequests.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            button2.Location = new Point(3, 368);
            button2.Name = "button2";
            button2.Size = new Size(340, 46);
            button2.TabIndex = 16;
            button2.Text = "Set Record Directory";
            button2.UseVisualStyleBackColor = true;
            button2.Click += this.button2_Click;
            // 
            // btnResumeRecord
            // 
            btnResumeRecord.Location = new Point(3, 316);
            btnResumeRecord.Name = "btnResumeRecord";
            btnResumeRecord.Size = new Size(340, 46);
            btnResumeRecord.TabIndex = 15;
            btnResumeRecord.Text = "Resume Record";
            btnResumeRecord.UseVisualStyleBackColor = true;
            btnResumeRecord.Click += this.btnResumeRecord_Click;
            // 
            // btnPauseRecord
            // 
            btnPauseRecord.Location = new Point(3, 264);
            btnPauseRecord.Name = "btnPauseRecord";
            btnPauseRecord.Size = new Size(340, 46);
            btnPauseRecord.TabIndex = 14;
            btnPauseRecord.Text = "Pause Record";
            btnPauseRecord.UseVisualStyleBackColor = true;
            btnPauseRecord.Click += this.btnPauseRecord_Click;
            // 
            // btnToggleRecordPause
            // 
            btnToggleRecordPause.Location = new Point(3, 212);
            btnToggleRecordPause.Name = "btnToggleRecordPause";
            btnToggleRecordPause.Size = new Size(340, 46);
            btnToggleRecordPause.TabIndex = 13;
            btnToggleRecordPause.Text = "Toggle Record Pause";
            btnToggleRecordPause.UseVisualStyleBackColor = true;
            btnToggleRecordPause.Click += this.btnToggleRecordPause_Click;
            // 
            // btnStopRecord
            // 
            btnStopRecord.Location = new Point(3, 160);
            btnStopRecord.Name = "btnStopRecord";
            btnStopRecord.Size = new Size(340, 46);
            btnStopRecord.TabIndex = 12;
            btnStopRecord.Text = "Stop Record";
            btnStopRecord.UseVisualStyleBackColor = true;
            btnStopRecord.Click += this.btnStopRecord_Click;
            // 
            // btnStartRecord
            // 
            btnStartRecord.Location = new Point(3, 108);
            btnStartRecord.Name = "btnStartRecord";
            btnStartRecord.Size = new Size(340, 46);
            btnStartRecord.TabIndex = 11;
            btnStartRecord.Text = "Start Record";
            btnStartRecord.UseVisualStyleBackColor = true;
            btnStartRecord.Click += this.btnStartRecord_Click;
            // 
            // btnToggleRecord
            // 
            btnToggleRecord.Location = new Point(3, 56);
            btnToggleRecord.Name = "btnToggleRecord";
            btnToggleRecord.Size = new Size(340, 46);
            btnToggleRecord.TabIndex = 10;
            btnToggleRecord.Text = "Toggle Record";
            btnToggleRecord.UseVisualStyleBackColor = true;
            btnToggleRecord.Click += this.btnToggleRecord_Click;
            // 
            // btnGetRecordStatus
            // 
            btnGetRecordStatus.Location = new Point(3, 4);
            btnGetRecordStatus.Name = "btnGetRecordStatus";
            btnGetRecordStatus.Size = new Size(340, 46);
            btnGetRecordStatus.TabIndex = 9;
            btnGetRecordStatus.Text = "Get Record Status";
            btnGetRecordStatus.UseVisualStyleBackColor = true;
            btnGetRecordStatus.Click += this.btnGetRecordStatus_Click;
            // 
            // tbSceneItemsRequests
            // 
            tbSceneItemsRequests.Location = new Point(8, 46);
            tbSceneItemsRequests.Name = "tbSceneItemsRequests";
            tbSceneItemsRequests.Size = new Size(1241, 450);
            tbSceneItemsRequests.TabIndex = 9;
            tbSceneItemsRequests.Text = "Scene Items Requests";
            tbSceneItemsRequests.UseVisualStyleBackColor = true;
            // 
            // tpStreamRequests
            // 
            tpStreamRequests.Location = new Point(8, 46);
            tpStreamRequests.Name = "tpStreamRequests";
            tpStreamRequests.Size = new Size(1241, 450);
            tpStreamRequests.TabIndex = 10;
            tpStreamRequests.Text = "Stream Requests";
            tpStreamRequests.UseVisualStyleBackColor = true;
            // 
            // tpTransitionsRequests
            // 
            tpTransitionsRequests.Location = new Point(8, 46);
            tpTransitionsRequests.Name = "tpTransitionsRequests";
            tpTransitionsRequests.Size = new Size(1241, 450);
            tpTransitionsRequests.TabIndex = 11;
            tpTransitionsRequests.Text = "Transitions Requests";
            tpTransitionsRequests.UseVisualStyleBackColor = true;
            // 
            // tpUIRequests
            // 
            tpUIRequests.Controls.Add(btnOpenSourceProjectorWindow);
            tpUIRequests.Controls.Add(button5);
            tpUIRequests.Controls.Add(btnOpenSourceProjectorOnMonitor);
            tpUIRequests.Controls.Add(btnOpenVideoMixProjectorWindow);
            tpUIRequests.Controls.Add(btnOpenVideoMixProjectorOnMonitor);
            tpUIRequests.Controls.Add(btnOpenInputInteractDialog);
            tpUIRequests.Controls.Add(btnOpenInputFiltersDialog);
            tpUIRequests.Controls.Add(btnOpenInputPropertiesDialog);
            tpUIRequests.Controls.Add(btnGetStudioModeEnabled);
            tpUIRequests.Controls.Add(btnStartStudioMode);
            tpUIRequests.Controls.Add(btnStopStudioMode);
            tpUIRequests.Controls.Add(btnGetMonitorList);
            tpUIRequests.Location = new Point(8, 46);
            tpUIRequests.Name = "tpUIRequests";
            tpUIRequests.Size = new Size(1241, 450);
            tpUIRequests.TabIndex = 12;
            tpUIRequests.Text = "UI Requests";
            tpUIRequests.UseVisualStyleBackColor = true;
            // 
            // btnOpenSourceProjectorWindow
            // 
            btnOpenSourceProjectorWindow.Location = new Point(351, 212);
            btnOpenSourceProjectorWindow.Name = "btnOpenSourceProjectorWindow";
            btnOpenSourceProjectorWindow.Size = new Size(340, 46);
            btnOpenSourceProjectorWindow.TabIndex = 20;
            btnOpenSourceProjectorWindow.Text = "OpenSourceProjectorWindow";
            btnOpenSourceProjectorWindow.UseVisualStyleBackColor = true;
            // 
            // button5
            // 
            button5.Location = new Point(349, 160);
            button5.Name = "button5";
            button5.Size = new Size(340, 46);
            button5.TabIndex = 19;
            button5.Text = "OpenVideoMixProjectorOnMonitor";
            button5.UseVisualStyleBackColor = true;
            // 
            // btnOpenSourceProjectorOnMonitor
            // 
            btnOpenSourceProjectorOnMonitor.Location = new Point(351, 108);
            btnOpenSourceProjectorOnMonitor.Name = "btnOpenSourceProjectorOnMonitor";
            btnOpenSourceProjectorOnMonitor.Size = new Size(340, 46);
            btnOpenSourceProjectorOnMonitor.TabIndex = 18;
            btnOpenSourceProjectorOnMonitor.Text = "OpenSourceProjectorOnMonitor";
            btnOpenSourceProjectorOnMonitor.UseVisualStyleBackColor = true;
            // 
            // btnOpenVideoMixProjectorWindow
            // 
            btnOpenVideoMixProjectorWindow.Location = new Point(351, 56);
            btnOpenVideoMixProjectorWindow.Name = "btnOpenVideoMixProjectorWindow";
            btnOpenVideoMixProjectorWindow.Size = new Size(340, 46);
            btnOpenVideoMixProjectorWindow.TabIndex = 17;
            btnOpenVideoMixProjectorWindow.Text = "OpenVideoMixProjectorWindow";
            btnOpenVideoMixProjectorWindow.UseVisualStyleBackColor = true;
            // 
            // btnOpenVideoMixProjectorOnMonitor
            // 
            btnOpenVideoMixProjectorOnMonitor.Location = new Point(351, 4);
            btnOpenVideoMixProjectorOnMonitor.Name = "btnOpenVideoMixProjectorOnMonitor";
            btnOpenVideoMixProjectorOnMonitor.Size = new Size(340, 46);
            btnOpenVideoMixProjectorOnMonitor.TabIndex = 16;
            btnOpenVideoMixProjectorOnMonitor.Text = "OpenVideoMixProjectorOnMonitor";
            btnOpenVideoMixProjectorOnMonitor.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputInteractDialog
            // 
            btnOpenInputInteractDialog.Location = new Point(3, 264);
            btnOpenInputInteractDialog.Name = "btnOpenInputInteractDialog";
            btnOpenInputInteractDialog.Size = new Size(340, 46);
            btnOpenInputInteractDialog.TabIndex = 15;
            btnOpenInputInteractDialog.Text = "OpenInputInteractDialog";
            btnOpenInputInteractDialog.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputFiltersDialog
            // 
            btnOpenInputFiltersDialog.Location = new Point(3, 212);
            btnOpenInputFiltersDialog.Name = "btnOpenInputFiltersDialog";
            btnOpenInputFiltersDialog.Size = new Size(340, 46);
            btnOpenInputFiltersDialog.TabIndex = 14;
            btnOpenInputFiltersDialog.Text = "OpenInputFiltersDialog";
            btnOpenInputFiltersDialog.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputPropertiesDialog
            // 
            btnOpenInputPropertiesDialog.Location = new Point(3, 160);
            btnOpenInputPropertiesDialog.Name = "btnOpenInputPropertiesDialog";
            btnOpenInputPropertiesDialog.Size = new Size(340, 46);
            btnOpenInputPropertiesDialog.TabIndex = 13;
            btnOpenInputPropertiesDialog.Text = "OpenInputPropertiesDialog";
            btnOpenInputPropertiesDialog.UseVisualStyleBackColor = true;
            // 
            // lbScenes
            // 
            lbScenes.FormattingEnabled = true;
            lbScenes.ItemHeight = 32;
            lbScenes.Location = new Point(1307, 311);
            lbScenes.Name = "lbScenes";
            lbScenes.Size = new Size(288, 452);
            lbScenes.TabIndex = 20;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(1307, 261);
            label4.Name = "label4";
            label4.Size = new Size(93, 32);
            label4.TabIndex = 21;
            label4.Text = "Scenes:";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(1601, 260);
            label5.Name = "label5";
            label5.Size = new Size(95, 32);
            label5.TabIndex = 23;
            label5.Text = "Groups:";
            // 
            // lbGroups
            // 
            lbGroups.FormattingEnabled = true;
            lbGroups.ItemHeight = 32;
            lbGroups.Location = new Point(1601, 310);
            lbGroups.Name = "lbGroups";
            lbGroups.Size = new Size(288, 452);
            lbGroups.TabIndex = 22;
            // 
            // tbNameItem
            // 
            tbNameItem.Location = new Point(167, 192);
            tbNameItem.Name = "tbNameItem";
            tbNameItem.Size = new Size(200, 39);
            tbNameItem.TabIndex = 24;
            tbNameItem.Text = "Some Item Name";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(23, 195);
            label6.Name = "label6";
            label6.Size = new Size(138, 32);
            label6.TabIndex = 25;
            label6.Text = "Item Name:";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(1307, 22);
            label7.Name = "label7";
            label7.Size = new Size(152, 32);
            label7.TabIndex = 26;
            label7.Text = "Record State:";
            // 
            // lblRecordState
            // 
            lblRecordState.AutoSize = true;
            lblRecordState.Location = new Point(1465, 22);
            lblRecordState.Name = "lblRecordState";
            lblRecordState.Size = new Size(56, 32);
            lblRecordState.TabIndex = 27;
            lblRecordState.Text = "N/A";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(1307, 60);
            label8.Name = "label8";
            label8.Size = new Size(166, 32);
            label8.TabIndex = 28;
            label8.Text = "Record Folder:";
            // 
            // tbRecordFolder
            // 
            tbRecordFolder.Location = new Point(1479, 57);
            tbRecordFolder.Name = "tbRecordFolder";
            tbRecordFolder.Size = new Size(200, 39);
            tbRecordFolder.TabIndex = 29;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new SizeF(13F, 32F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1986, 849);
            this.Controls.Add(tbRecordFolder);
            this.Controls.Add(label8);
            this.Controls.Add(lblRecordState);
            this.Controls.Add(label7);
            this.Controls.Add(label6);
            this.Controls.Add(tbNameItem);
            this.Controls.Add(label5);
            this.Controls.Add(lbGroups);
            this.Controls.Add(label4);
            this.Controls.Add(lbScenes);
            this.Controls.Add(tabControl1);
            this.Controls.Add(label3);
            this.Controls.Add(label2);
            this.Controls.Add(label1);
            this.Controls.Add(button3);
            this.Controls.Add(tbPassword);
            this.Controls.Add(nudPort);
            this.Controls.Add(tbHostname);
            this.Controls.Add(button1);
            this.Name = "Form1";
            this.Text = "OBS Client - Sample Windows Application";
            this.Load += this.Form1_Load;
            ((System.ComponentModel.ISupportInitialize)nudPort).EndInit();
            tabControl1.ResumeLayout(false);
            tpGeneralRequests.ResumeLayout(false);
            tpConfigRequests.ResumeLayout(false);
            tpSourcesRequests.ResumeLayout(false);
            tpScenesRequests.ResumeLayout(false);
            tpInputsRequests.ResumeLayout(false);
            tpOutputsRequests.ResumeLayout(false);
            tbRecordRequests.ResumeLayout(false);
            tpUIRequests.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Button button1;
        private TextBox tbHostname;
        private NumericUpDown nudPort;
        private TextBox tbPassword;
        private Button btnStartVirtualCam;
        private Button btnStopVirtualCamera;
        private Button btnToggleVirtualCamera;
        private Button btnGetVirtualCameraState;
        private Button button3;
        private Button btnGetMonitorList;
        private Button btnGetStudioModeEnabled;
        private Button btnStopStudioMode;
        private Button btnStartStudioMode;
        private Button btnGetVersion;
        private Button btnGetStats;
        private Button btnGetHotkeyList;
        private Button btnCallVendorRequest;
        private Button btnBroadcastCustomEvent;
        private Button btnSleep;
        private Button btnTriggerHotkeyByKeySequence;
        private Button btnTriggerHotkeyByName;
        private Button btnGetSceneCollectionList;
        private Button btnSetPersistentData;
        private Button btnGetPersistentData;
        private Button btnSetCurrentSceneCollection;
        private Button btnGetProfileList;
        private Button btnCreateSceneCollection;
        private Button btnSetVideoSettings;
        private Button btnGetVideoSettings;
        private Button btnSetProfileParameter;
        private Button btnGetProfileParameter;
        private Button btnRemoveProfile;
        private Button btnCreateProfile;
        private Button btnSetCurrentProfile;
        private Label label1;
        private Label label2;
        private Label label3;
        private TabControl tabControl1;
        private TabPage tpGeneralRequests;
        private TabPage tpConfigRequests;
        private Button btnGetRecordDirectory;
        private Button btnSetStreamServiceSettings;
        private Button btnGetStreamServiceSettings;
        private TabPage tpSourcesRequests;
        private TabPage tpScenesRequests;
        private TabPage tpInputsRequests;
        private TabPage tpFiltersRequests;
        private Button btnSaveSourceScreenshot;
        private Button btnSetSourceActive;
        private Button btnGetSourceActive;
        private Button btnGetSceneList;
        private Button btnGetGroupList;
        private Button btnGetCurrentProgramScene;
        private Button btnSetCurrentPreviewScene;
        private Button btnGetCurrentPreviewScene;
        private Button btnSetCurrentProgramScene;
        private Button btnRemoveScene;
        private Button btnCreateScene;
        private Button btnGetSceneSceneTransitionOverride;
        private Button btnSetSceneName;
        private Button btnSetSceneSceneTransitionOverride;
        private Button btnGetInputList;
        private Button btnGetInputKindList;
        private Button btnGetSpecialInputs;
        private Button btnGetInputSettings;
        private Button btnGetInputDefaultSettings;
        private Button btnSetInputName;
        private Button btnRemoveInput;
        private Button btnCreateInput;
        private Button btnGetInputMute;
        private Button btnSetInputSettings;
        private Button btnToggleInputMute;
        private Button btnSetInputMute;
        private ListBox lbScenes;
        private Label label4;
        private Label label5;
        private ListBox lbGroups;
        private TextBox tbNameItem;
        private Label label6;
        private TabPage tbMediaInputsRequests;
        private TabPage tpOutputsRequests;
        private TabPage tbRecordRequests;
        private TabPage tbSceneItemsRequests;
        private TabPage tpStreamRequests;
        private TabPage tpTransitionsRequests;
        private TabPage tpUIRequests;
        private Button btnOpenInputPropertiesDialog;
        private Button btnOpenInputFiltersDialog;
        private Button btnOpenInputInteractDialog;
        private Button btnOpenVideoMixProjectorOnMonitor;
        private Button btnOpenSourceProjectorWindow;
        private Button button5;
        private Button btnOpenSourceProjectorOnMonitor;
        private Button btnOpenVideoMixProjectorWindow;
        private Button bnGetStreamStatus;
        private Button btnResumeRecord;
        private Button btnPauseRecord;
        private Button btnToggleRecordPause;
        private Button btnStopRecord;
        private Button btnStartRecord;
        private Button btnToggleRecord;
        private Button btnGetRecordStatus;
        private Label label7;
        private Label lblRecordState;
        private Button button2;
        private Label label8;
        private TextBox tbRecordFolder;
    }
}