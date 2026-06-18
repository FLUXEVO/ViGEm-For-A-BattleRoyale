using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace VigemStickDriftUi
{
    public class MainForm : Form
    {
        private const string ActionACross = "A / Cross";
        private const string ActionBCircle = "B / Circle";
        private const string ActionXSquare = "X / Square";
        private const string ActionYTriangle = "Y / Triangle";
        private const string ActionLbL1 = "LB / L1";
        private const string ActionRbR1 = "RB / R1";
        private const string ActionLtL2 = "LT / L2";
        private const string ActionRtR2 = "RT / R2";
        private const string ActionBackShare = "Back / Share";
        private const string ActionStartOptions = "Start / Options";
        private const string ActionLsL3 = "LS / L3";
        private const string ActionRsR3 = "RS / R3";
        private const string ActionDPadUp = "DPad Up";
        private const string ActionDPadDown = "DPad Down";
        private const string ActionDPadLeft = "DPad Left";
        private const string ActionDPadRight = "DPad Right";

        private static readonly string[] BindingActions =
        {
            ActionACross, ActionBCircle, ActionXSquare, ActionYTriangle,
            ActionLbL1, ActionRbR1, ActionLtL2, ActionRtR2,
            ActionBackShare, ActionStartOptions, ActionLsL3, ActionRsR3,
            ActionDPadUp, ActionDPadDown, ActionDPadLeft, ActionDPadRight
        };

        private readonly string profilesDirectory = Path.Combine(AppContext.BaseDirectory, "profiles");
        private readonly string patternsDirectory = Path.Combine(AppContext.BaseDirectory, "Patterns");
        private readonly Dictionary<string, BindingControlSet> bindingControls = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PatternStep> customPatternPoints = new();
        private bool isConnected;

        // Core Refactored Managers
        private readonly ControllerManager controllerManager = new();
        private readonly JitterEngine jitterEngine = new();
        private readonly InputBindingManager inputBindingManager = new();

        private ComboBox controllerTypeBox;
        private Button connectButton;
        private Button disconnectButton;
        private Button resetButton;
        private TrackBar driftSlider;
        private Label valueLabel;
        private Label statusLabel;
        private Label titleLabel;
        private Label hintLabel;
        private ComboBox profileComboBox;
        private Button saveProfileButton;
        private Button loadProfileButton;
        private Button deleteProfileButton;
        private NumericUpDown jitterDelayBox;
        private NumericUpDown holdExtraDriftBox;
        private ComboBox pullDirectionBox;
        private NumericUpDown jitterStrengthBox;
        private NumericUpDown scrollDisengageBox;
        private ComboBox jitterHoldKeyBox;
        private ComboBox jitterPatternBox;
        private TextBox customPatternTextBox;
        private Panel patternPreviewPanel;
        private CheckedListBox disableKeysList;
        private Button wheelDisengageToggleButton;
        private Button clearPatternButton;
        private Button useShakePresetButton;
        private Button useCirclePresetButton;
        private ComboBox patternComboBox;
        private Button savePatternButton;
        private Button loadPatternButton;
        private Button deletePatternButton;
        private System.Windows.Forms.Timer updateTimer;

        private GlobalInputHook inputHook;
        private bool isTemporarilyDisengaged;
        private bool wheelDisengageEnabled = true;
        private DateTime? disengagedUntilUtc;

        private sealed class BindingControlSet
        {
            public ComboBox KeySelector { get; set; }
            public CheckBox TurboToggle { get; set; }
            public NumericUpDown TurboHzBox { get; set; }
        }

        public MainForm()
        {
            InitializeUi();
            InitializeInputHook();
            EnsureProfilesDirectory();
            EnsurePatternsDirectory();
            LoadProfileList();
            LoadPatternList();
            ApplyDefaults();
        }

        private void InitializeUi()
        {
            Text = "ViGEm Stick Drift UI V8 - Refactored";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 860);
            MinimumSize = new Size(1120, 760);
            SuspendLayout();

            titleLabel = new Label { AutoSize = true, Text = "Virtual Right Stick Drift V8", Font = new Font(Font.FontFamily, 13, FontStyle.Bold), Margin = new Padding(0, 0, 20, 0) };
            controllerTypeBox = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 10, 0) };
            controllerTypeBox.Items.AddRange(new object[] { "Xbox 360", "PS4" });
            controllerTypeBox.SelectedItem = "PS4";

            connectButton = new Button { Width = 82, Height = 30, Text = "Connect", Margin = new Padding(0, 0, 8, 0) };
            connectButton.Click += ConnectButton_Click;

            disconnectButton = new Button { Width = 92, Height = 30, Text = "Disconnect", Enabled = false, Margin = new Padding(0, 0, 16, 0) };
            disconnectButton.Click += DisconnectButton_Click;

            profileComboBox = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDown, Margin = new Padding(0, 0, 8, 0) };
            saveProfileButton = new Button { Width = 56, Height = 30, Text = "Save" }; saveProfileButton.Click += SaveProfileButton_Click;
            loadProfileButton = new Button { Width = 56, Height = 30, Text = "Load" }; loadProfileButton.Click += LoadProfileButton_Click;
            deleteProfileButton = new Button { Width = 58, Height = 30, Text = "Delete" }; deleteProfileButton.Click += DeleteProfileButton_Click;

            var headerFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(18, 12, 18, 0), WrapContents = false };
            headerFlow.Controls.AddRange(new Control[] { titleLabel, controllerTypeBox, connectButton, disconnectButton, new Label { AutoSize = true, Text = "Profile", Margin = new Padding(0, 6, 8, 0) }, profileComboBox, saveProfileButton, loadProfileButton, deleteProfileButton });

            valueLabel = new Label { Dock = DockStyle.Top, Height = 24, Padding = new Padding(20, 0, 20, 0), Text = "Base downward drift: 0%" };
            statusLabel = new Label { Dock = DockStyle.Top, Height = 24, Padding = new Padding(20, 0, 20, 0), Text = "Status: Not connected" };
            hintLabel = new Label { Dock = DockStyle.Top, Height = 44, Padding = new Padding(20, 0, 20, 0), Text = "Clean Refactored Architecture separating UI logic from underlying systems." };

            var driftBox = new GroupBox { Dock = DockStyle.Fill, Text = "Base drift", Padding = new Padding(12) };
            var driftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            driftSlider = new TrackBar { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, Value = 0, TickFrequency = 10 };
            driftSlider.Scroll += DriftSlider_Scroll;
            resetButton = new Button { Dock = DockStyle.Fill, Text = "Reset" }; resetButton.Click += ResetButton_Click;

            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            driftLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Centered / neutral", TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            driftLayout.Controls.Add(driftSlider, 0, 1);
            driftLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "More downward drift", TextAlign = ContentAlignment.MiddleCenter }, 0, 2);
            driftLayout.Controls.Add(resetButton, 0, 3);
            AddLabeledCombo(driftLayout, 4, "Jitter trigger", out jitterHoldKeyBox, GetJitterTriggerChoices(), 95);
            AddLabeledNumeric(driftLayout, 5, "Extra drift on hold %", out holdExtraDriftBox, 0, 35, 8, 140);
            AddLabeledCombo(driftLayout, 6, "Pull direction", out pullDirectionBox, new[] { "Left", "Center", "Right" }, 95);
            driftBox.Controls.Add(driftLayout);

            var jitterBox = new GroupBox { Dock = DockStyle.Fill, Text = "Jitter / shake behavior", Padding = new Padding(12) };
            var jitterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); jitterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddLabeledCombo(jitterLayout, 0, "Pattern", out jitterPatternBox, new[] { "Off", "Shake", "Circle", "Custom" }, 110);
            jitterPatternBox.SelectedIndexChanged += (s, e) => { patternPreviewPanel.Invalidate(); };
            AddLabeledNumeric(jitterLayout, 1, "Start delay (ms)", out jitterDelayBox, 0, 5000, 120, 110);
            AddLabeledNumeric(jitterLayout, 2, "Centered radius %", out jitterStrengthBox, 0, 45, 18, 110);
            jitterLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "When active, jitter is processed by the Jitter Engine module." }, 0, 3);
            jitterBox.Controls.Add(jitterLayout);

            var customPatternBox = new GroupBox { Dock = DockStyle.Fill, Text = "Custom pattern editor", Padding = new Padding(12) };
            var customLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
            customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); customLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            patternPreviewPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            patternPreviewPanel.Paint += PatternPreviewPanel_Paint;
            patternPreviewPanel.MouseClick += PatternPreviewPanel_MouseClick;
            customLayout.Controls.Add(patternPreviewPanel, 0, 1);

            var presetFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            useShakePresetButton = new Button { Width = 88, Height = 30, Text = "Shake" }; useShakePresetButton.Click += UseShakePresetButton_Click;
            useCirclePresetButton = new Button { Width = 88, Height = 30, Text = "Circle" }; useCirclePresetButton.Click += UseCirclePresetButton_Click;
            clearPatternButton = new Button { Width = 88, Height = 30, Text = "Clear" }; clearPatternButton.Click += ClearPatternButton_Click;
            presetFlow.Controls.AddRange(new Control[] { useShakePresetButton, useCirclePresetButton, clearPatternButton });
            customLayout.Controls.Add(presetFlow, 1, 1);

            customPatternTextBox = new TextBox { Dock = DockStyle.Fill, Text = "-18:0:10|18:0:10|-10:10:10|10:-10:10" };
            customPatternTextBox.TextChanged += (s, e) => { ParseCustomPatternText(customPatternTextBox.Text); patternPreviewPanel.Invalidate(); };
            customLayout.Controls.Add(customPatternTextBox, 0, 3); customLayout.SetColumnSpan(customPatternTextBox, 2);

            patternComboBox = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            savePatternButton = new Button { Width = 56, Text = "Save" }; savePatternButton.Click += SavePatternButton_Click;
            loadPatternButton = new Button { Width = 56, Text = "Load" }; loadPatternButton.Click += LoadPatternButton_Click;
            deletePatternButton = new Button { Width = 58, Text = "Delete" }; deletePatternButton.Click += DeletePatternButton_Click;
            var patFlow = new FlowLayoutPanel { Dock = DockStyle.Fill }; patFlow.Controls.AddRange(new Control[] { new Label { Text = "Library", AutoSize = true }, patternComboBox, savePatternButton, loadPatternButton, deletePatternButton });
            customLayout.Controls.Add(patFlow, 0, 4); customLayout.SetColumnSpan(patFlow, 2);
            customPatternBox.Controls.Add(customLayout);

            var disengageBox = new GroupBox { Dock = DockStyle.Fill, Text = "Disengage / disable", Padding = new Padding(12) };
            var disengageLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            wheelDisengageToggleButton = new Button { Dock = DockStyle.Fill, Text = "Wheel disengage: ON" }; wheelDisengageToggleButton.Click += WheelDisengageToggleButton_Click;
            scrollDisengageBox = new NumericUpDown { Width = 90, Minimum = 0, Maximum = 5000, Value = 500 };
            disableKeysList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true }; PopulateDisableKeys();
            disengageLayout.Controls.Add(wheelDisengageToggleButton, 0, 0);
            var scrollPanel = new Panel { Dock = DockStyle.Fill }; scrollPanel.Controls.AddRange(new Control[] { new Label { Text = "Disable duration (ms)", Width = 130, Top = 8 }, scrollDisengageBox });
            scrollDisengageBox.Left = 140; scrollDisengageBox.Top = 4;
            disengageLayout.Controls.Add(scrollPanel, 0, 1);
            disengageLayout.Controls.Add(disableKeysList, 0, 2);
            disengageBox.Controls.Add(disengageLayout);

            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 3, RowCount = 2 };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250)); contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340)); contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 54)); contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

            contentLayout.Controls.Add(driftBox, 0, 0); contentLayout.SetRowSpan(driftBox, 2);
            contentLayout.Controls.Add(jitterBox, 1, 0);
            contentLayout.Controls.Add(customPatternBox, 2, 0);
            contentLayout.Controls.Add(disengageBox, 1, 1);
            contentLayout.Controls.Add(BuildBindingBox(), 2, 1);

            updateTimer = new System.Windows.Forms.Timer { Interval = 10 };
            updateTimer.Tick += UpdateTimer_Tick;

            Controls.AddRange(new Control[] { contentLayout, hintLabel, statusLabel, valueLabel, headerFlow });
            FormClosing += MainForm_FormClosing;
            ResumeLayout();
        }

        private GroupBox BuildBindingBox()
        {
            var bindingBox = new GroupBox { Dock = DockStyle.Fill, Text = "PC key -> controller" };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 + ((BindingActions.Length + 1) / 2) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(new Label { Text = "Bind keyboard keys to virtual controller buttons.", Dock = DockStyle.Fill }, 0, 0);
            layout.SetColumnSpan(layout.Controls[0], 2);

            int row = 1;
            for (int index = 0; index < BindingActions.Length; index += 2)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                layout.Controls.Add(CreateBindingSelector(BindingActions[index]), 0, row);
                if (index + 1 < BindingActions.Length) layout.Controls.Add(CreateBindingSelector(BindingActions[index + 1]), 1, row);
                row++;
            }
            bindingBox.Controls.Add(layout);
            return bindingBox;
        }

        private Control CreateBindingSelector(string actionName)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var label = new Label { Left = 0, Top = 8, Width = 90, Text = actionName };
            var comboBox = new ComboBox { Left = 94, Top = 4, Width = 72, DropDownStyle = ComboBoxStyle.DropDownList };
            var turboToggle = new CheckBox { Left = 171, Top = 7, Width = 56, Text = "Turbo" };
            var turboHzBox = new NumericUpDown { Left = 230, Top = 4, Width = 52, Minimum = 1, Maximum = 30, Value = 12 };

            foreach (string choice in GetKeyChoices()) comboBox.Items.Add(choice);
            comboBox.SelectedItem = "None";
            panel.Controls.AddRange(new Control[] { label, comboBox, turboToggle, turboHzBox, new Label { Left = 286, Top = 8, Text = "Hz" } });

            bindingControls[actionName] = new BindingControlSet { KeySelector = comboBox, TurboToggle = turboToggle, TurboHzBox = turboHzBox };
            return panel;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                controllerManager.Connect(controllerTypeBox.SelectedItem?.ToString());
                isConnected = true;
                connectButton.Enabled = false;
                disconnectButton.Enabled = true;
                controllerTypeBox.Enabled = false;
                updateTimer.Start();
                statusLabel.Text = $"Status: {controllerManager.GetConnectedControllerName()} connected";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect target: {ex.Message}");
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            CleanupController();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (isTemporarilyDisengaged && disengagedUntilUtc.HasValue && DateTime.UtcNow >= disengagedUntilUtc.Value)
            {
                isTemporarilyDisengaged = false;
                disengagedUntilUtc = null;
            }

            if (!controllerManager.IsConnected) return;

            if (isTemporarilyDisengaged)
            {
                controllerManager.SendNeutralStick();
                statusLabel.Text = "Status: Temporarily disabled";
                updateTimer.Interval = 10;
                return;
            }

            // Read inputs & calculate standard base pull
            int baseDriftY = driftSlider.Value;
            int baseDriftX = GetBasePullDirectionX(baseDriftY);

            // Forward state to JitterEngine to let it compute offsets and timings
            string selectedPattern = jitterPatternBox.SelectedItem?.ToString() ?? "Off";
            jitterEngine.CalculateOutput(
                baseDriftX, baseDriftY,
                selectedPattern,
                (int)jitterDelayBox.Value,
                (int)jitterStrengthBox.Value,
                (int)holdExtraDriftBox.Value,
                customPatternPoints,
                out int finalX, out int finalY,
                out int nextIntervalMs, out bool waitingForDelay
            );

            updateTimer.Interval = nextIntervalMs;

            // Apply Stick Positions
            controllerManager.SetStick(finalX, finalY);

            // Pass button bindings through InputBindingManager wrapper mapping lambda
            controllerManager.ApplyBindings(actionName =>
            {
                if (!bindingControls.TryGetValue(actionName, out var controls)) return false;
                return inputBindingManager.IsActionPressed(
                    actionName,
                    controls.KeySelector.SelectedItem?.ToString(),
                    controls.TurboToggle.Checked,
                    (int)controls.TurboHzBox.Value
                );
            });

            // Submit virtual report details
            controllerManager.SubmitReport();

            // Display Status updates
            string controllerName = controllerManager.GetConnectedControllerName();
            string bindKey = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
            if (jitterEngine.IsJitterActive)
            {
                statusLabel.Text = $"Status: {controllerName} connected - centered shake active ({finalX}%, {finalY}%)";
            }
            else if (waitingForDelay)
            {
                statusLabel.Text = $"Status: {controllerName} connected - waiting for delay (extra drift on)";
            }
            else
            {
                statusLabel.Text = $"Status: {controllerName} connected ({finalX}%, {finalY}%)";
            }
        }

        private void InitializeInputHook()
        {
            inputHook = new GlobalInputHook();
            inputHook.WheelScrolled += (s, e) => { if (wheelDisengageEnabled) EngageTemporaryDisengage("mouse wheel"); };
            inputHook.LeftButtonStateChanged += (s, down) => CheckJitterBindTrigger("Mouse Left", down);
            inputHook.RightButtonStateChanged += (s, down) => CheckJitterBindTrigger("Mouse Right", down);
            inputHook.KeyStateChanged += (s, e) =>
            {
                string keyName = NormalizeKeyName(e.Key);
                inputBindingManager.SetKeyState(keyName, e.IsDown);

                if (e.IsDown && IsDisableKeySelected(e.Key)) EngageTemporaryDisengage($"key {keyName}");
                CheckJitterBindTrigger(keyName, e.IsDown);
            };
            inputHook.Start();
        }

        private void CheckJitterBindTrigger(string triggeredInput, bool isDown)
        {
            string configuredJitterKeyName = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
            if (string.Equals(configuredJitterKeyName, triggeredInput, StringComparison.OrdinalIgnoreCase))
            {
                jitterEngine.SetTriggerState(isDown);
            }
        }

        private void EngageTemporaryDisengage(string source)
        {
            int disengageMs = (int)scrollDisengageBox.Value;
            if (disengageMs <= 0) return;

            isTemporarilyDisengaged = true;
            disengagedUntilUtc = DateTime.UtcNow.AddMilliseconds(disengageMs);
            controllerManager.SendNeutralStick();
        }

        private int GetBasePullDirectionX(int basePercentY)
        {
            string direction = pullDirectionBox.SelectedItem?.ToString() ?? "Center";
            if (string.Equals(direction, "Left", StringComparison.OrdinalIgnoreCase)) return Math.Clamp(50 - (basePercentY / 2), 0, 100);
            if (string.Equals(direction, "Right", StringComparison.OrdinalIgnoreCase)) return Math.Clamp(50 + (basePercentY / 2), 0, 100);
            return 50;
        }

        private void CleanupController()
        {
            updateTimer.Stop();
            controllerManager.Disconnect();
            jitterEngine.Reset();
            inputBindingManager.Clear();

            isConnected = false;
            isTemporarilyDisengaged = false;
            disengagedUntilUtc = null;

            controllerTypeBox.Enabled = true;
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
            statusLabel.Text = "Status: Not connected";
        }

        private void DriftSlider_Scroll(object sender, EventArgs e) => valueLabel.Text = $"Base downward drift: {driftSlider.Value}%";
        private void ResetButton_Click(object sender, EventArgs e) { driftSlider.Value = 0; valueLabel.Text = "Base downward drift: 0%"; }
        private void WheelDisengageToggleButton_Click(object sender, EventArgs e) { wheelDisengageEnabled = !wheelDisengageEnabled; wheelDisengageToggleButton.Text = wheelDisengageEnabled ? "Wheel disengage: ON" : "Wheel disengage: OFF"; }

        private void PatternPreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            graphics.DrawLine(Pens.LightGray, 0, rect.Height / 2, rect.Width, rect.Height / 2);
            graphics.DrawLine(Pens.LightGray, rect.Width / 2, 0, rect.Width / 2, rect.Height);

            if (customPatternPoints.Count == 0) return;
            Point[] screenPoints = customPatternPoints.Select(ConvertPatternPointToScreen).ToArray();
            if (screenPoints.Length > 1) graphics.DrawLines(Pens.Blue, screenPoints);
            foreach (var p in screenPoints) graphics.FillEllipse(Brushes.Red, p.X - 4, p.Y - 4, 8, 8);
        }

        private void PatternPreviewPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                customPatternPoints.Add(ConvertScreenPointToPattern(e.Location));
                SyncCustomPatternTextFromPoints();
            }
            else if (e.Button == MouseButtons.Right && customPatternPoints.Count > 0)
            {
                int idx = FindNearestPointIndex(e.Location);
                if (idx >= 0) { customPatternPoints.RemoveAt(idx); SyncCustomPatternTextFromPoints(); }
            }
        }

        private Point ConvertPatternPointToScreen(PatternStep step)
        {
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            int x = (int)Math.Round(((step.X + 100) / 200.0) * rect.Width);
            int y = (int)Math.Round(((100 - step.Y) / 200.0) * rect.Height);
            return new Point(Math.Clamp(x, 0, rect.Width), Math.Clamp(y, 0, rect.Height));
        }

        private PatternStep ConvertScreenPointToPattern(Point pt)
        {
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            int x = (int)Math.Round((pt.X / (double)rect.Width) * 200.0 - 100.0);
            int y = (int)Math.Round(100.0 - (pt.Y / (double)rect.Height) * 200.0);
            return new PatternStep { X = Math.Clamp(x, -100, 100), Y = Math.Clamp(y, -100, 100), Delay = 10 };
        }

        private int FindNearestPointIndex(Point loc)
        {
            int idx = -1; double minDist = double.MaxValue;
            for (int i = 0; i < customPatternPoints.Count; i++)
            {
                Point sPt = ConvertPatternPointToScreen(customPatternPoints[i]);
                double d = Math.Pow(sPt.X - loc.X, 2) + Math.Pow(sPt.Y - loc.Y, 2);
                if (d < minDist && d < 144) { minDist = d; idx = i; }
            }
            return idx;
        }

        private void ParseCustomPatternText(string text)
        {
            customPatternPoints.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (string segment in text.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = segment.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    int delay = parts.Length >= 3 && int.TryParse(parts[2], out int d) ? d : 10;
                    customPatternPoints.Add(new PatternStep { X = x, Y = y, Delay = delay });
                }
            }
        }

        private void SyncCustomPatternTextFromPoints()
        {
            customPatternTextBox.Text = string.Join("|", customPatternPoints.Select(p => $"{p.X}:{p.Y}:{p.Delay}"));
            patternPreviewPanel.Invalidate();
        }

        private void UseShakePresetButton_Click(object sender, EventArgs e) { customPatternTextBox.Text = "-18:0:10|18:0:10|-10:10:10|10:-10:10"; }
        private void UseCirclePresetButton_Click(object sender, EventArgs e) { customPatternPoints.Clear(); var list = jitterEngine.GetPatternSteps("Circle", 18, null); customPatternPoints.AddRange(list); SyncCustomPatternTextFromPoints(); }
        private void ClearPatternButton_Click(object sender, EventArgs e) { customPatternPoints.Clear(); SyncCustomPatternTextFromPoints(); }

        private void PopulateDisableKeys() { foreach (var k in new[] { "Tab", "E", "F", "Q", "R", "Shift", "Ctrl" }) disableKeysList.Items.Add(k, k == "Tab" || k == "E"); }
        private bool IsDisableKeySelected(Keys key) => disableKeysList.CheckedItems.Cast<object>().Any(i => string.Equals(i.ToString(), NormalizeKeyName(key), StringComparison.OrdinalIgnoreCase));
        private void EnsureProfilesDirectory() => Directory.CreateDirectory(profilesDirectory);
        private void EnsurePatternsDirectory() => Directory.CreateDirectory(patternsDirectory);
        private static string[] GetJitterTriggerChoices() => new[] { "None", "Mouse Left", "Mouse Right", "Shift", "Ctrl", "Space", "Tab" };
        private static string[] GetKeyChoices() => new[] { "None", "Shift", "Ctrl", "Space", "Tab", "Enter", "A", "B", "C", "X", "Y", "Z" };

        private void LoadProfileList() { profileComboBox.Items.Clear(); if (Directory.Exists(profilesDirectory)) profileComboBox.Items.AddRange(Directory.GetFiles(profilesDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).ToArray()); }
        private void LoadPatternList() { patternComboBox.Items.Clear(); if (Directory.Exists(patternsDirectory)) patternComboBox.Items.AddRange(Directory.GetFiles(patternsDirectory, "*.txt").Select(Path.GetFileNameWithoutExtension).ToArray()); }

        private void SaveProfileButton_Click(object sender, EventArgs e)
        {
            string name = profileComboBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var prof = new InputProfile
            {
                Name = name, ControllerType = controllerTypeBox.SelectedItem?.ToString(), BaseDriftPercent = driftSlider.Value,
                BasePullDirection = pullDirectionBox.SelectedItem?.ToString(), HoldExtraDriftPercent = (int)holdExtraDriftBox.Value,
                JitterDelayMs = (int)jitterDelayBox.Value, JitterStrengthPercent = (int)jitterStrengthBox.Value,
                ScrollDisengageMs = (int)scrollDisengageBox.Value, WheelDisengageEnabled = wheelDisengageEnabled,
                JitterHoldKey = jitterHoldKeyBox.SelectedItem?.ToString(), JitterPatternType = jitterPatternBox.SelectedItem?.ToString(),
                CustomPatternPoints = customPatternTextBox.Text, DisableKeys = disableKeysList.CheckedItems.Cast<object>().Select(i => i.ToString()).ToList(),
                KeyBindings = bindingControls.ToDictionary(p => p.Key, p => p.Value.KeySelector.SelectedItem?.ToString()),
                TurboEnabled = bindingControls.ToDictionary(p => p.Key, p => p.Value.TurboToggle.Checked),
                TurboHz = bindingControls.ToDictionary(p => p.Key, p => (int)p.Value.TurboHzBox.Value)
            };
            File.WriteAllText(Path.Combine(profilesDirectory, name + ".json"), JsonSerializer.Serialize(prof));
            LoadProfileList();
        }

        private void LoadProfileButton_Click(object sender, EventArgs e)
        {
            string name = profileComboBox.Text.Trim();
            string path = Path.Combine(profilesDirectory, name + ".json");
            if (!File.Exists(path)) return;
            var prof = JsonSerializer.Deserialize<InputProfile>(File.ReadAllText(path));
            if (prof == null) return;

            controllerTypeBox.SelectedItem = prof.ControllerType;
            driftSlider.Value = prof.BaseDriftPercent;
            pullDirectionBox.SelectedItem = prof.BasePullDirection;
            holdExtraDriftBox.Value = prof.HoldExtraDriftPercent;
            jitterDelayBox.Value = prof.JitterDelayMs;
            jitterStrengthBox.Value = prof.JitterStrengthPercent;
            scrollDisengageBox.Value = prof.ScrollDisengageMs;
            wheelDisengageEnabled = prof.WheelDisengageEnabled;
            jitterHoldKeyBox.SelectedItem = prof.JitterHoldKey;
            jitterPatternBox.SelectedItem = prof.JitterPatternType;
            customPatternTextBox.Text = prof.CustomPatternPoints;

            for (int i = 0; i < disableKeysList.Items.Count; i++)
                disableKeysList.SetItemChecked(i, prof.DisableKeys.Contains(disableKeysList.Items[i].ToString()));

            foreach (var kvp in prof.KeyBindings)
                if (bindingControls.TryGetValue(kvp.Key, out var set)) { set.KeySelector.SelectedItem = kvp.Value; set.TurboToggle.Checked = prof.TurboEnabled[kvp.Key]; set.TurboHzBox.Value = prof.TurboHz[kvp.Key]; }
        }

        private void DeleteProfileButton_Click(object sender, EventArgs e) { string p = Path.Combine(profilesDirectory, profileComboBox.Text + ".json"); if (File.Exists(p)) File.Delete(p); LoadProfileList(); }
        private void SavePatternButton_Click(object sender, EventArgs e) { if (customPatternPoints.Count > 0) File.WriteAllLines(Path.Combine(patternsDirectory, patternComboBox.Text + ".txt"), customPatternPoints.Select(pt => $"{pt.X},{pt.Y},{pt.Delay}")); LoadPatternList(); }
        private void LoadPatternButton_Click(object sender, EventArgs e)
        {
            string p = Path.Combine(patternsDirectory, patternComboBox.Text + ".txt");
            if (!File.Exists(p)) return;
            customPatternPoints.Clear();
            foreach (var l in File.ReadAllLines(p))
            {
                var parts = l.Split(',');
                if (parts.Length >= 2
                    && TryParseCoord(parts[0], out int x)
                    && TryParseCoord(parts[1], out int y))
                {
                    int delay = parts.Length >= 3 && TryParseCoord(parts[2], out int d) ? d : 10;
                    customPatternPoints.Add(new PatternStep { X = x, Y = y, Delay = delay });
                }
            }
            SyncCustomPatternTextFromPoints();
        }

        private static bool TryParseCoord(string s, out int result)
        {
            if (double.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            {
                result = (int)Math.Round(d);
                return true;
            }
            result = 0;
            return false;
        }
        private void DeletePatternButton_Click(object sender, EventArgs e) { string p = Path.Combine(patternsDirectory, patternComboBox.Text + ".txt"); if (File.Exists(p)) File.Delete(p); LoadPatternList(); }

        private void ApplyDefaults() { controllerTypeBox.SelectedItem = "PS4"; jitterPatternBox.SelectedItem = "Shake"; pullDirectionBox.SelectedItem = "Center"; }
        private void AddLabeledNumeric(TableLayoutPanel parent, int r, string t, out NumericUpDown n, int min, int max, int val, int w) { var p = new Panel { Dock = DockStyle.Fill }; n = new NumericUpDown { Left = w + 8, Top = 4, Width = 90, Minimum = min, Maximum = max, Value = val }; p.Controls.AddRange(new Control[] { new Label { Text = t, Width = w, Top = 8 }, n }); parent.Controls.Add(p, 0, r); }
        private void AddLabeledCombo(TableLayoutPanel parent, int r, string t, out ComboBox c, IEnumerable<string> items, int w) { var p = new Panel { Dock = DockStyle.Fill }; c = new ComboBox { Left = w + 8, Top = 4, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList }; foreach (var i in items) c.Items.Add(i); c.SelectedIndex = 0; p.Controls.AddRange(new Control[] { new Label { Text = t, Width = w, Top = 8 }, c }); parent.Controls.Add(p, 0, r); }

        private static string NormalizeKeyName(Keys key) => key switch { Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey => "Shift", Keys.LControlKey or Keys.RControlKey or Keys.ControlKey => "Ctrl", Keys.LMenu or Keys.RMenu or Keys.Menu => "Alt", Keys.Space => "Space", Keys.Return => "Enter", Keys.Escape => "Escape", _ => key.ToString() };
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) { try { inputHook?.Dispose(); } catch { } CleanupController(); }
    }
}
