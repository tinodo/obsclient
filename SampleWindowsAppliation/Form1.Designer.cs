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
            this.button1 = new Button();
            this.tbHostname = new TextBox();
            this.nudPort = new NumericUpDown();
            this.tbPassword = new TextBox();
            this.btnStartVirtualCam = new Button();
            this.btnStopVirtualCamera = new Button();
            this.btnToggleVirtualCamera = new Button();
            this.btnGetVirtualCameraState = new Button();
            this.button3 = new Button();
            this.btnGetMonitorList = new Button();
            this.btnGetStudioModeEnabled = new Button();
            this.btnStopStudioMode = new Button();
            this.btnStartStudioMode = new Button();
            this.btnSleep = new Button();
            this.btnTriggerHotkeyByKeySequence = new Button();
            this.btnTriggerHotkeyByName = new Button();
            this.btnGetHotkeyList = new Button();
            this.btnCallVendorRequest = new Button();
            this.btnBroadcastCustomEvent = new Button();
            this.btnGetStats = new Button();
            this.btnGetVersion = new Button();
            this.btnGetProfileList = new Button();
            this.btnCreateSceneCollection = new Button();
            this.btnSetCurrentSceneCollection = new Button();
            this.btnGetSceneCollectionList = new Button();
            this.btnSetPersistentData = new Button();
            this.btnGetPersistentData = new Button();
            this.btnSetCurrentProfile = new Button();
            this.btnCreateProfile = new Button();
            this.btnRemoveProfile = new Button();
            this.btnGetProfileParameter = new Button();
            this.btnSetProfileParameter = new Button();
            this.btnGetVideoSettings = new Button();
            this.btnSetVideoSettings = new Button();
            this.label1 = new Label();
            this.label2 = new Label();
            this.label3 = new Label();
            this.tabControl1 = new TabControl();
            this.tpGeneralRequests = new TabPage();
            this.tpConfigRequests = new TabPage();
            this.btnGetRecordDirectory = new Button();
            this.btnSetStreamServiceSettings = new Button();
            this.btnGetStreamServiceSettings = new Button();
            this.tpSourcesRequests = new TabPage();
            this.btnSaveSourceScreenshot = new Button();
            this.btnSetSourceActive = new Button();
            this.btnGetSourceActive = new Button();
            this.tpScenesRequests = new TabPage();
            this.btnSetSceneSceneTransitionOverride = new Button();
            this.btnGetSceneSceneTransitionOverride = new Button();
            this.btnSetSceneName = new Button();
            this.btnRemoveScene = new Button();
            this.btnCreateScene = new Button();
            this.btnSetCurrentPreviewScene = new Button();
            this.btnGetCurrentPreviewScene = new Button();
            this.btnSetCurrentProgramScene = new Button();
            this.btnGetCurrentProgramScene = new Button();
            this.btnGetGroupList = new Button();
            this.btnGetSceneList = new Button();
            this.tpInputsRequests = new TabPage();
            this.btnToggleInputMute = new Button();
            this.btnSetInputMute = new Button();
            this.btnGetInputMute = new Button();
            this.btnSetInputSettings = new Button();
            this.btnGetInputSettings = new Button();
            this.btnGetInputDefaultSettings = new Button();
            this.btnSetInputName = new Button();
            this.btnRemoveInput = new Button();
            this.btnCreateInput = new Button();
            this.btnGetSpecialInputs = new Button();
            this.btnGetInputKindList = new Button();
            this.btnGetInputList = new Button();
            this.tpFiltersRequests = new TabPage();
            this.tbMediaInputsRequests = new TabPage();
            this.tpOutputsRequests = new TabPage();
            this.bnGetStreamStatus = new Button();
            this.tbRecordRequests = new TabPage();
            this.btnSetRecordDirectory = new Button();
            this.btnResumeRecord = new Button();
            this.btnPauseRecord = new Button();
            this.btnToggleRecordPause = new Button();
            this.btnStopRecord = new Button();
            this.btnStartRecord = new Button();
            this.btnToggleRecord = new Button();
            this.btnGetRecordStatus = new Button();
            this.tbSceneItemsRequests = new TabPage();
            this.tpStreamRequests = new TabPage();
            this.tpTransitionsRequests = new TabPage();
            this.tpUIRequests = new TabPage();
            this.btnOpenSourceProjectorWindow = new Button();
            this.button5 = new Button();
            this.btnOpenSourceProjectorOnMonitor = new Button();
            this.btnOpenVideoMixProjectorWindow = new Button();
            this.btnOpenVideoMixProjectorOnMonitor = new Button();
            this.btnOpenInputInteractDialog = new Button();
            this.btnOpenInputFiltersDialog = new Button();
            this.btnOpenInputPropertiesDialog = new Button();
            this.tpMisc = new TabPage();
            this.btnBatch1 = new Button();
            this.lbScenes = new ListBox();
            this.label4 = new Label();
            this.label5 = new Label();
            this.lbGroups = new ListBox();
            this.tbNameItem = new TextBox();
            this.label6 = new Label();
            this.label7 = new Label();
            this.lblRecordState = new Label();
            this.label8 = new Label();
            this.tbRecordFolder = new TextBox();
            this.cbAutoReconnect = new CheckBox();
            this.lblBytesOut = new Label();
            this.label10 = new Label();
            this.lblBytesIn = new Label();
            this.label12 = new Label();
            this.lblMessagesIn = new Label();
            this.label11 = new Label();
            this.lblMessagesOut = new Label();
            this.label14 = new Label();
            this.label9 = new Label();
            this.lblLastEvent = new Label();
            ((System.ComponentModel.ISupportInitialize)this.nudPort).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tpGeneralRequests.SuspendLayout();
            this.tpConfigRequests.SuspendLayout();
            this.tpSourcesRequests.SuspendLayout();
            this.tpScenesRequests.SuspendLayout();
            this.tpInputsRequests.SuspendLayout();
            this.tpOutputsRequests.SuspendLayout();
            this.tbRecordRequests.SuspendLayout();
            this.tpUIRequests.SuspendLayout();
            this.tpMisc.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new Point(294, 6);
            this.button1.Margin = new Padding(2);
            this.button1.Name = "button1";
            this.button1.Size = new Size(115, 36);
            this.button1.TabIndex = 0;
            this.button1.Text = "Connect";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += this.Button1_Click;
            // 
            // tbHostname
            // 
            this.tbHostname.Location = new Point(112, 12);
            this.tbHostname.Margin = new Padding(2);
            this.tbHostname.Name = "tbHostname";
            this.tbHostname.Size = new Size(155, 31);
            this.tbHostname.TabIndex = 2;
            this.tbHostname.Text = "localhost";
            // 
            // nudPort
            // 
            this.nudPort.Location = new Point(112, 47);
            this.nudPort.Margin = new Padding(2);
            this.nudPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.nudPort.Name = "nudPort";
            this.nudPort.Size = new Size(154, 31);
            this.nudPort.TabIndex = 3;
            this.nudPort.Value = new decimal(new int[] { 4455, 0, 0, 0 });
            // 
            // tbPassword
            // 
            this.tbPassword.Location = new Point(112, 82);
            this.tbPassword.Margin = new Padding(2);
            this.tbPassword.Name = "tbPassword";
            this.tbPassword.Size = new Size(155, 31);
            this.tbPassword.TabIndex = 4;
            this.tbPassword.UseSystemPasswordChar = true;
            // 
            // btnStartVirtualCam
            // 
            this.btnStartVirtualCam.Location = new Point(2, 84);
            this.btnStartVirtualCam.Margin = new Padding(2);
            this.btnStartVirtualCam.Name = "btnStartVirtualCam";
            this.btnStartVirtualCam.Size = new Size(262, 36);
            this.btnStartVirtualCam.TabIndex = 5;
            this.btnStartVirtualCam.Text = "Start Virtual Camera";
            this.btnStartVirtualCam.UseVisualStyleBackColor = true;
            this.btnStartVirtualCam.Click += this.BtnStartVirtualCam_Click;
            // 
            // btnStopVirtualCamera
            // 
            this.btnStopVirtualCamera.Location = new Point(0, 125);
            this.btnStopVirtualCamera.Margin = new Padding(2);
            this.btnStopVirtualCamera.Name = "btnStopVirtualCamera";
            this.btnStopVirtualCamera.Size = new Size(262, 36);
            this.btnStopVirtualCamera.TabIndex = 6;
            this.btnStopVirtualCamera.Text = "Stop Virtual Camera";
            this.btnStopVirtualCamera.UseVisualStyleBackColor = true;
            this.btnStopVirtualCamera.Click += this.BtnStopVirtualCamera_Click;
            // 
            // btnToggleVirtualCamera
            // 
            this.btnToggleVirtualCamera.Location = new Point(2, 44);
            this.btnToggleVirtualCamera.Margin = new Padding(2);
            this.btnToggleVirtualCamera.Name = "btnToggleVirtualCamera";
            this.btnToggleVirtualCamera.Size = new Size(262, 36);
            this.btnToggleVirtualCamera.TabIndex = 7;
            this.btnToggleVirtualCamera.Text = "Toggle Virtual Camera";
            this.btnToggleVirtualCamera.UseVisualStyleBackColor = true;
            this.btnToggleVirtualCamera.Click += this.BtnToggleVirtualCamera_Click;
            // 
            // btnGetVirtualCameraState
            // 
            this.btnGetVirtualCameraState.Location = new Point(2, 3);
            this.btnGetVirtualCameraState.Margin = new Padding(2);
            this.btnGetVirtualCameraState.Name = "btnGetVirtualCameraState";
            this.btnGetVirtualCameraState.Size = new Size(262, 36);
            this.btnGetVirtualCameraState.TabIndex = 8;
            this.btnGetVirtualCameraState.Text = "Get Virtual Camera Status";
            this.btnGetVirtualCameraState.UseVisualStyleBackColor = true;
            this.btnGetVirtualCameraState.Click += this.BtnGetVirtualCameraState_Click;
            // 
            // button3
            // 
            this.button3.Location = new Point(294, 43);
            this.button3.Margin = new Padding(2);
            this.button3.Name = "button3";
            this.button3.Size = new Size(115, 36);
            this.button3.TabIndex = 9;
            this.button3.Text = "Disconnect";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += this.Button3_Click;
            // 
            // btnGetMonitorList
            // 
            this.btnGetMonitorList.Location = new Point(2, 247);
            this.btnGetMonitorList.Margin = new Padding(2);
            this.btnGetMonitorList.Name = "btnGetMonitorList";
            this.btnGetMonitorList.Size = new Size(262, 36);
            this.btnGetMonitorList.TabIndex = 13;
            this.btnGetMonitorList.Text = "Get Monitor List";
            this.btnGetMonitorList.UseVisualStyleBackColor = true;
            this.btnGetMonitorList.Click += this.BtnGetMonitorList_Click;
            // 
            // btnGetStudioModeEnabled
            // 
            this.btnGetStudioModeEnabled.Location = new Point(2, 3);
            this.btnGetStudioModeEnabled.Margin = new Padding(2);
            this.btnGetStudioModeEnabled.Name = "btnGetStudioModeEnabled";
            this.btnGetStudioModeEnabled.Size = new Size(262, 36);
            this.btnGetStudioModeEnabled.TabIndex = 12;
            this.btnGetStudioModeEnabled.Text = "Get Studio Mode Enabled";
            this.btnGetStudioModeEnabled.UseVisualStyleBackColor = true;
            this.btnGetStudioModeEnabled.Click += this.BtnGetStudioModeEnabled_Click;
            // 
            // btnStopStudioMode
            // 
            this.btnStopStudioMode.Location = new Point(2, 84);
            this.btnStopStudioMode.Margin = new Padding(2);
            this.btnStopStudioMode.Name = "btnStopStudioMode";
            this.btnStopStudioMode.Size = new Size(262, 36);
            this.btnStopStudioMode.TabIndex = 11;
            this.btnStopStudioMode.Text = "Stop Studio Mode";
            this.btnStopStudioMode.UseVisualStyleBackColor = true;
            this.btnStopStudioMode.Click += this.BtnStopStudioMode_Click;
            // 
            // btnStartStudioMode
            // 
            this.btnStartStudioMode.Location = new Point(2, 44);
            this.btnStartStudioMode.Margin = new Padding(2);
            this.btnStartStudioMode.Name = "btnStartStudioMode";
            this.btnStartStudioMode.Size = new Size(262, 36);
            this.btnStartStudioMode.TabIndex = 10;
            this.btnStartStudioMode.Text = "Start Studio Mode";
            this.btnStartStudioMode.UseVisualStyleBackColor = true;
            this.btnStartStudioMode.Click += this.BtnStartStudioMode_Click;
            // 
            // btnSleep
            // 
            this.btnSleep.Enabled = false;
            this.btnSleep.Location = new Point(13, 258);
            this.btnSleep.Margin = new Padding(2);
            this.btnSleep.Name = "btnSleep";
            this.btnSleep.Size = new Size(262, 36);
            this.btnSleep.TabIndex = 22;
            this.btnSleep.Text = "Sleep";
            this.btnSleep.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByKeySequence
            // 
            this.btnTriggerHotkeyByKeySequence.Enabled = false;
            this.btnTriggerHotkeyByKeySequence.Location = new Point(13, 217);
            this.btnTriggerHotkeyByKeySequence.Margin = new Padding(2);
            this.btnTriggerHotkeyByKeySequence.Name = "btnTriggerHotkeyByKeySequence";
            this.btnTriggerHotkeyByKeySequence.Size = new Size(293, 36);
            this.btnTriggerHotkeyByKeySequence.TabIndex = 21;
            this.btnTriggerHotkeyByKeySequence.Text = "Trigger Hotkey By Key Sequence";
            this.btnTriggerHotkeyByKeySequence.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByName
            // 
            this.btnTriggerHotkeyByName.Enabled = false;
            this.btnTriggerHotkeyByName.Location = new Point(13, 177);
            this.btnTriggerHotkeyByName.Margin = new Padding(2);
            this.btnTriggerHotkeyByName.Name = "btnTriggerHotkeyByName";
            this.btnTriggerHotkeyByName.Size = new Size(262, 36);
            this.btnTriggerHotkeyByName.TabIndex = 20;
            this.btnTriggerHotkeyByName.Text = "Trigger Hotkey By Name";
            this.btnTriggerHotkeyByName.UseVisualStyleBackColor = true;
            // 
            // btnGetHotkeyList
            // 
            this.btnGetHotkeyList.Location = new Point(13, 136);
            this.btnGetHotkeyList.Margin = new Padding(2);
            this.btnGetHotkeyList.Name = "btnGetHotkeyList";
            this.btnGetHotkeyList.Size = new Size(262, 36);
            this.btnGetHotkeyList.TabIndex = 19;
            this.btnGetHotkeyList.Text = "Get Hotkey List";
            this.btnGetHotkeyList.UseVisualStyleBackColor = true;
            this.btnGetHotkeyList.Click += this.BtnGetHotkeyList_Click;
            // 
            // btnCallVendorRequest
            // 
            this.btnCallVendorRequest.Enabled = false;
            this.btnCallVendorRequest.Location = new Point(13, 95);
            this.btnCallVendorRequest.Margin = new Padding(2);
            this.btnCallVendorRequest.Name = "btnCallVendorRequest";
            this.btnCallVendorRequest.Size = new Size(262, 36);
            this.btnCallVendorRequest.TabIndex = 18;
            this.btnCallVendorRequest.Text = "Call Vendor Request";
            this.btnCallVendorRequest.UseVisualStyleBackColor = true;
            // 
            // btnBroadcastCustomEvent
            // 
            this.btnBroadcastCustomEvent.Enabled = false;
            this.btnBroadcastCustomEvent.Location = new Point(13, 55);
            this.btnBroadcastCustomEvent.Margin = new Padding(2);
            this.btnBroadcastCustomEvent.Name = "btnBroadcastCustomEvent";
            this.btnBroadcastCustomEvent.Size = new Size(262, 36);
            this.btnBroadcastCustomEvent.TabIndex = 17;
            this.btnBroadcastCustomEvent.Text = "Broadcast Custom Event";
            this.btnBroadcastCustomEvent.UseVisualStyleBackColor = true;
            // 
            // btnGetStats
            // 
            this.btnGetStats.Location = new Point(149, 14);
            this.btnGetStats.Margin = new Padding(2);
            this.btnGetStats.Name = "btnGetStats";
            this.btnGetStats.Size = new Size(125, 36);
            this.btnGetStats.TabIndex = 16;
            this.btnGetStats.Text = "Get Stats";
            this.btnGetStats.UseVisualStyleBackColor = true;
            this.btnGetStats.Click += this.BtnGetStats_Click;
            // 
            // btnGetVersion
            // 
            this.btnGetVersion.Location = new Point(13, 14);
            this.btnGetVersion.Margin = new Padding(2);
            this.btnGetVersion.Name = "btnGetVersion";
            this.btnGetVersion.Size = new Size(125, 36);
            this.btnGetVersion.TabIndex = 15;
            this.btnGetVersion.Text = "Get Version";
            this.btnGetVersion.UseVisualStyleBackColor = true;
            this.btnGetVersion.Click += this.BtnGetVersion_Click;
            // 
            // btnGetProfileList
            // 
            this.btnGetProfileList.Location = new Point(14, 217);
            this.btnGetProfileList.Margin = new Padding(2);
            this.btnGetProfileList.Name = "btnGetProfileList";
            this.btnGetProfileList.Size = new Size(262, 36);
            this.btnGetProfileList.TabIndex = 28;
            this.btnGetProfileList.Text = "Get Profile List";
            this.btnGetProfileList.UseVisualStyleBackColor = true;
            this.btnGetProfileList.Click += this.BtnGetProfileList_Click;
            // 
            // btnCreateSceneCollection
            // 
            this.btnCreateSceneCollection.Enabled = false;
            this.btnCreateSceneCollection.Location = new Point(14, 177);
            this.btnCreateSceneCollection.Margin = new Padding(2);
            this.btnCreateSceneCollection.Name = "btnCreateSceneCollection";
            this.btnCreateSceneCollection.Size = new Size(262, 36);
            this.btnCreateSceneCollection.TabIndex = 27;
            this.btnCreateSceneCollection.Text = "Create Scene Collection";
            this.btnCreateSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentSceneCollection
            // 
            this.btnSetCurrentSceneCollection.Enabled = false;
            this.btnSetCurrentSceneCollection.Location = new Point(14, 136);
            this.btnSetCurrentSceneCollection.Margin = new Padding(2);
            this.btnSetCurrentSceneCollection.Name = "btnSetCurrentSceneCollection";
            this.btnSetCurrentSceneCollection.Size = new Size(262, 36);
            this.btnSetCurrentSceneCollection.TabIndex = 26;
            this.btnSetCurrentSceneCollection.Text = "Set Current Scene Collection";
            this.btnSetCurrentSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneCollectionList
            // 
            this.btnGetSceneCollectionList.Location = new Point(14, 95);
            this.btnGetSceneCollectionList.Margin = new Padding(2);
            this.btnGetSceneCollectionList.Name = "btnGetSceneCollectionList";
            this.btnGetSceneCollectionList.Size = new Size(262, 36);
            this.btnGetSceneCollectionList.TabIndex = 25;
            this.btnGetSceneCollectionList.Text = "Get Scene Collection List";
            this.btnGetSceneCollectionList.UseVisualStyleBackColor = true;
            this.btnGetSceneCollectionList.Click += this.BtnGetSceneCollectionList_Click;
            // 
            // btnSetPersistentData
            // 
            this.btnSetPersistentData.Enabled = false;
            this.btnSetPersistentData.Location = new Point(14, 55);
            this.btnSetPersistentData.Margin = new Padding(2);
            this.btnSetPersistentData.Name = "btnSetPersistentData";
            this.btnSetPersistentData.Size = new Size(262, 36);
            this.btnSetPersistentData.TabIndex = 24;
            this.btnSetPersistentData.Text = "Set Persistent Data";
            this.btnSetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnGetPersistentData
            // 
            this.btnGetPersistentData.Enabled = false;
            this.btnGetPersistentData.Location = new Point(14, 14);
            this.btnGetPersistentData.Margin = new Padding(2);
            this.btnGetPersistentData.Name = "btnGetPersistentData";
            this.btnGetPersistentData.Size = new Size(262, 36);
            this.btnGetPersistentData.TabIndex = 23;
            this.btnGetPersistentData.Text = "Get Persistent Data";
            this.btnGetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentProfile
            // 
            this.btnSetCurrentProfile.Enabled = false;
            this.btnSetCurrentProfile.Location = new Point(14, 258);
            this.btnSetCurrentProfile.Margin = new Padding(2);
            this.btnSetCurrentProfile.Name = "btnSetCurrentProfile";
            this.btnSetCurrentProfile.Size = new Size(262, 36);
            this.btnSetCurrentProfile.TabIndex = 29;
            this.btnSetCurrentProfile.Text = "Set Current Profile";
            this.btnSetCurrentProfile.UseVisualStyleBackColor = true;
            // 
            // btnCreateProfile
            // 
            this.btnCreateProfile.Enabled = false;
            this.btnCreateProfile.Location = new Point(14, 298);
            this.btnCreateProfile.Margin = new Padding(2);
            this.btnCreateProfile.Name = "btnCreateProfile";
            this.btnCreateProfile.Size = new Size(262, 36);
            this.btnCreateProfile.TabIndex = 30;
            this.btnCreateProfile.Text = "Create Profile";
            this.btnCreateProfile.UseVisualStyleBackColor = true;
            // 
            // btnRemoveProfile
            // 
            this.btnRemoveProfile.Enabled = false;
            this.btnRemoveProfile.Location = new Point(280, 14);
            this.btnRemoveProfile.Margin = new Padding(2);
            this.btnRemoveProfile.Name = "btnRemoveProfile";
            this.btnRemoveProfile.Size = new Size(262, 36);
            this.btnRemoveProfile.TabIndex = 31;
            this.btnRemoveProfile.Text = "Remove Profile";
            this.btnRemoveProfile.UseVisualStyleBackColor = true;
            // 
            // btnGetProfileParameter
            // 
            this.btnGetProfileParameter.Enabled = false;
            this.btnGetProfileParameter.Location = new Point(280, 55);
            this.btnGetProfileParameter.Margin = new Padding(2);
            this.btnGetProfileParameter.Name = "btnGetProfileParameter";
            this.btnGetProfileParameter.Size = new Size(262, 36);
            this.btnGetProfileParameter.TabIndex = 32;
            this.btnGetProfileParameter.Text = "Get Profile Parameter";
            this.btnGetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnSetProfileParameter
            // 
            this.btnSetProfileParameter.Enabled = false;
            this.btnSetProfileParameter.Location = new Point(280, 95);
            this.btnSetProfileParameter.Margin = new Padding(2);
            this.btnSetProfileParameter.Name = "btnSetProfileParameter";
            this.btnSetProfileParameter.Size = new Size(262, 36);
            this.btnSetProfileParameter.TabIndex = 33;
            this.btnSetProfileParameter.Text = "Set Profile Parameter";
            this.btnSetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnGetVideoSettings
            // 
            this.btnGetVideoSettings.Enabled = false;
            this.btnGetVideoSettings.Location = new Point(280, 136);
            this.btnGetVideoSettings.Margin = new Padding(2);
            this.btnGetVideoSettings.Name = "btnGetVideoSettings";
            this.btnGetVideoSettings.Size = new Size(262, 36);
            this.btnGetVideoSettings.TabIndex = 34;
            this.btnGetVideoSettings.Text = "Get Video Settings";
            this.btnGetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetVideoSettings
            // 
            this.btnSetVideoSettings.Enabled = false;
            this.btnSetVideoSettings.Location = new Point(280, 177);
            this.btnSetVideoSettings.Margin = new Padding(2);
            this.btnSetVideoSettings.Name = "btnSetVideoSettings";
            this.btnSetVideoSettings.Size = new Size(262, 36);
            this.btnSetVideoSettings.TabIndex = 35;
            this.btnSetVideoSettings.Text = "Set Video Settings";
            this.btnSetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new Point(9, 12);
            this.label1.Margin = new Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new Size(98, 25);
            this.label1.TabIndex = 16;
            this.label1.Text = "Hostname:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new Point(9, 48);
            this.label2.Margin = new Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new Size(48, 25);
            this.label2.TabIndex = 17;
            this.label2.Text = "Port:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new Point(9, 82);
            this.label3.Margin = new Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new Size(91, 25);
            this.label3.TabIndex = 18;
            this.label3.Text = "Password:";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tpGeneralRequests);
            this.tabControl1.Controls.Add(this.tpConfigRequests);
            this.tabControl1.Controls.Add(this.tpSourcesRequests);
            this.tabControl1.Controls.Add(this.tpScenesRequests);
            this.tabControl1.Controls.Add(this.tpInputsRequests);
            this.tabControl1.Controls.Add(this.tpFiltersRequests);
            this.tabControl1.Controls.Add(this.tbMediaInputsRequests);
            this.tabControl1.Controls.Add(this.tpOutputsRequests);
            this.tabControl1.Controls.Add(this.tbRecordRequests);
            this.tabControl1.Controls.Add(this.tbSceneItemsRequests);
            this.tabControl1.Controls.Add(this.tpStreamRequests);
            this.tabControl1.Controls.Add(this.tpTransitionsRequests);
            this.tabControl1.Controls.Add(this.tpUIRequests);
            this.tabControl1.Controls.Add(this.tpMisc);
            this.tabControl1.Location = new Point(18, 204);
            this.tabControl1.Margin = new Padding(2);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new Size(967, 394);
            this.tabControl1.TabIndex = 19;
            // 
            // tpGeneralRequests
            // 
            this.tpGeneralRequests.Controls.Add(this.btnSleep);
            this.tpGeneralRequests.Controls.Add(this.btnGetVersion);
            this.tpGeneralRequests.Controls.Add(this.btnTriggerHotkeyByKeySequence);
            this.tpGeneralRequests.Controls.Add(this.btnGetStats);
            this.tpGeneralRequests.Controls.Add(this.btnTriggerHotkeyByName);
            this.tpGeneralRequests.Controls.Add(this.btnBroadcastCustomEvent);
            this.tpGeneralRequests.Controls.Add(this.btnGetHotkeyList);
            this.tpGeneralRequests.Controls.Add(this.btnCallVendorRequest);
            this.tpGeneralRequests.Location = new Point(4, 34);
            this.tpGeneralRequests.Margin = new Padding(2);
            this.tpGeneralRequests.Name = "tpGeneralRequests";
            this.tpGeneralRequests.Padding = new Padding(2);
            this.tpGeneralRequests.Size = new Size(959, 356);
            this.tpGeneralRequests.TabIndex = 0;
            this.tpGeneralRequests.Text = "General Requests";
            this.tpGeneralRequests.UseVisualStyleBackColor = true;
            // 
            // tpConfigRequests
            // 
            this.tpConfigRequests.Controls.Add(this.btnGetRecordDirectory);
            this.tpConfigRequests.Controls.Add(this.btnSetStreamServiceSettings);
            this.tpConfigRequests.Controls.Add(this.btnGetStreamServiceSettings);
            this.tpConfigRequests.Controls.Add(this.btnSetVideoSettings);
            this.tpConfigRequests.Controls.Add(this.btnGetPersistentData);
            this.tpConfigRequests.Controls.Add(this.btnGetVideoSettings);
            this.tpConfigRequests.Controls.Add(this.btnSetPersistentData);
            this.tpConfigRequests.Controls.Add(this.btnSetProfileParameter);
            this.tpConfigRequests.Controls.Add(this.btnGetSceneCollectionList);
            this.tpConfigRequests.Controls.Add(this.btnGetProfileParameter);
            this.tpConfigRequests.Controls.Add(this.btnSetCurrentSceneCollection);
            this.tpConfigRequests.Controls.Add(this.btnRemoveProfile);
            this.tpConfigRequests.Controls.Add(this.btnCreateSceneCollection);
            this.tpConfigRequests.Controls.Add(this.btnCreateProfile);
            this.tpConfigRequests.Controls.Add(this.btnGetProfileList);
            this.tpConfigRequests.Controls.Add(this.btnSetCurrentProfile);
            this.tpConfigRequests.Location = new Point(4, 34);
            this.tpConfigRequests.Margin = new Padding(2);
            this.tpConfigRequests.Name = "tpConfigRequests";
            this.tpConfigRequests.Padding = new Padding(2);
            this.tpConfigRequests.Size = new Size(959, 356);
            this.tpConfigRequests.TabIndex = 1;
            this.tpConfigRequests.Text = "Config Requests";
            this.tpConfigRequests.UseVisualStyleBackColor = true;
            // 
            // btnGetRecordDirectory
            // 
            this.btnGetRecordDirectory.Location = new Point(280, 298);
            this.btnGetRecordDirectory.Margin = new Padding(2);
            this.btnGetRecordDirectory.Name = "btnGetRecordDirectory";
            this.btnGetRecordDirectory.Size = new Size(262, 36);
            this.btnGetRecordDirectory.TabIndex = 38;
            this.btnGetRecordDirectory.Text = "Get Record Directory";
            this.btnGetRecordDirectory.UseVisualStyleBackColor = true;
            this.btnGetRecordDirectory.Click += this.BtnGetRecordDirectory_Click;
            // 
            // btnSetStreamServiceSettings
            // 
            this.btnSetStreamServiceSettings.Enabled = false;
            this.btnSetStreamServiceSettings.Location = new Point(280, 258);
            this.btnSetStreamServiceSettings.Margin = new Padding(2);
            this.btnSetStreamServiceSettings.Name = "btnSetStreamServiceSettings";
            this.btnSetStreamServiceSettings.Size = new Size(262, 36);
            this.btnSetStreamServiceSettings.TabIndex = 37;
            this.btnSetStreamServiceSettings.Text = "Set Stream Service Settings";
            this.btnSetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetStreamServiceSettings
            // 
            this.btnGetStreamServiceSettings.Enabled = false;
            this.btnGetStreamServiceSettings.Location = new Point(280, 217);
            this.btnGetStreamServiceSettings.Margin = new Padding(2);
            this.btnGetStreamServiceSettings.Name = "btnGetStreamServiceSettings";
            this.btnGetStreamServiceSettings.Size = new Size(262, 36);
            this.btnGetStreamServiceSettings.TabIndex = 36;
            this.btnGetStreamServiceSettings.Text = "Get Stream Service Settings";
            this.btnGetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // tpSourcesRequests
            // 
            this.tpSourcesRequests.Controls.Add(this.btnSaveSourceScreenshot);
            this.tpSourcesRequests.Controls.Add(this.btnSetSourceActive);
            this.tpSourcesRequests.Controls.Add(this.btnGetSourceActive);
            this.tpSourcesRequests.Location = new Point(4, 34);
            this.tpSourcesRequests.Margin = new Padding(2);
            this.tpSourcesRequests.Name = "tpSourcesRequests";
            this.tpSourcesRequests.Size = new Size(959, 356);
            this.tpSourcesRequests.TabIndex = 2;
            this.tpSourcesRequests.Text = "Sources Requests";
            this.tpSourcesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSaveSourceScreenshot
            // 
            this.btnSaveSourceScreenshot.Enabled = false;
            this.btnSaveSourceScreenshot.Location = new Point(2, 84);
            this.btnSaveSourceScreenshot.Margin = new Padding(2);
            this.btnSaveSourceScreenshot.Name = "btnSaveSourceScreenshot";
            this.btnSaveSourceScreenshot.Size = new Size(262, 36);
            this.btnSaveSourceScreenshot.TabIndex = 22;
            this.btnSaveSourceScreenshot.Text = "Save Source Screenshot";
            this.btnSaveSourceScreenshot.UseVisualStyleBackColor = true;
            // 
            // btnSetSourceActive
            // 
            this.btnSetSourceActive.Enabled = false;
            this.btnSetSourceActive.Location = new Point(2, 43);
            this.btnSetSourceActive.Margin = new Padding(2);
            this.btnSetSourceActive.Name = "btnSetSourceActive";
            this.btnSetSourceActive.Size = new Size(262, 36);
            this.btnSetSourceActive.TabIndex = 21;
            this.btnSetSourceActive.Text = "Set Source Active";
            this.btnSetSourceActive.UseVisualStyleBackColor = true;
            // 
            // btnGetSourceActive
            // 
            this.btnGetSourceActive.Enabled = false;
            this.btnGetSourceActive.Location = new Point(2, 2);
            this.btnGetSourceActive.Margin = new Padding(2);
            this.btnGetSourceActive.Name = "btnGetSourceActive";
            this.btnGetSourceActive.Size = new Size(262, 36);
            this.btnGetSourceActive.TabIndex = 20;
            this.btnGetSourceActive.Text = "Get Source Active";
            this.btnGetSourceActive.UseVisualStyleBackColor = true;
            // 
            // tpScenesRequests
            // 
            this.tpScenesRequests.Controls.Add(this.btnSetSceneSceneTransitionOverride);
            this.tpScenesRequests.Controls.Add(this.btnGetSceneSceneTransitionOverride);
            this.tpScenesRequests.Controls.Add(this.btnSetSceneName);
            this.tpScenesRequests.Controls.Add(this.btnRemoveScene);
            this.tpScenesRequests.Controls.Add(this.btnCreateScene);
            this.tpScenesRequests.Controls.Add(this.btnSetCurrentPreviewScene);
            this.tpScenesRequests.Controls.Add(this.btnGetCurrentPreviewScene);
            this.tpScenesRequests.Controls.Add(this.btnSetCurrentProgramScene);
            this.tpScenesRequests.Controls.Add(this.btnGetCurrentProgramScene);
            this.tpScenesRequests.Controls.Add(this.btnGetGroupList);
            this.tpScenesRequests.Controls.Add(this.btnGetSceneList);
            this.tpScenesRequests.Location = new Point(4, 34);
            this.tpScenesRequests.Margin = new Padding(2);
            this.tpScenesRequests.Name = "tpScenesRequests";
            this.tpScenesRequests.Size = new Size(959, 356);
            this.tpScenesRequests.TabIndex = 3;
            this.tpScenesRequests.Text = "Scenes Requests";
            this.tpScenesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneSceneTransitionOverride
            // 
            this.btnSetSceneSceneTransitionOverride.Enabled = false;
            this.btnSetSceneSceneTransitionOverride.Location = new Point(270, 84);
            this.btnSetSceneSceneTransitionOverride.Margin = new Padding(2);
            this.btnSetSceneSceneTransitionOverride.Name = "btnSetSceneSceneTransitionOverride";
            this.btnSetSceneSceneTransitionOverride.Size = new Size(354, 36);
            this.btnSetSceneSceneTransitionOverride.TabIndex = 30;
            this.btnSetSceneSceneTransitionOverride.Text = "Set Scene Scene Transition Override";
            this.btnSetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneSceneTransitionOverride
            // 
            this.btnGetSceneSceneTransitionOverride.Enabled = false;
            this.btnGetSceneSceneTransitionOverride.Location = new Point(270, 43);
            this.btnGetSceneSceneTransitionOverride.Margin = new Padding(2);
            this.btnGetSceneSceneTransitionOverride.Name = "btnGetSceneSceneTransitionOverride";
            this.btnGetSceneSceneTransitionOverride.Size = new Size(354, 36);
            this.btnGetSceneSceneTransitionOverride.TabIndex = 29;
            this.btnGetSceneSceneTransitionOverride.Text = "Get Scene Scene Transition Override";
            this.btnGetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneName
            // 
            this.btnSetSceneName.Location = new Point(270, 2);
            this.btnSetSceneName.Margin = new Padding(2);
            this.btnSetSceneName.Name = "btnSetSceneName";
            this.btnSetSceneName.Size = new Size(262, 36);
            this.btnSetSceneName.TabIndex = 28;
            this.btnSetSceneName.Text = "Set Scene Name";
            this.btnSetSceneName.UseVisualStyleBackColor = true;
            this.btnSetSceneName.Click += this.BtnSetSceneName_Click;
            // 
            // btnRemoveScene
            // 
            this.btnRemoveScene.Location = new Point(2, 287);
            this.btnRemoveScene.Margin = new Padding(2);
            this.btnRemoveScene.Name = "btnRemoveScene";
            this.btnRemoveScene.Size = new Size(262, 36);
            this.btnRemoveScene.TabIndex = 27;
            this.btnRemoveScene.Text = "Remove Scene";
            this.btnRemoveScene.UseVisualStyleBackColor = true;
            this.btnRemoveScene.Click += this.BtnRemoveScene_Click;
            // 
            // btnCreateScene
            // 
            this.btnCreateScene.Location = new Point(2, 246);
            this.btnCreateScene.Margin = new Padding(2);
            this.btnCreateScene.Name = "btnCreateScene";
            this.btnCreateScene.Size = new Size(262, 36);
            this.btnCreateScene.TabIndex = 26;
            this.btnCreateScene.Text = "Create Scene";
            this.btnCreateScene.UseVisualStyleBackColor = true;
            this.btnCreateScene.Click += this.BtnCreateScene_Click;
            // 
            // btnSetCurrentPreviewScene
            // 
            this.btnSetCurrentPreviewScene.Location = new Point(2, 205);
            this.btnSetCurrentPreviewScene.Margin = new Padding(2);
            this.btnSetCurrentPreviewScene.Name = "btnSetCurrentPreviewScene";
            this.btnSetCurrentPreviewScene.Size = new Size(262, 36);
            this.btnSetCurrentPreviewScene.TabIndex = 25;
            this.btnSetCurrentPreviewScene.Text = "Set Current Preview Scene";
            this.btnSetCurrentPreviewScene.UseVisualStyleBackColor = true;
            this.btnSetCurrentPreviewScene.Click += this.BtnSetCurrentPreviewScene_Click;
            // 
            // btnGetCurrentPreviewScene
            // 
            this.btnGetCurrentPreviewScene.Location = new Point(2, 165);
            this.btnGetCurrentPreviewScene.Margin = new Padding(2);
            this.btnGetCurrentPreviewScene.Name = "btnGetCurrentPreviewScene";
            this.btnGetCurrentPreviewScene.Size = new Size(262, 36);
            this.btnGetCurrentPreviewScene.TabIndex = 24;
            this.btnGetCurrentPreviewScene.Text = "Get Current Preview Scene";
            this.btnGetCurrentPreviewScene.UseVisualStyleBackColor = true;
            this.btnGetCurrentPreviewScene.Click += this.BtnGetCurrentPreviewScene_Click;
            // 
            // btnSetCurrentProgramScene
            // 
            this.btnSetCurrentProgramScene.Location = new Point(2, 124);
            this.btnSetCurrentProgramScene.Margin = new Padding(2);
            this.btnSetCurrentProgramScene.Name = "btnSetCurrentProgramScene";
            this.btnSetCurrentProgramScene.Size = new Size(262, 36);
            this.btnSetCurrentProgramScene.TabIndex = 23;
            this.btnSetCurrentProgramScene.Text = "Set Current Program Scene";
            this.btnSetCurrentProgramScene.UseVisualStyleBackColor = true;
            this.btnSetCurrentProgramScene.Click += this.BtnSetCurrentProgramScene_Click;
            // 
            // btnGetCurrentProgramScene
            // 
            this.btnGetCurrentProgramScene.Location = new Point(2, 84);
            this.btnGetCurrentProgramScene.Margin = new Padding(2);
            this.btnGetCurrentProgramScene.Name = "btnGetCurrentProgramScene";
            this.btnGetCurrentProgramScene.Size = new Size(262, 36);
            this.btnGetCurrentProgramScene.TabIndex = 22;
            this.btnGetCurrentProgramScene.Text = "Get Current Program Scene";
            this.btnGetCurrentProgramScene.UseVisualStyleBackColor = true;
            this.btnGetCurrentProgramScene.Click += this.BtnGetCurrentProgramScene_Click;
            // 
            // btnGetGroupList
            // 
            this.btnGetGroupList.Location = new Point(2, 43);
            this.btnGetGroupList.Margin = new Padding(2);
            this.btnGetGroupList.Name = "btnGetGroupList";
            this.btnGetGroupList.Size = new Size(262, 36);
            this.btnGetGroupList.TabIndex = 21;
            this.btnGetGroupList.Text = "Get Group List";
            this.btnGetGroupList.UseVisualStyleBackColor = true;
            this.btnGetGroupList.Click += this.BtnGetGroupList_Click;
            // 
            // btnGetSceneList
            // 
            this.btnGetSceneList.Location = new Point(2, 2);
            this.btnGetSceneList.Margin = new Padding(2);
            this.btnGetSceneList.Name = "btnGetSceneList";
            this.btnGetSceneList.Size = new Size(262, 36);
            this.btnGetSceneList.TabIndex = 20;
            this.btnGetSceneList.Text = "Get Scene List";
            this.btnGetSceneList.UseVisualStyleBackColor = true;
            this.btnGetSceneList.Click += this.BtnGetSceneList_Click;
            // 
            // tpInputsRequests
            // 
            this.tpInputsRequests.Controls.Add(this.btnToggleInputMute);
            this.tpInputsRequests.Controls.Add(this.btnSetInputMute);
            this.tpInputsRequests.Controls.Add(this.btnGetInputMute);
            this.tpInputsRequests.Controls.Add(this.btnSetInputSettings);
            this.tpInputsRequests.Controls.Add(this.btnGetInputSettings);
            this.tpInputsRequests.Controls.Add(this.btnGetInputDefaultSettings);
            this.tpInputsRequests.Controls.Add(this.btnSetInputName);
            this.tpInputsRequests.Controls.Add(this.btnRemoveInput);
            this.tpInputsRequests.Controls.Add(this.btnCreateInput);
            this.tpInputsRequests.Controls.Add(this.btnGetSpecialInputs);
            this.tpInputsRequests.Controls.Add(this.btnGetInputKindList);
            this.tpInputsRequests.Controls.Add(this.btnGetInputList);
            this.tpInputsRequests.Location = new Point(4, 34);
            this.tpInputsRequests.Margin = new Padding(2);
            this.tpInputsRequests.Name = "tpInputsRequests";
            this.tpInputsRequests.Size = new Size(959, 356);
            this.tpInputsRequests.TabIndex = 4;
            this.tpInputsRequests.Text = "Inputs Requests";
            this.tpInputsRequests.UseVisualStyleBackColor = true;
            // 
            // btnToggleInputMute
            // 
            this.btnToggleInputMute.Enabled = false;
            this.btnToggleInputMute.Location = new Point(270, 124);
            this.btnToggleInputMute.Margin = new Padding(2);
            this.btnToggleInputMute.Name = "btnToggleInputMute";
            this.btnToggleInputMute.Size = new Size(262, 36);
            this.btnToggleInputMute.TabIndex = 32;
            this.btnToggleInputMute.Text = "Toggle Input Mute";
            this.btnToggleInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputMute
            // 
            this.btnSetInputMute.Enabled = false;
            this.btnSetInputMute.Location = new Point(270, 84);
            this.btnSetInputMute.Margin = new Padding(2);
            this.btnSetInputMute.Name = "btnSetInputMute";
            this.btnSetInputMute.Size = new Size(262, 36);
            this.btnSetInputMute.TabIndex = 31;
            this.btnSetInputMute.Text = "Set Input Mute";
            this.btnSetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnGetInputMute
            // 
            this.btnGetInputMute.Enabled = false;
            this.btnGetInputMute.Location = new Point(270, 43);
            this.btnGetInputMute.Margin = new Padding(2);
            this.btnGetInputMute.Name = "btnGetInputMute";
            this.btnGetInputMute.Size = new Size(262, 36);
            this.btnGetInputMute.TabIndex = 30;
            this.btnGetInputMute.Text = "Get Input Mute";
            this.btnGetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputSettings
            // 
            this.btnSetInputSettings.Enabled = false;
            this.btnSetInputSettings.Location = new Point(270, 2);
            this.btnSetInputSettings.Margin = new Padding(2);
            this.btnSetInputSettings.Name = "btnSetInputSettings";
            this.btnSetInputSettings.Size = new Size(262, 36);
            this.btnSetInputSettings.TabIndex = 29;
            this.btnSetInputSettings.Text = "Set Input Settings";
            this.btnSetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputSettings
            // 
            this.btnGetInputSettings.Enabled = false;
            this.btnGetInputSettings.Location = new Point(2, 287);
            this.btnGetInputSettings.Margin = new Padding(2);
            this.btnGetInputSettings.Name = "btnGetInputSettings";
            this.btnGetInputSettings.Size = new Size(262, 36);
            this.btnGetInputSettings.TabIndex = 28;
            this.btnGetInputSettings.Text = "Get Input Settings";
            this.btnGetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputDefaultSettings
            // 
            this.btnGetInputDefaultSettings.Enabled = false;
            this.btnGetInputDefaultSettings.Location = new Point(2, 246);
            this.btnGetInputDefaultSettings.Margin = new Padding(2);
            this.btnGetInputDefaultSettings.Name = "btnGetInputDefaultSettings";
            this.btnGetInputDefaultSettings.Size = new Size(262, 36);
            this.btnGetInputDefaultSettings.TabIndex = 27;
            this.btnGetInputDefaultSettings.Text = "Get Input Default Settings";
            this.btnGetInputDefaultSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetInputName
            // 
            this.btnSetInputName.Enabled = false;
            this.btnSetInputName.Location = new Point(2, 205);
            this.btnSetInputName.Margin = new Padding(2);
            this.btnSetInputName.Name = "btnSetInputName";
            this.btnSetInputName.Size = new Size(262, 36);
            this.btnSetInputName.TabIndex = 26;
            this.btnSetInputName.Text = "Set Input Name";
            this.btnSetInputName.UseVisualStyleBackColor = true;
            // 
            // btnRemoveInput
            // 
            this.btnRemoveInput.Enabled = false;
            this.btnRemoveInput.Location = new Point(2, 165);
            this.btnRemoveInput.Margin = new Padding(2);
            this.btnRemoveInput.Name = "btnRemoveInput";
            this.btnRemoveInput.Size = new Size(262, 36);
            this.btnRemoveInput.TabIndex = 25;
            this.btnRemoveInput.Text = "Remove Input";
            this.btnRemoveInput.UseVisualStyleBackColor = true;
            // 
            // btnCreateInput
            // 
            this.btnCreateInput.Enabled = false;
            this.btnCreateInput.Location = new Point(0, 124);
            this.btnCreateInput.Margin = new Padding(2);
            this.btnCreateInput.Name = "btnCreateInput";
            this.btnCreateInput.Size = new Size(262, 36);
            this.btnCreateInput.TabIndex = 24;
            this.btnCreateInput.Text = "Create Input";
            this.btnCreateInput.UseVisualStyleBackColor = true;
            // 
            // btnGetSpecialInputs
            // 
            this.btnGetSpecialInputs.Location = new Point(0, 84);
            this.btnGetSpecialInputs.Margin = new Padding(2);
            this.btnGetSpecialInputs.Name = "btnGetSpecialInputs";
            this.btnGetSpecialInputs.Size = new Size(262, 36);
            this.btnGetSpecialInputs.TabIndex = 23;
            this.btnGetSpecialInputs.Text = "Get Special Inputs";
            this.btnGetSpecialInputs.UseVisualStyleBackColor = true;
            this.btnGetSpecialInputs.Click += this.BtnGetSpecialInputs_Click;
            // 
            // btnGetInputKindList
            // 
            this.btnGetInputKindList.Location = new Point(2, 43);
            this.btnGetInputKindList.Margin = new Padding(2);
            this.btnGetInputKindList.Name = "btnGetInputKindList";
            this.btnGetInputKindList.Size = new Size(262, 36);
            this.btnGetInputKindList.TabIndex = 22;
            this.btnGetInputKindList.Text = "Get Input Kind List";
            this.btnGetInputKindList.UseVisualStyleBackColor = true;
            this.btnGetInputKindList.Click += this.BtnGetInputKindList_Click;
            // 
            // btnGetInputList
            // 
            this.btnGetInputList.Location = new Point(2, 2);
            this.btnGetInputList.Margin = new Padding(2);
            this.btnGetInputList.Name = "btnGetInputList";
            this.btnGetInputList.Size = new Size(262, 36);
            this.btnGetInputList.TabIndex = 21;
            this.btnGetInputList.Text = "Get Input List";
            this.btnGetInputList.UseVisualStyleBackColor = true;
            this.btnGetInputList.Click += this.BtnGetInputList_Click;
            // 
            // tpFiltersRequests
            // 
            this.tpFiltersRequests.Location = new Point(4, 34);
            this.tpFiltersRequests.Margin = new Padding(2);
            this.tpFiltersRequests.Name = "tpFiltersRequests";
            this.tpFiltersRequests.Size = new Size(959, 356);
            this.tpFiltersRequests.TabIndex = 5;
            this.tpFiltersRequests.Text = "Filters Requests";
            this.tpFiltersRequests.UseVisualStyleBackColor = true;
            // 
            // tbMediaInputsRequests
            // 
            this.tbMediaInputsRequests.Location = new Point(4, 34);
            this.tbMediaInputsRequests.Margin = new Padding(2);
            this.tbMediaInputsRequests.Name = "tbMediaInputsRequests";
            this.tbMediaInputsRequests.Size = new Size(959, 356);
            this.tbMediaInputsRequests.TabIndex = 6;
            this.tbMediaInputsRequests.Text = "Media Inputs Requests";
            this.tbMediaInputsRequests.UseVisualStyleBackColor = true;
            // 
            // tpOutputsRequests
            // 
            this.tpOutputsRequests.Controls.Add(this.bnGetStreamStatus);
            this.tpOutputsRequests.Controls.Add(this.btnGetVirtualCameraState);
            this.tpOutputsRequests.Controls.Add(this.btnToggleVirtualCamera);
            this.tpOutputsRequests.Controls.Add(this.btnStartVirtualCam);
            this.tpOutputsRequests.Controls.Add(this.btnStopVirtualCamera);
            this.tpOutputsRequests.Location = new Point(4, 34);
            this.tpOutputsRequests.Margin = new Padding(2);
            this.tpOutputsRequests.Name = "tpOutputsRequests";
            this.tpOutputsRequests.Size = new Size(959, 356);
            this.tpOutputsRequests.TabIndex = 7;
            this.tpOutputsRequests.Text = "Outputs Requests";
            this.tpOutputsRequests.UseVisualStyleBackColor = true;
            // 
            // bnGetStreamStatus
            // 
            this.bnGetStreamStatus.Location = new Point(0, 166);
            this.bnGetStreamStatus.Margin = new Padding(2);
            this.bnGetStreamStatus.Name = "bnGetStreamStatus";
            this.bnGetStreamStatus.Size = new Size(262, 36);
            this.bnGetStreamStatus.TabIndex = 9;
            this.bnGetStreamStatus.Text = "Get Stream Status";
            this.bnGetStreamStatus.UseVisualStyleBackColor = true;
            this.bnGetStreamStatus.Click += this.BtnGetStreamStatus_Click;
            // 
            // tbRecordRequests
            // 
            this.tbRecordRequests.Controls.Add(this.btnSetRecordDirectory);
            this.tbRecordRequests.Controls.Add(this.btnResumeRecord);
            this.tbRecordRequests.Controls.Add(this.btnPauseRecord);
            this.tbRecordRequests.Controls.Add(this.btnToggleRecordPause);
            this.tbRecordRequests.Controls.Add(this.btnStopRecord);
            this.tbRecordRequests.Controls.Add(this.btnStartRecord);
            this.tbRecordRequests.Controls.Add(this.btnToggleRecord);
            this.tbRecordRequests.Controls.Add(this.btnGetRecordStatus);
            this.tbRecordRequests.Location = new Point(4, 34);
            this.tbRecordRequests.Margin = new Padding(2);
            this.tbRecordRequests.Name = "tbRecordRequests";
            this.tbRecordRequests.Size = new Size(959, 356);
            this.tbRecordRequests.TabIndex = 8;
            this.tbRecordRequests.Text = "Record Requests";
            this.tbRecordRequests.UseVisualStyleBackColor = true;
            // 
            // btnSetRecordDirectory
            // 
            this.btnSetRecordDirectory.Location = new Point(2, 288);
            this.btnSetRecordDirectory.Margin = new Padding(2);
            this.btnSetRecordDirectory.Name = "btnSetRecordDirectory";
            this.btnSetRecordDirectory.Size = new Size(262, 36);
            this.btnSetRecordDirectory.TabIndex = 16;
            this.btnSetRecordDirectory.Text = "Set Record Directory";
            this.btnSetRecordDirectory.UseVisualStyleBackColor = true;
            this.btnSetRecordDirectory.Click += this.BtnSetRecordDirectory_Click;
            // 
            // btnResumeRecord
            // 
            this.btnResumeRecord.Location = new Point(2, 247);
            this.btnResumeRecord.Margin = new Padding(2);
            this.btnResumeRecord.Name = "btnResumeRecord";
            this.btnResumeRecord.Size = new Size(262, 36);
            this.btnResumeRecord.TabIndex = 15;
            this.btnResumeRecord.Text = "Resume Record";
            this.btnResumeRecord.UseVisualStyleBackColor = true;
            this.btnResumeRecord.Click += this.BtnResumeRecord_Click;
            // 
            // btnPauseRecord
            // 
            this.btnPauseRecord.Location = new Point(2, 206);
            this.btnPauseRecord.Margin = new Padding(2);
            this.btnPauseRecord.Name = "btnPauseRecord";
            this.btnPauseRecord.Size = new Size(262, 36);
            this.btnPauseRecord.TabIndex = 14;
            this.btnPauseRecord.Text = "Pause Record";
            this.btnPauseRecord.UseVisualStyleBackColor = true;
            this.btnPauseRecord.Click += this.BtnPauseRecord_Click;
            // 
            // btnToggleRecordPause
            // 
            this.btnToggleRecordPause.Location = new Point(2, 166);
            this.btnToggleRecordPause.Margin = new Padding(2);
            this.btnToggleRecordPause.Name = "btnToggleRecordPause";
            this.btnToggleRecordPause.Size = new Size(262, 36);
            this.btnToggleRecordPause.TabIndex = 13;
            this.btnToggleRecordPause.Text = "Toggle Record Pause";
            this.btnToggleRecordPause.UseVisualStyleBackColor = true;
            this.btnToggleRecordPause.Click += this.BtnToggleRecordPause_Click;
            // 
            // btnStopRecord
            // 
            this.btnStopRecord.Location = new Point(2, 125);
            this.btnStopRecord.Margin = new Padding(2);
            this.btnStopRecord.Name = "btnStopRecord";
            this.btnStopRecord.Size = new Size(262, 36);
            this.btnStopRecord.TabIndex = 12;
            this.btnStopRecord.Text = "Stop Record";
            this.btnStopRecord.UseVisualStyleBackColor = true;
            this.btnStopRecord.Click += this.BtnStopRecord_Click;
            // 
            // btnStartRecord
            // 
            this.btnStartRecord.Location = new Point(2, 84);
            this.btnStartRecord.Margin = new Padding(2);
            this.btnStartRecord.Name = "btnStartRecord";
            this.btnStartRecord.Size = new Size(262, 36);
            this.btnStartRecord.TabIndex = 11;
            this.btnStartRecord.Text = "Start Record";
            this.btnStartRecord.UseVisualStyleBackColor = true;
            this.btnStartRecord.Click += this.BtnStartRecord_Click;
            // 
            // btnToggleRecord
            // 
            this.btnToggleRecord.Location = new Point(2, 44);
            this.btnToggleRecord.Margin = new Padding(2);
            this.btnToggleRecord.Name = "btnToggleRecord";
            this.btnToggleRecord.Size = new Size(262, 36);
            this.btnToggleRecord.TabIndex = 10;
            this.btnToggleRecord.Text = "Toggle Record";
            this.btnToggleRecord.UseVisualStyleBackColor = true;
            this.btnToggleRecord.Click += this.BtnToggleRecord_Click;
            // 
            // btnGetRecordStatus
            // 
            this.btnGetRecordStatus.Location = new Point(2, 3);
            this.btnGetRecordStatus.Margin = new Padding(2);
            this.btnGetRecordStatus.Name = "btnGetRecordStatus";
            this.btnGetRecordStatus.Size = new Size(262, 36);
            this.btnGetRecordStatus.TabIndex = 9;
            this.btnGetRecordStatus.Text = "Get Record Status";
            this.btnGetRecordStatus.UseVisualStyleBackColor = true;
            this.btnGetRecordStatus.Click += this.BtnGetRecordStatus_Click;
            // 
            // tbSceneItemsRequests
            // 
            this.tbSceneItemsRequests.Location = new Point(4, 34);
            this.tbSceneItemsRequests.Margin = new Padding(2);
            this.tbSceneItemsRequests.Name = "tbSceneItemsRequests";
            this.tbSceneItemsRequests.Size = new Size(959, 356);
            this.tbSceneItemsRequests.TabIndex = 9;
            this.tbSceneItemsRequests.Text = "Scene Items Requests";
            this.tbSceneItemsRequests.UseVisualStyleBackColor = true;
            // 
            // tpStreamRequests
            // 
            this.tpStreamRequests.Location = new Point(4, 34);
            this.tpStreamRequests.Margin = new Padding(2);
            this.tpStreamRequests.Name = "tpStreamRequests";
            this.tpStreamRequests.Size = new Size(959, 356);
            this.tpStreamRequests.TabIndex = 10;
            this.tpStreamRequests.Text = "Stream Requests";
            this.tpStreamRequests.UseVisualStyleBackColor = true;
            // 
            // tpTransitionsRequests
            // 
            this.tpTransitionsRequests.Location = new Point(4, 34);
            this.tpTransitionsRequests.Margin = new Padding(2);
            this.tpTransitionsRequests.Name = "tpTransitionsRequests";
            this.tpTransitionsRequests.Size = new Size(959, 356);
            this.tpTransitionsRequests.TabIndex = 11;
            this.tpTransitionsRequests.Text = "Transitions Requests";
            this.tpTransitionsRequests.UseVisualStyleBackColor = true;
            // 
            // tpUIRequests
            // 
            this.tpUIRequests.Controls.Add(this.btnOpenSourceProjectorWindow);
            this.tpUIRequests.Controls.Add(this.button5);
            this.tpUIRequests.Controls.Add(this.btnOpenSourceProjectorOnMonitor);
            this.tpUIRequests.Controls.Add(this.btnOpenVideoMixProjectorWindow);
            this.tpUIRequests.Controls.Add(this.btnOpenVideoMixProjectorOnMonitor);
            this.tpUIRequests.Controls.Add(this.btnOpenInputInteractDialog);
            this.tpUIRequests.Controls.Add(this.btnOpenInputFiltersDialog);
            this.tpUIRequests.Controls.Add(this.btnOpenInputPropertiesDialog);
            this.tpUIRequests.Controls.Add(this.btnGetStudioModeEnabled);
            this.tpUIRequests.Controls.Add(this.btnStartStudioMode);
            this.tpUIRequests.Controls.Add(this.btnStopStudioMode);
            this.tpUIRequests.Controls.Add(this.btnGetMonitorList);
            this.tpUIRequests.Location = new Point(4, 34);
            this.tpUIRequests.Margin = new Padding(2);
            this.tpUIRequests.Name = "tpUIRequests";
            this.tpUIRequests.Size = new Size(959, 356);
            this.tpUIRequests.TabIndex = 12;
            this.tpUIRequests.Text = "UI Requests";
            this.tpUIRequests.UseVisualStyleBackColor = true;
            // 
            // btnOpenSourceProjectorWindow
            // 
            this.btnOpenSourceProjectorWindow.Location = new Point(270, 166);
            this.btnOpenSourceProjectorWindow.Margin = new Padding(2);
            this.btnOpenSourceProjectorWindow.Name = "btnOpenSourceProjectorWindow";
            this.btnOpenSourceProjectorWindow.Size = new Size(262, 36);
            this.btnOpenSourceProjectorWindow.TabIndex = 20;
            this.btnOpenSourceProjectorWindow.Text = "OpenSourceProjectorWindow";
            this.btnOpenSourceProjectorWindow.UseVisualStyleBackColor = true;
            // 
            // button5
            // 
            this.button5.Location = new Point(268, 125);
            this.button5.Margin = new Padding(2);
            this.button5.Name = "button5";
            this.button5.Size = new Size(262, 36);
            this.button5.TabIndex = 19;
            this.button5.Text = "OpenVideoMixProjectorOnMonitor";
            this.button5.UseVisualStyleBackColor = true;
            // 
            // btnOpenSourceProjectorOnMonitor
            // 
            this.btnOpenSourceProjectorOnMonitor.Location = new Point(270, 84);
            this.btnOpenSourceProjectorOnMonitor.Margin = new Padding(2);
            this.btnOpenSourceProjectorOnMonitor.Name = "btnOpenSourceProjectorOnMonitor";
            this.btnOpenSourceProjectorOnMonitor.Size = new Size(262, 36);
            this.btnOpenSourceProjectorOnMonitor.TabIndex = 18;
            this.btnOpenSourceProjectorOnMonitor.Text = "OpenSourceProjectorOnMonitor";
            this.btnOpenSourceProjectorOnMonitor.UseVisualStyleBackColor = true;
            // 
            // btnOpenVideoMixProjectorWindow
            // 
            this.btnOpenVideoMixProjectorWindow.Location = new Point(270, 44);
            this.btnOpenVideoMixProjectorWindow.Margin = new Padding(2);
            this.btnOpenVideoMixProjectorWindow.Name = "btnOpenVideoMixProjectorWindow";
            this.btnOpenVideoMixProjectorWindow.Size = new Size(262, 36);
            this.btnOpenVideoMixProjectorWindow.TabIndex = 17;
            this.btnOpenVideoMixProjectorWindow.Text = "OpenVideoMixProjectorWindow";
            this.btnOpenVideoMixProjectorWindow.UseVisualStyleBackColor = true;
            // 
            // btnOpenVideoMixProjectorOnMonitor
            // 
            this.btnOpenVideoMixProjectorOnMonitor.Location = new Point(270, 3);
            this.btnOpenVideoMixProjectorOnMonitor.Margin = new Padding(2);
            this.btnOpenVideoMixProjectorOnMonitor.Name = "btnOpenVideoMixProjectorOnMonitor";
            this.btnOpenVideoMixProjectorOnMonitor.Size = new Size(262, 36);
            this.btnOpenVideoMixProjectorOnMonitor.TabIndex = 16;
            this.btnOpenVideoMixProjectorOnMonitor.Text = "OpenVideoMixProjectorOnMonitor";
            this.btnOpenVideoMixProjectorOnMonitor.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputInteractDialog
            // 
            this.btnOpenInputInteractDialog.Location = new Point(2, 206);
            this.btnOpenInputInteractDialog.Margin = new Padding(2);
            this.btnOpenInputInteractDialog.Name = "btnOpenInputInteractDialog";
            this.btnOpenInputInteractDialog.Size = new Size(262, 36);
            this.btnOpenInputInteractDialog.TabIndex = 15;
            this.btnOpenInputInteractDialog.Text = "OpenInputInteractDialog";
            this.btnOpenInputInteractDialog.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputFiltersDialog
            // 
            this.btnOpenInputFiltersDialog.Location = new Point(2, 166);
            this.btnOpenInputFiltersDialog.Margin = new Padding(2);
            this.btnOpenInputFiltersDialog.Name = "btnOpenInputFiltersDialog";
            this.btnOpenInputFiltersDialog.Size = new Size(262, 36);
            this.btnOpenInputFiltersDialog.TabIndex = 14;
            this.btnOpenInputFiltersDialog.Text = "OpenInputFiltersDialog";
            this.btnOpenInputFiltersDialog.UseVisualStyleBackColor = true;
            // 
            // btnOpenInputPropertiesDialog
            // 
            this.btnOpenInputPropertiesDialog.Location = new Point(2, 125);
            this.btnOpenInputPropertiesDialog.Margin = new Padding(2);
            this.btnOpenInputPropertiesDialog.Name = "btnOpenInputPropertiesDialog";
            this.btnOpenInputPropertiesDialog.Size = new Size(262, 36);
            this.btnOpenInputPropertiesDialog.TabIndex = 13;
            this.btnOpenInputPropertiesDialog.Text = "OpenInputPropertiesDialog";
            this.btnOpenInputPropertiesDialog.UseVisualStyleBackColor = true;
            // 
            // tpMisc
            // 
            this.tpMisc.Controls.Add(this.btnBatch1);
            this.tpMisc.Location = new Point(4, 34);
            this.tpMisc.Margin = new Padding(2);
            this.tpMisc.Name = "tpMisc";
            this.tpMisc.Size = new Size(959, 356);
            this.tpMisc.TabIndex = 13;
            this.tpMisc.Text = "Misc";
            this.tpMisc.UseVisualStyleBackColor = true;
            // 
            // btnBatch1
            // 
            this.btnBatch1.Location = new Point(18, 14);
            this.btnBatch1.Margin = new Padding(2);
            this.btnBatch1.Name = "btnBatch1";
            this.btnBatch1.Size = new Size(262, 36);
            this.btnBatch1.TabIndex = 23;
            this.btnBatch1.Text = "Batch Request Sample 1";
            this.btnBatch1.UseVisualStyleBackColor = true;
            this.btnBatch1.Click += this.BtnBatch1_Click;
            // 
            // lbScenes
            // 
            this.lbScenes.FormattingEnabled = true;
            this.lbScenes.ItemHeight = 25;
            this.lbScenes.Location = new Point(1005, 243);
            this.lbScenes.Margin = new Padding(2);
            this.lbScenes.Name = "lbScenes";
            this.lbScenes.Size = new Size(222, 354);
            this.lbScenes.TabIndex = 20;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new Point(1005, 204);
            this.label4.Margin = new Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new Size(70, 25);
            this.label4.TabIndex = 21;
            this.label4.Text = "Scenes:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new Point(1232, 203);
            this.label5.Margin = new Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new Size(74, 25);
            this.label5.TabIndex = 23;
            this.label5.Text = "Groups:";
            // 
            // lbGroups
            // 
            this.lbGroups.FormattingEnabled = true;
            this.lbGroups.ItemHeight = 25;
            this.lbGroups.Location = new Point(1232, 242);
            this.lbGroups.Margin = new Padding(2);
            this.lbGroups.Name = "lbGroups";
            this.lbGroups.Size = new Size(222, 354);
            this.lbGroups.TabIndex = 22;
            // 
            // tbNameItem
            // 
            this.tbNameItem.Location = new Point(129, 159);
            this.tbNameItem.Margin = new Padding(2);
            this.tbNameItem.Name = "tbNameItem";
            this.tbNameItem.Size = new Size(155, 31);
            this.tbNameItem.TabIndex = 24;
            this.tbNameItem.Text = "Some Item Name";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new Point(18, 161);
            this.label6.Margin = new Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new Size(104, 25);
            this.label6.TabIndex = 25;
            this.label6.Text = "Item Name:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new Point(1005, 17);
            this.label7.Margin = new Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new Size(115, 25);
            this.label7.TabIndex = 26;
            this.label7.Text = "Record State:";
            // 
            // lblRecordState
            // 
            this.lblRecordState.AutoSize = true;
            this.lblRecordState.Location = new Point(1127, 17);
            this.lblRecordState.Margin = new Padding(2, 0, 2, 0);
            this.lblRecordState.Name = "lblRecordState";
            this.lblRecordState.Size = new Size(44, 25);
            this.lblRecordState.TabIndex = 27;
            this.lblRecordState.Text = "N/A";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new Point(1005, 47);
            this.label8.Margin = new Padding(2, 0, 2, 0);
            this.label8.Name = "label8";
            this.label8.Size = new Size(126, 25);
            this.label8.TabIndex = 28;
            this.label8.Text = "Record Folder:";
            // 
            // tbRecordFolder
            // 
            this.tbRecordFolder.Location = new Point(1138, 45);
            this.tbRecordFolder.Margin = new Padding(2);
            this.tbRecordFolder.Name = "tbRecordFolder";
            this.tbRecordFolder.Size = new Size(155, 31);
            this.tbRecordFolder.TabIndex = 29;
            // 
            // cbAutoReconnect
            // 
            this.cbAutoReconnect.AutoSize = true;
            this.cbAutoReconnect.Checked = true;
            this.cbAutoReconnect.CheckState = CheckState.Checked;
            this.cbAutoReconnect.Location = new Point(112, 117);
            this.cbAutoReconnect.Margin = new Padding(2);
            this.cbAutoReconnect.Name = "cbAutoReconnect";
            this.cbAutoReconnect.Size = new Size(158, 29);
            this.cbAutoReconnect.TabIndex = 30;
            this.cbAutoReconnect.Text = "AutoReconnect";
            this.cbAutoReconnect.UseVisualStyleBackColor = true;
            this.cbAutoReconnect.CheckedChanged += this.CbAutoReconnect_CheckedChanged;
            // 
            // lblBytesOut
            // 
            this.lblBytesOut.AutoSize = true;
            this.lblBytesOut.Location = new Point(578, 14);
            this.lblBytesOut.Margin = new Padding(2, 0, 2, 0);
            this.lblBytesOut.Name = "lblBytesOut";
            this.lblBytesOut.Size = new Size(22, 25);
            this.lblBytesOut.TabIndex = 32;
            this.lblBytesOut.Text = "0";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new Point(457, 14);
            this.label10.Margin = new Padding(2, 0, 2, 0);
            this.label10.Name = "label10";
            this.label10.Size = new Size(98, 25);
            this.label10.TabIndex = 31;
            this.label10.Text = "Bytes Sent:";
            // 
            // lblBytesIn
            // 
            this.lblBytesIn.AutoSize = true;
            this.lblBytesIn.Location = new Point(578, 47);
            this.lblBytesIn.Margin = new Padding(2, 0, 2, 0);
            this.lblBytesIn.Name = "lblBytesIn";
            this.lblBytesIn.Size = new Size(22, 25);
            this.lblBytesIn.TabIndex = 34;
            this.lblBytesIn.Text = "0";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new Point(457, 47);
            this.label12.Margin = new Padding(2, 0, 2, 0);
            this.label12.Name = "label12";
            this.label12.Size = new Size(110, 25);
            this.label12.TabIndex = 33;
            this.label12.Text = "Bytes Recvd:";
            // 
            // lblMessagesIn
            // 
            this.lblMessagesIn.AutoSize = true;
            this.lblMessagesIn.Location = new Point(578, 117);
            this.lblMessagesIn.Margin = new Padding(2, 0, 2, 0);
            this.lblMessagesIn.Name = "lblMessagesIn";
            this.lblMessagesIn.Size = new Size(22, 25);
            this.lblMessagesIn.TabIndex = 38;
            this.lblMessagesIn.Text = "0";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new Point(457, 117);
            this.label11.Margin = new Padding(2, 0, 2, 0);
            this.label11.Name = "label11";
            this.label11.Size = new Size(111, 25);
            this.label11.TabIndex = 37;
            this.label11.Text = "Msgs Recvd:";
            // 
            // lblMessagesOut
            // 
            this.lblMessagesOut.AutoSize = true;
            this.lblMessagesOut.Location = new Point(578, 84);
            this.lblMessagesOut.Margin = new Padding(2, 0, 2, 0);
            this.lblMessagesOut.Name = "lblMessagesOut";
            this.lblMessagesOut.Size = new Size(22, 25);
            this.lblMessagesOut.TabIndex = 36;
            this.lblMessagesOut.Text = "0";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new Point(457, 84);
            this.label14.Margin = new Padding(2, 0, 2, 0);
            this.label14.Name = "label14";
            this.label14.Size = new Size(99, 25);
            this.label14.TabIndex = 35;
            this.label14.Text = "Msgs Sent:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new Point(457, 152);
            this.label9.Margin = new Padding(2, 0, 2, 0);
            this.label9.Name = "label9";
            this.label9.Size = new Size(95, 25);
            this.label9.TabIndex = 39;
            this.label9.Text = "Last Event:";
            // 
            // lblLastEvent
            // 
            this.lblLastEvent.AutoSize = true;
            this.lblLastEvent.Location = new Point(578, 152);
            this.lblLastEvent.Margin = new Padding(2, 0, 2, 0);
            this.lblLastEvent.Name = "lblLastEvent";
            this.lblLastEvent.Size = new Size(44, 25);
            this.lblLastEvent.TabIndex = 40;
            this.lblLastEvent.Text = "N/A";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new SizeF(10F, 25F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1528, 663);
            this.Controls.Add(this.lblLastEvent);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.lblMessagesIn);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.lblMessagesOut);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.lblBytesIn);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.lblBytesOut);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.cbAutoReconnect);
            this.Controls.Add(this.tbRecordFolder);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.lblRecordState);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.tbNameItem);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.lbGroups);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.lbScenes);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.tbPassword);
            this.Controls.Add(this.nudPort);
            this.Controls.Add(this.tbHostname);
            this.Controls.Add(this.button1);
            this.Margin = new Padding(2);
            this.Name = "Form1";
            this.Text = "OBS Client - Sample Windows Application";
            this.Load += this.Form1_Load;
            ((System.ComponentModel.ISupportInitialize)this.nudPort).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tpGeneralRequests.ResumeLayout(false);
            this.tpConfigRequests.ResumeLayout(false);
            this.tpSourcesRequests.ResumeLayout(false);
            this.tpScenesRequests.ResumeLayout(false);
            this.tpInputsRequests.ResumeLayout(false);
            this.tpOutputsRequests.ResumeLayout(false);
            this.tbRecordRequests.ResumeLayout(false);
            this.tpUIRequests.ResumeLayout(false);
            this.tpMisc.ResumeLayout(false);
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
        private Button btnSetRecordDirectory;
        private Label label8;
        private TextBox tbRecordFolder;
        private CheckBox cbAutoReconnect;
        private Label lblBytesOut;
        private Label label10;
        private Label lblBytesIn;
        private Label label12;
        private Label lblMessagesIn;
        private Label label11;
        private Label lblMessagesOut;
        private Label label14;
        private Label label9;
        private Label lblLastEvent;
        private TabPage tpMisc;
        private Button btnBatch1;
    }
}