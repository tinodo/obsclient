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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.tbHostname = new System.Windows.Forms.TextBox();
            this.nudPort = new System.Windows.Forms.NumericUpDown();
            this.tbPassword = new System.Windows.Forms.TextBox();
            this.btnStartVirtualCam = new System.Windows.Forms.Button();
            this.btnStopVirtualCamera = new System.Windows.Forms.Button();
            this.btnToggleVirtualCamera = new System.Windows.Forms.Button();
            this.btnGetVirtualCameraState = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.button5 = new System.Windows.Forms.Button();
            this.button6 = new System.Windows.Forms.Button();
            this.button7 = new System.Windows.Forms.Button();
            this.btnSleep = new System.Windows.Forms.Button();
            this.btnTriggerHotkeyByKeySequence = new System.Windows.Forms.Button();
            this.btnTriggerHotkeyByName = new System.Windows.Forms.Button();
            this.btnGetHotkeyList = new System.Windows.Forms.Button();
            this.btnCallVendorRequest = new System.Windows.Forms.Button();
            this.btnBroadcastCustomEvent = new System.Windows.Forms.Button();
            this.btnGetStats = new System.Windows.Forms.Button();
            this.btnGetVersion = new System.Windows.Forms.Button();
            this.btnGetProfileList = new System.Windows.Forms.Button();
            this.btnCreateSceneCollection = new System.Windows.Forms.Button();
            this.btnSetCurrentSceneCollection = new System.Windows.Forms.Button();
            this.btnGetSceneCollectionList = new System.Windows.Forms.Button();
            this.btnSetPersistentData = new System.Windows.Forms.Button();
            this.btnGetPersistentData = new System.Windows.Forms.Button();
            this.btnSetCurrentProfile = new System.Windows.Forms.Button();
            this.btnCreateProfile = new System.Windows.Forms.Button();
            this.btnRemoveProfile = new System.Windows.Forms.Button();
            this.btnGetProfileParameter = new System.Windows.Forms.Button();
            this.btnSetProfileParameter = new System.Windows.Forms.Button();
            this.btnGetVideoSettings = new System.Windows.Forms.Button();
            this.btnSetVideoSettings = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tpGeneralRequests = new System.Windows.Forms.TabPage();
            this.tpConfigRequests = new System.Windows.Forms.TabPage();
            this.btnGetRecordDirectory = new System.Windows.Forms.Button();
            this.btnSetStreamServiceSettings = new System.Windows.Forms.Button();
            this.btnGetStreamServiceSettings = new System.Windows.Forms.Button();
            this.tpSourcesRequests = new System.Windows.Forms.TabPage();
            this.btnSaveSourceScreenshot = new System.Windows.Forms.Button();
            this.btnSetSourceActive = new System.Windows.Forms.Button();
            this.btnGetSourceActive = new System.Windows.Forms.Button();
            this.tpScenesRequests = new System.Windows.Forms.TabPage();
            this.btnSetSceneSceneTransitionOverride = new System.Windows.Forms.Button();
            this.btnGetSceneSceneTransitionOverride = new System.Windows.Forms.Button();
            this.btnSetSceneName = new System.Windows.Forms.Button();
            this.btnRemoveScene = new System.Windows.Forms.Button();
            this.btnCreateScene = new System.Windows.Forms.Button();
            this.btnSetCurrentPreviewScene = new System.Windows.Forms.Button();
            this.btnGetCurrentPreviewScene = new System.Windows.Forms.Button();
            this.btnSetCurrentProgramScene = new System.Windows.Forms.Button();
            this.btnGetCurrentProgramScene = new System.Windows.Forms.Button();
            this.btnGetGroupList = new System.Windows.Forms.Button();
            this.btnGetSceneList = new System.Windows.Forms.Button();
            this.tpInputsRequests = new System.Windows.Forms.TabPage();
            this.btnToggleInputMute = new System.Windows.Forms.Button();
            this.btnSetInputMute = new System.Windows.Forms.Button();
            this.btnGetInputMute = new System.Windows.Forms.Button();
            this.btnSetInputSettings = new System.Windows.Forms.Button();
            this.btnGetInputSettings = new System.Windows.Forms.Button();
            this.btnGetInputDefaultSettings = new System.Windows.Forms.Button();
            this.btnSetInputName = new System.Windows.Forms.Button();
            this.btnRemoveInput = new System.Windows.Forms.Button();
            this.btnCreateInput = new System.Windows.Forms.Button();
            this.btnGetSpecialInputs = new System.Windows.Forms.Button();
            this.btnGetInputKindList = new System.Windows.Forms.Button();
            this.btnGetInputList = new System.Windows.Forms.Button();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.lbScenes = new System.Windows.Forms.ListBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lbGroups = new System.Windows.Forms.ListBox();
            this.tbNameItem = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.nudPort)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tpGeneralRequests.SuspendLayout();
            this.tpConfigRequests.SuspendLayout();
            this.tpSourcesRequests.SuspendLayout();
            this.tpScenesRequests.SuspendLayout();
            this.tpInputsRequests.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(382, 8);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(150, 46);
            this.button1.TabIndex = 0;
            this.button1.Text = "Connect";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(382, 101);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(150, 46);
            this.button2.TabIndex = 1;
            this.button2.Text = "Test";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // tbHostname
            // 
            this.tbHostname.Location = new System.Drawing.Point(146, 15);
            this.tbHostname.Name = "tbHostname";
            this.tbHostname.Size = new System.Drawing.Size(200, 39);
            this.tbHostname.TabIndex = 2;
            this.tbHostname.Text = "localhost";
            // 
            // nudPort
            // 
            this.nudPort.Location = new System.Drawing.Point(146, 60);
            this.nudPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.nudPort.Name = "nudPort";
            this.nudPort.Size = new System.Drawing.Size(200, 39);
            this.nudPort.TabIndex = 3;
            this.nudPort.Value = new decimal(new int[] {
            4455,
            0,
            0,
            0});
            // 
            // tbPassword
            // 
            this.tbPassword.Location = new System.Drawing.Point(146, 105);
            this.tbPassword.Name = "tbPassword";
            this.tbPassword.Size = new System.Drawing.Size(200, 39);
            this.tbPassword.TabIndex = 4;
            this.tbPassword.UseSystemPasswordChar = true;
            // 
            // btnStartVirtualCam
            // 
            this.btnStartVirtualCam.Location = new System.Drawing.Point(992, 39);
            this.btnStartVirtualCam.Name = "btnStartVirtualCam";
            this.btnStartVirtualCam.Size = new System.Drawing.Size(340, 46);
            this.btnStartVirtualCam.TabIndex = 5;
            this.btnStartVirtualCam.Text = "Start Virtual Camera";
            this.btnStartVirtualCam.UseVisualStyleBackColor = true;
            this.btnStartVirtualCam.Click += new System.EventHandler(this.btnStartVirtualCam_Click);
            // 
            // btnStopVirtualCamera
            // 
            this.btnStopVirtualCamera.Location = new System.Drawing.Point(992, 91);
            this.btnStopVirtualCamera.Name = "btnStopVirtualCamera";
            this.btnStopVirtualCamera.Size = new System.Drawing.Size(340, 46);
            this.btnStopVirtualCamera.TabIndex = 6;
            this.btnStopVirtualCamera.Text = "Stop Virtual Camera";
            this.btnStopVirtualCamera.UseVisualStyleBackColor = true;
            this.btnStopVirtualCamera.Click += new System.EventHandler(this.btnStopVirtualCamera_Click);
            // 
            // btnToggleVirtualCamera
            // 
            this.btnToggleVirtualCamera.Location = new System.Drawing.Point(992, 143);
            this.btnToggleVirtualCamera.Name = "btnToggleVirtualCamera";
            this.btnToggleVirtualCamera.Size = new System.Drawing.Size(340, 46);
            this.btnToggleVirtualCamera.TabIndex = 7;
            this.btnToggleVirtualCamera.Text = "Toggle Virtual Camera";
            this.btnToggleVirtualCamera.UseVisualStyleBackColor = true;
            this.btnToggleVirtualCamera.Click += new System.EventHandler(this.btnToggleVirtualCamera_Click);
            // 
            // btnGetVirtualCameraState
            // 
            this.btnGetVirtualCameraState.Location = new System.Drawing.Point(992, 195);
            this.btnGetVirtualCameraState.Name = "btnGetVirtualCameraState";
            this.btnGetVirtualCameraState.Size = new System.Drawing.Size(340, 46);
            this.btnGetVirtualCameraState.TabIndex = 8;
            this.btnGetVirtualCameraState.Text = "Get Virtual Camera Status";
            this.btnGetVirtualCameraState.UseVisualStyleBackColor = true;
            this.btnGetVirtualCameraState.Click += new System.EventHandler(this.btnGetVirtualCameraState_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(382, 55);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(150, 46);
            this.button3.TabIndex = 9;
            this.button3.Text = "Disconnect";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(1338, 195);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(340, 46);
            this.button4.TabIndex = 13;
            this.button4.Text = "Get Monitor List";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(1338, 143);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(340, 46);
            this.button5.TabIndex = 12;
            this.button5.Text = "Get Studio Mode";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(1338, 91);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(340, 46);
            this.button6.TabIndex = 11;
            this.button6.Text = "Stop Studio Mode";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // button7
            // 
            this.button7.Location = new System.Drawing.Point(1338, 39);
            this.button7.Name = "button7";
            this.button7.Size = new System.Drawing.Size(340, 46);
            this.button7.TabIndex = 10;
            this.button7.Text = "Start Studio Mode";
            this.button7.UseVisualStyleBackColor = true;
            this.button7.Click += new System.EventHandler(this.button7_Click);
            // 
            // btnSleep
            // 
            this.btnSleep.Enabled = false;
            this.btnSleep.Location = new System.Drawing.Point(17, 330);
            this.btnSleep.Name = "btnSleep";
            this.btnSleep.Size = new System.Drawing.Size(340, 46);
            this.btnSleep.TabIndex = 22;
            this.btnSleep.Text = "Sleep";
            this.btnSleep.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByKeySequence
            // 
            this.btnTriggerHotkeyByKeySequence.Enabled = false;
            this.btnTriggerHotkeyByKeySequence.Location = new System.Drawing.Point(17, 278);
            this.btnTriggerHotkeyByKeySequence.Name = "btnTriggerHotkeyByKeySequence";
            this.btnTriggerHotkeyByKeySequence.Size = new System.Drawing.Size(381, 46);
            this.btnTriggerHotkeyByKeySequence.TabIndex = 21;
            this.btnTriggerHotkeyByKeySequence.Text = "Trigger Hotkey By Key Sequence";
            this.btnTriggerHotkeyByKeySequence.UseVisualStyleBackColor = true;
            // 
            // btnTriggerHotkeyByName
            // 
            this.btnTriggerHotkeyByName.Enabled = false;
            this.btnTriggerHotkeyByName.Location = new System.Drawing.Point(17, 226);
            this.btnTriggerHotkeyByName.Name = "btnTriggerHotkeyByName";
            this.btnTriggerHotkeyByName.Size = new System.Drawing.Size(340, 46);
            this.btnTriggerHotkeyByName.TabIndex = 20;
            this.btnTriggerHotkeyByName.Text = "Trigger Hotkey By Name";
            this.btnTriggerHotkeyByName.UseVisualStyleBackColor = true;
            // 
            // btnGetHotkeyList
            // 
            this.btnGetHotkeyList.Location = new System.Drawing.Point(17, 174);
            this.btnGetHotkeyList.Name = "btnGetHotkeyList";
            this.btnGetHotkeyList.Size = new System.Drawing.Size(340, 46);
            this.btnGetHotkeyList.TabIndex = 19;
            this.btnGetHotkeyList.Text = "Get Hotkey List";
            this.btnGetHotkeyList.UseVisualStyleBackColor = true;
            this.btnGetHotkeyList.Click += new System.EventHandler(this.btnGetHotkeyList_Click);
            // 
            // btnCallVendorRequest
            // 
            this.btnCallVendorRequest.Enabled = false;
            this.btnCallVendorRequest.Location = new System.Drawing.Point(17, 122);
            this.btnCallVendorRequest.Name = "btnCallVendorRequest";
            this.btnCallVendorRequest.Size = new System.Drawing.Size(340, 46);
            this.btnCallVendorRequest.TabIndex = 18;
            this.btnCallVendorRequest.Text = "Call Vendor Request";
            this.btnCallVendorRequest.UseVisualStyleBackColor = true;
            // 
            // btnBroadcastCustomEvent
            // 
            this.btnBroadcastCustomEvent.Enabled = false;
            this.btnBroadcastCustomEvent.Location = new System.Drawing.Point(17, 70);
            this.btnBroadcastCustomEvent.Name = "btnBroadcastCustomEvent";
            this.btnBroadcastCustomEvent.Size = new System.Drawing.Size(340, 46);
            this.btnBroadcastCustomEvent.TabIndex = 17;
            this.btnBroadcastCustomEvent.Text = "Broadcast Custom Event";
            this.btnBroadcastCustomEvent.UseVisualStyleBackColor = true;
            // 
            // btnGetStats
            // 
            this.btnGetStats.Location = new System.Drawing.Point(194, 18);
            this.btnGetStats.Name = "btnGetStats";
            this.btnGetStats.Size = new System.Drawing.Size(163, 46);
            this.btnGetStats.TabIndex = 16;
            this.btnGetStats.Text = "Get Stats";
            this.btnGetStats.UseVisualStyleBackColor = true;
            this.btnGetStats.Click += new System.EventHandler(this.btnGetStats_Click);
            // 
            // btnGetVersion
            // 
            this.btnGetVersion.Location = new System.Drawing.Point(17, 18);
            this.btnGetVersion.Name = "btnGetVersion";
            this.btnGetVersion.Size = new System.Drawing.Size(163, 46);
            this.btnGetVersion.TabIndex = 15;
            this.btnGetVersion.Text = "Get Version";
            this.btnGetVersion.UseVisualStyleBackColor = true;
            this.btnGetVersion.Click += new System.EventHandler(this.btnGetVersion_Click);
            // 
            // btnGetProfileList
            // 
            this.btnGetProfileList.Location = new System.Drawing.Point(18, 278);
            this.btnGetProfileList.Name = "btnGetProfileList";
            this.btnGetProfileList.Size = new System.Drawing.Size(340, 46);
            this.btnGetProfileList.TabIndex = 28;
            this.btnGetProfileList.Text = "Get Profile List";
            this.btnGetProfileList.UseVisualStyleBackColor = true;
            this.btnGetProfileList.Click += new System.EventHandler(this.btnGetProfileList_Click);
            // 
            // btnCreateSceneCollection
            // 
            this.btnCreateSceneCollection.Enabled = false;
            this.btnCreateSceneCollection.Location = new System.Drawing.Point(18, 226);
            this.btnCreateSceneCollection.Name = "btnCreateSceneCollection";
            this.btnCreateSceneCollection.Size = new System.Drawing.Size(340, 46);
            this.btnCreateSceneCollection.TabIndex = 27;
            this.btnCreateSceneCollection.Text = "Create Scene Collection";
            this.btnCreateSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentSceneCollection
            // 
            this.btnSetCurrentSceneCollection.Enabled = false;
            this.btnSetCurrentSceneCollection.Location = new System.Drawing.Point(18, 174);
            this.btnSetCurrentSceneCollection.Name = "btnSetCurrentSceneCollection";
            this.btnSetCurrentSceneCollection.Size = new System.Drawing.Size(340, 46);
            this.btnSetCurrentSceneCollection.TabIndex = 26;
            this.btnSetCurrentSceneCollection.Text = "Set Current Scene Collection";
            this.btnSetCurrentSceneCollection.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneCollectionList
            // 
            this.btnGetSceneCollectionList.Location = new System.Drawing.Point(18, 122);
            this.btnGetSceneCollectionList.Name = "btnGetSceneCollectionList";
            this.btnGetSceneCollectionList.Size = new System.Drawing.Size(340, 46);
            this.btnGetSceneCollectionList.TabIndex = 25;
            this.btnGetSceneCollectionList.Text = "Get Scene Collection List";
            this.btnGetSceneCollectionList.UseVisualStyleBackColor = true;
            this.btnGetSceneCollectionList.Click += new System.EventHandler(this.btnGetSceneCollectionList_Click);
            // 
            // btnSetPersistentData
            // 
            this.btnSetPersistentData.Enabled = false;
            this.btnSetPersistentData.Location = new System.Drawing.Point(18, 70);
            this.btnSetPersistentData.Name = "btnSetPersistentData";
            this.btnSetPersistentData.Size = new System.Drawing.Size(340, 46);
            this.btnSetPersistentData.TabIndex = 24;
            this.btnSetPersistentData.Text = "Set Persistent Data";
            this.btnSetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnGetPersistentData
            // 
            this.btnGetPersistentData.Enabled = false;
            this.btnGetPersistentData.Location = new System.Drawing.Point(18, 18);
            this.btnGetPersistentData.Name = "btnGetPersistentData";
            this.btnGetPersistentData.Size = new System.Drawing.Size(340, 46);
            this.btnGetPersistentData.TabIndex = 23;
            this.btnGetPersistentData.Text = "Get Persistent Data";
            this.btnGetPersistentData.UseVisualStyleBackColor = true;
            // 
            // btnSetCurrentProfile
            // 
            this.btnSetCurrentProfile.Enabled = false;
            this.btnSetCurrentProfile.Location = new System.Drawing.Point(18, 330);
            this.btnSetCurrentProfile.Name = "btnSetCurrentProfile";
            this.btnSetCurrentProfile.Size = new System.Drawing.Size(340, 46);
            this.btnSetCurrentProfile.TabIndex = 29;
            this.btnSetCurrentProfile.Text = "Set Current Profile";
            this.btnSetCurrentProfile.UseVisualStyleBackColor = true;
            // 
            // btnCreateProfile
            // 
            this.btnCreateProfile.Enabled = false;
            this.btnCreateProfile.Location = new System.Drawing.Point(18, 382);
            this.btnCreateProfile.Name = "btnCreateProfile";
            this.btnCreateProfile.Size = new System.Drawing.Size(340, 46);
            this.btnCreateProfile.TabIndex = 30;
            this.btnCreateProfile.Text = "Create Profile";
            this.btnCreateProfile.UseVisualStyleBackColor = true;
            // 
            // btnRemoveProfile
            // 
            this.btnRemoveProfile.Enabled = false;
            this.btnRemoveProfile.Location = new System.Drawing.Point(364, 18);
            this.btnRemoveProfile.Name = "btnRemoveProfile";
            this.btnRemoveProfile.Size = new System.Drawing.Size(340, 46);
            this.btnRemoveProfile.TabIndex = 31;
            this.btnRemoveProfile.Text = "Remove Profile";
            this.btnRemoveProfile.UseVisualStyleBackColor = true;
            // 
            // btnGetProfileParameter
            // 
            this.btnGetProfileParameter.Enabled = false;
            this.btnGetProfileParameter.Location = new System.Drawing.Point(364, 70);
            this.btnGetProfileParameter.Name = "btnGetProfileParameter";
            this.btnGetProfileParameter.Size = new System.Drawing.Size(340, 46);
            this.btnGetProfileParameter.TabIndex = 32;
            this.btnGetProfileParameter.Text = "Get Profile Parameter";
            this.btnGetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnSetProfileParameter
            // 
            this.btnSetProfileParameter.Enabled = false;
            this.btnSetProfileParameter.Location = new System.Drawing.Point(364, 122);
            this.btnSetProfileParameter.Name = "btnSetProfileParameter";
            this.btnSetProfileParameter.Size = new System.Drawing.Size(340, 46);
            this.btnSetProfileParameter.TabIndex = 33;
            this.btnSetProfileParameter.Text = "Set Profile Parameter";
            this.btnSetProfileParameter.UseVisualStyleBackColor = true;
            // 
            // btnGetVideoSettings
            // 
            this.btnGetVideoSettings.Enabled = false;
            this.btnGetVideoSettings.Location = new System.Drawing.Point(364, 174);
            this.btnGetVideoSettings.Name = "btnGetVideoSettings";
            this.btnGetVideoSettings.Size = new System.Drawing.Size(340, 46);
            this.btnGetVideoSettings.TabIndex = 34;
            this.btnGetVideoSettings.Text = "Get Video Settings";
            this.btnGetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetVideoSettings
            // 
            this.btnSetVideoSettings.Enabled = false;
            this.btnSetVideoSettings.Location = new System.Drawing.Point(364, 226);
            this.btnSetVideoSettings.Name = "btnSetVideoSettings";
            this.btnSetVideoSettings.Size = new System.Drawing.Size(340, 46);
            this.btnSetVideoSettings.TabIndex = 35;
            this.btnSetVideoSettings.Text = "Set Video Settings";
            this.btnSetVideoSettings.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(128, 32);
            this.label1.TabIndex = 16;
            this.label1.Text = "Hostname:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(61, 32);
            this.label2.TabIndex = 17;
            this.label2.Text = "Port:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 105);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(116, 32);
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
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Location = new System.Drawing.Point(23, 261);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1257, 504);
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
            this.tpGeneralRequests.Location = new System.Drawing.Point(8, 46);
            this.tpGeneralRequests.Name = "tpGeneralRequests";
            this.tpGeneralRequests.Padding = new System.Windows.Forms.Padding(3);
            this.tpGeneralRequests.Size = new System.Drawing.Size(1241, 450);
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
            this.tpConfigRequests.Location = new System.Drawing.Point(8, 46);
            this.tpConfigRequests.Name = "tpConfigRequests";
            this.tpConfigRequests.Padding = new System.Windows.Forms.Padding(3);
            this.tpConfigRequests.Size = new System.Drawing.Size(1241, 450);
            this.tpConfigRequests.TabIndex = 1;
            this.tpConfigRequests.Text = "Config Requests";
            this.tpConfigRequests.UseVisualStyleBackColor = true;
            // 
            // btnGetRecordDirectory
            // 
            this.btnGetRecordDirectory.Location = new System.Drawing.Point(364, 382);
            this.btnGetRecordDirectory.Name = "btnGetRecordDirectory";
            this.btnGetRecordDirectory.Size = new System.Drawing.Size(340, 46);
            this.btnGetRecordDirectory.TabIndex = 38;
            this.btnGetRecordDirectory.Text = "Get Record Directory";
            this.btnGetRecordDirectory.UseVisualStyleBackColor = true;
            this.btnGetRecordDirectory.Click += new System.EventHandler(this.btnGetRecordDirectory_Click);
            // 
            // btnSetStreamServiceSettings
            // 
            this.btnSetStreamServiceSettings.Enabled = false;
            this.btnSetStreamServiceSettings.Location = new System.Drawing.Point(364, 330);
            this.btnSetStreamServiceSettings.Name = "btnSetStreamServiceSettings";
            this.btnSetStreamServiceSettings.Size = new System.Drawing.Size(340, 46);
            this.btnSetStreamServiceSettings.TabIndex = 37;
            this.btnSetStreamServiceSettings.Text = "Set Stream Service Settings";
            this.btnSetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetStreamServiceSettings
            // 
            this.btnGetStreamServiceSettings.Enabled = false;
            this.btnGetStreamServiceSettings.Location = new System.Drawing.Point(364, 278);
            this.btnGetStreamServiceSettings.Name = "btnGetStreamServiceSettings";
            this.btnGetStreamServiceSettings.Size = new System.Drawing.Size(340, 46);
            this.btnGetStreamServiceSettings.TabIndex = 36;
            this.btnGetStreamServiceSettings.Text = "Get Stream Service Settings";
            this.btnGetStreamServiceSettings.UseVisualStyleBackColor = true;
            // 
            // tpSourcesRequests
            // 
            this.tpSourcesRequests.Controls.Add(this.btnSaveSourceScreenshot);
            this.tpSourcesRequests.Controls.Add(this.btnSetSourceActive);
            this.tpSourcesRequests.Controls.Add(this.btnGetSourceActive);
            this.tpSourcesRequests.Location = new System.Drawing.Point(8, 46);
            this.tpSourcesRequests.Name = "tpSourcesRequests";
            this.tpSourcesRequests.Size = new System.Drawing.Size(1241, 450);
            this.tpSourcesRequests.TabIndex = 2;
            this.tpSourcesRequests.Text = "Sources Requests";
            this.tpSourcesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSaveSourceScreenshot
            // 
            this.btnSaveSourceScreenshot.Enabled = false;
            this.btnSaveSourceScreenshot.Location = new System.Drawing.Point(3, 107);
            this.btnSaveSourceScreenshot.Name = "btnSaveSourceScreenshot";
            this.btnSaveSourceScreenshot.Size = new System.Drawing.Size(340, 46);
            this.btnSaveSourceScreenshot.TabIndex = 22;
            this.btnSaveSourceScreenshot.Text = "Save Source Screenshot";
            this.btnSaveSourceScreenshot.UseVisualStyleBackColor = true;
            // 
            // btnSetSourceActive
            // 
            this.btnSetSourceActive.Enabled = false;
            this.btnSetSourceActive.Location = new System.Drawing.Point(3, 55);
            this.btnSetSourceActive.Name = "btnSetSourceActive";
            this.btnSetSourceActive.Size = new System.Drawing.Size(340, 46);
            this.btnSetSourceActive.TabIndex = 21;
            this.btnSetSourceActive.Text = "Set Source Active";
            this.btnSetSourceActive.UseVisualStyleBackColor = true;
            // 
            // btnGetSourceActive
            // 
            this.btnGetSourceActive.Enabled = false;
            this.btnGetSourceActive.Location = new System.Drawing.Point(3, 3);
            this.btnGetSourceActive.Name = "btnGetSourceActive";
            this.btnGetSourceActive.Size = new System.Drawing.Size(340, 46);
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
            this.tpScenesRequests.Location = new System.Drawing.Point(8, 46);
            this.tpScenesRequests.Name = "tpScenesRequests";
            this.tpScenesRequests.Size = new System.Drawing.Size(1241, 450);
            this.tpScenesRequests.TabIndex = 3;
            this.tpScenesRequests.Text = "Scenes Requests";
            this.tpScenesRequests.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneSceneTransitionOverride
            // 
            this.btnSetSceneSceneTransitionOverride.Enabled = false;
            this.btnSetSceneSceneTransitionOverride.Location = new System.Drawing.Point(351, 107);
            this.btnSetSceneSceneTransitionOverride.Name = "btnSetSceneSceneTransitionOverride";
            this.btnSetSceneSceneTransitionOverride.Size = new System.Drawing.Size(460, 46);
            this.btnSetSceneSceneTransitionOverride.TabIndex = 30;
            this.btnSetSceneSceneTransitionOverride.Text = "Set Scene Scene Transition Override";
            this.btnSetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnGetSceneSceneTransitionOverride
            // 
            this.btnGetSceneSceneTransitionOverride.Enabled = false;
            this.btnGetSceneSceneTransitionOverride.Location = new System.Drawing.Point(351, 55);
            this.btnGetSceneSceneTransitionOverride.Name = "btnGetSceneSceneTransitionOverride";
            this.btnGetSceneSceneTransitionOverride.Size = new System.Drawing.Size(460, 46);
            this.btnGetSceneSceneTransitionOverride.TabIndex = 29;
            this.btnGetSceneSceneTransitionOverride.Text = "Get Scene Scene Transition Override";
            this.btnGetSceneSceneTransitionOverride.UseVisualStyleBackColor = true;
            // 
            // btnSetSceneName
            // 
            this.btnSetSceneName.Location = new System.Drawing.Point(351, 3);
            this.btnSetSceneName.Name = "btnSetSceneName";
            this.btnSetSceneName.Size = new System.Drawing.Size(340, 46);
            this.btnSetSceneName.TabIndex = 28;
            this.btnSetSceneName.Text = "Set Scene Name";
            this.btnSetSceneName.UseVisualStyleBackColor = true;
            this.btnSetSceneName.Click += new System.EventHandler(this.btnSetSceneName_Click);
            // 
            // btnRemoveScene
            // 
            this.btnRemoveScene.Location = new System.Drawing.Point(3, 367);
            this.btnRemoveScene.Name = "btnRemoveScene";
            this.btnRemoveScene.Size = new System.Drawing.Size(340, 46);
            this.btnRemoveScene.TabIndex = 27;
            this.btnRemoveScene.Text = "Remove Scene";
            this.btnRemoveScene.UseVisualStyleBackColor = true;
            this.btnRemoveScene.Click += new System.EventHandler(this.btnRemoveScene_Click);
            // 
            // btnCreateScene
            // 
            this.btnCreateScene.Location = new System.Drawing.Point(3, 315);
            this.btnCreateScene.Name = "btnCreateScene";
            this.btnCreateScene.Size = new System.Drawing.Size(340, 46);
            this.btnCreateScene.TabIndex = 26;
            this.btnCreateScene.Text = "Create Scene";
            this.btnCreateScene.UseVisualStyleBackColor = true;
            this.btnCreateScene.Click += new System.EventHandler(this.btnCreateScene_Click);
            // 
            // btnSetCurrentPreviewScene
            // 
            this.btnSetCurrentPreviewScene.Location = new System.Drawing.Point(3, 263);
            this.btnSetCurrentPreviewScene.Name = "btnSetCurrentPreviewScene";
            this.btnSetCurrentPreviewScene.Size = new System.Drawing.Size(340, 46);
            this.btnSetCurrentPreviewScene.TabIndex = 25;
            this.btnSetCurrentPreviewScene.Text = "Set Current Preview Scene";
            this.btnSetCurrentPreviewScene.UseVisualStyleBackColor = true;
            this.btnSetCurrentPreviewScene.Click += new System.EventHandler(this.btnSetCurrentPreviewScene_Click);
            // 
            // btnGetCurrentPreviewScene
            // 
            this.btnGetCurrentPreviewScene.Location = new System.Drawing.Point(3, 211);
            this.btnGetCurrentPreviewScene.Name = "btnGetCurrentPreviewScene";
            this.btnGetCurrentPreviewScene.Size = new System.Drawing.Size(340, 46);
            this.btnGetCurrentPreviewScene.TabIndex = 24;
            this.btnGetCurrentPreviewScene.Text = "Get Current Preview Scene";
            this.btnGetCurrentPreviewScene.UseVisualStyleBackColor = true;
            this.btnGetCurrentPreviewScene.Click += new System.EventHandler(this.btnGetCurrentPreviewScene_Click);
            // 
            // btnSetCurrentProgramScene
            // 
            this.btnSetCurrentProgramScene.Location = new System.Drawing.Point(3, 159);
            this.btnSetCurrentProgramScene.Name = "btnSetCurrentProgramScene";
            this.btnSetCurrentProgramScene.Size = new System.Drawing.Size(340, 46);
            this.btnSetCurrentProgramScene.TabIndex = 23;
            this.btnSetCurrentProgramScene.Text = "Set Current Program Scene";
            this.btnSetCurrentProgramScene.UseVisualStyleBackColor = true;
            this.btnSetCurrentProgramScene.Click += new System.EventHandler(this.btnSetCurrentProgramScene_Click);
            // 
            // btnGetCurrentProgramScene
            // 
            this.btnGetCurrentProgramScene.Location = new System.Drawing.Point(3, 107);
            this.btnGetCurrentProgramScene.Name = "btnGetCurrentProgramScene";
            this.btnGetCurrentProgramScene.Size = new System.Drawing.Size(340, 46);
            this.btnGetCurrentProgramScene.TabIndex = 22;
            this.btnGetCurrentProgramScene.Text = "Get Current Program Scene";
            this.btnGetCurrentProgramScene.UseVisualStyleBackColor = true;
            this.btnGetCurrentProgramScene.Click += new System.EventHandler(this.btnGetCurrentProgramScene_Click);
            // 
            // btnGetGroupList
            // 
            this.btnGetGroupList.Location = new System.Drawing.Point(3, 55);
            this.btnGetGroupList.Name = "btnGetGroupList";
            this.btnGetGroupList.Size = new System.Drawing.Size(340, 46);
            this.btnGetGroupList.TabIndex = 21;
            this.btnGetGroupList.Text = "Get Group List";
            this.btnGetGroupList.UseVisualStyleBackColor = true;
            this.btnGetGroupList.Click += new System.EventHandler(this.btnGetGroupList_Click);
            // 
            // btnGetSceneList
            // 
            this.btnGetSceneList.Location = new System.Drawing.Point(3, 3);
            this.btnGetSceneList.Name = "btnGetSceneList";
            this.btnGetSceneList.Size = new System.Drawing.Size(340, 46);
            this.btnGetSceneList.TabIndex = 20;
            this.btnGetSceneList.Text = "Get Scene List";
            this.btnGetSceneList.UseVisualStyleBackColor = true;
            this.btnGetSceneList.Click += new System.EventHandler(this.btnGetSceneList_Click);
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
            this.tpInputsRequests.Location = new System.Drawing.Point(8, 46);
            this.tpInputsRequests.Name = "tpInputsRequests";
            this.tpInputsRequests.Size = new System.Drawing.Size(1241, 450);
            this.tpInputsRequests.TabIndex = 4;
            this.tpInputsRequests.Text = "Inputs Requests";
            this.tpInputsRequests.UseVisualStyleBackColor = true;
            // 
            // btnToggleInputMute
            // 
            this.btnToggleInputMute.Enabled = false;
            this.btnToggleInputMute.Location = new System.Drawing.Point(351, 159);
            this.btnToggleInputMute.Name = "btnToggleInputMute";
            this.btnToggleInputMute.Size = new System.Drawing.Size(340, 46);
            this.btnToggleInputMute.TabIndex = 32;
            this.btnToggleInputMute.Text = "Toggle Input Mute";
            this.btnToggleInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputMute
            // 
            this.btnSetInputMute.Enabled = false;
            this.btnSetInputMute.Location = new System.Drawing.Point(351, 107);
            this.btnSetInputMute.Name = "btnSetInputMute";
            this.btnSetInputMute.Size = new System.Drawing.Size(340, 46);
            this.btnSetInputMute.TabIndex = 31;
            this.btnSetInputMute.Text = "Set Input Mute";
            this.btnSetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnGetInputMute
            // 
            this.btnGetInputMute.Enabled = false;
            this.btnGetInputMute.Location = new System.Drawing.Point(351, 55);
            this.btnGetInputMute.Name = "btnGetInputMute";
            this.btnGetInputMute.Size = new System.Drawing.Size(340, 46);
            this.btnGetInputMute.TabIndex = 30;
            this.btnGetInputMute.Text = "Get Input Mute";
            this.btnGetInputMute.UseVisualStyleBackColor = true;
            // 
            // btnSetInputSettings
            // 
            this.btnSetInputSettings.Enabled = false;
            this.btnSetInputSettings.Location = new System.Drawing.Point(351, 3);
            this.btnSetInputSettings.Name = "btnSetInputSettings";
            this.btnSetInputSettings.Size = new System.Drawing.Size(340, 46);
            this.btnSetInputSettings.TabIndex = 29;
            this.btnSetInputSettings.Text = "Set Input Settings";
            this.btnSetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputSettings
            // 
            this.btnGetInputSettings.Enabled = false;
            this.btnGetInputSettings.Location = new System.Drawing.Point(3, 367);
            this.btnGetInputSettings.Name = "btnGetInputSettings";
            this.btnGetInputSettings.Size = new System.Drawing.Size(340, 46);
            this.btnGetInputSettings.TabIndex = 28;
            this.btnGetInputSettings.Text = "Get Input Settings";
            this.btnGetInputSettings.UseVisualStyleBackColor = true;
            // 
            // btnGetInputDefaultSettings
            // 
            this.btnGetInputDefaultSettings.Enabled = false;
            this.btnGetInputDefaultSettings.Location = new System.Drawing.Point(3, 315);
            this.btnGetInputDefaultSettings.Name = "btnGetInputDefaultSettings";
            this.btnGetInputDefaultSettings.Size = new System.Drawing.Size(340, 46);
            this.btnGetInputDefaultSettings.TabIndex = 27;
            this.btnGetInputDefaultSettings.Text = "Get Input Default Settings";
            this.btnGetInputDefaultSettings.UseVisualStyleBackColor = true;
            // 
            // btnSetInputName
            // 
            this.btnSetInputName.Enabled = false;
            this.btnSetInputName.Location = new System.Drawing.Point(3, 263);
            this.btnSetInputName.Name = "btnSetInputName";
            this.btnSetInputName.Size = new System.Drawing.Size(340, 46);
            this.btnSetInputName.TabIndex = 26;
            this.btnSetInputName.Text = "Set Input Name";
            this.btnSetInputName.UseVisualStyleBackColor = true;
            // 
            // btnRemoveInput
            // 
            this.btnRemoveInput.Enabled = false;
            this.btnRemoveInput.Location = new System.Drawing.Point(3, 211);
            this.btnRemoveInput.Name = "btnRemoveInput";
            this.btnRemoveInput.Size = new System.Drawing.Size(340, 46);
            this.btnRemoveInput.TabIndex = 25;
            this.btnRemoveInput.Text = "Remove Input";
            this.btnRemoveInput.UseVisualStyleBackColor = true;
            // 
            // btnCreateInput
            // 
            this.btnCreateInput.Enabled = false;
            this.btnCreateInput.Location = new System.Drawing.Point(0, 159);
            this.btnCreateInput.Name = "btnCreateInput";
            this.btnCreateInput.Size = new System.Drawing.Size(340, 46);
            this.btnCreateInput.TabIndex = 24;
            this.btnCreateInput.Text = "Create Input";
            this.btnCreateInput.UseVisualStyleBackColor = true;
            // 
            // btnGetSpecialInputs
            // 
            this.btnGetSpecialInputs.Location = new System.Drawing.Point(0, 107);
            this.btnGetSpecialInputs.Name = "btnGetSpecialInputs";
            this.btnGetSpecialInputs.Size = new System.Drawing.Size(340, 46);
            this.btnGetSpecialInputs.TabIndex = 23;
            this.btnGetSpecialInputs.Text = "Get Special Inputs";
            this.btnGetSpecialInputs.UseVisualStyleBackColor = true;
            this.btnGetSpecialInputs.Click += new System.EventHandler(this.btnGetSpecialInputs_Click);
            // 
            // btnGetInputKindList
            // 
            this.btnGetInputKindList.Location = new System.Drawing.Point(3, 55);
            this.btnGetInputKindList.Name = "btnGetInputKindList";
            this.btnGetInputKindList.Size = new System.Drawing.Size(340, 46);
            this.btnGetInputKindList.TabIndex = 22;
            this.btnGetInputKindList.Text = "Get Input Kind List";
            this.btnGetInputKindList.UseVisualStyleBackColor = true;
            this.btnGetInputKindList.Click += new System.EventHandler(this.btnGetInputKindList_Click);
            // 
            // btnGetInputList
            // 
            this.btnGetInputList.Location = new System.Drawing.Point(3, 3);
            this.btnGetInputList.Name = "btnGetInputList";
            this.btnGetInputList.Size = new System.Drawing.Size(340, 46);
            this.btnGetInputList.TabIndex = 21;
            this.btnGetInputList.Text = "Get Input List";
            this.btnGetInputList.UseVisualStyleBackColor = true;
            this.btnGetInputList.Click += new System.EventHandler(this.btnGetInputList_Click);
            // 
            // tabPage4
            // 
            this.tabPage4.Location = new System.Drawing.Point(8, 46);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Size = new System.Drawing.Size(1241, 450);
            this.tabPage4.TabIndex = 5;
            this.tabPage4.Text = "tabPage4";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // lbScenes
            // 
            this.lbScenes.FormattingEnabled = true;
            this.lbScenes.ItemHeight = 32;
            this.lbScenes.Location = new System.Drawing.Point(1307, 311);
            this.lbScenes.Name = "lbScenes";
            this.lbScenes.Size = new System.Drawing.Size(288, 452);
            this.lbScenes.TabIndex = 20;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(1307, 261);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(93, 32);
            this.label4.TabIndex = 21;
            this.label4.Text = "Scenes:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(1601, 260);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(95, 32);
            this.label5.TabIndex = 23;
            this.label5.Text = "Groups:";
            // 
            // lbGroups
            // 
            this.lbGroups.FormattingEnabled = true;
            this.lbGroups.ItemHeight = 32;
            this.lbGroups.Location = new System.Drawing.Point(1601, 310);
            this.lbGroups.Name = "lbGroups";
            this.lbGroups.Size = new System.Drawing.Size(288, 452);
            this.lbGroups.TabIndex = 22;
            // 
            // tbNameItem
            // 
            this.tbNameItem.Location = new System.Drawing.Point(167, 192);
            this.tbNameItem.Name = "tbNameItem";
            this.tbNameItem.Size = new System.Drawing.Size(200, 39);
            this.tbNameItem.TabIndex = 24;
            this.tbNameItem.Text = "Some Item Name";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(23, 195);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(138, 32);
            this.label6.TabIndex = 25;
            this.label6.Text = "Name Item:";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2196, 871);
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
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.button6);
            this.Controls.Add(this.button7);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.btnGetVirtualCameraState);
            this.Controls.Add(this.btnToggleVirtualCamera);
            this.Controls.Add(this.btnStopVirtualCamera);
            this.Controls.Add(this.btnStartVirtualCam);
            this.Controls.Add(this.tbPassword);
            this.Controls.Add(this.nudPort);
            this.Controls.Add(this.tbHostname);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.nudPort)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tpGeneralRequests.ResumeLayout(false);
            this.tpConfigRequests.ResumeLayout(false);
            this.tpSourcesRequests.ResumeLayout(false);
            this.tpScenesRequests.ResumeLayout(false);
            this.tpInputsRequests.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button button1;
        private Button button2;
        private TextBox tbHostname;
        private NumericUpDown nudPort;
        private TextBox tbPassword;
        private Button btnStartVirtualCam;
        private Button btnStopVirtualCamera;
        private Button btnToggleVirtualCamera;
        private Button btnGetVirtualCameraState;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button6;
        private Button button7;
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
        private TabPage tabPage4;
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
    }
}