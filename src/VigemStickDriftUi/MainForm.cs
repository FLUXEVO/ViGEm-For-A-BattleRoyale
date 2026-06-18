using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

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
        private const int DefaultPatternStepDelayMs = 10;

        private static readonly string[] BindingActions =
        {
            ActionACross,
            ActionBCircle,
            ActionXSquare,
            ActionYTriangle,
            ActionLbL1,
            ActionRbR1,
            ActionLtL2,
            ActionRtR2,
            ActionBackShare,
            ActionStartOptions,
            ActionLsL3,
            ActionRsR3,
            ActionDPadUp,
            ActionDPadDown,
            ActionDPadLeft,
            ActionDPadRight
        };

        private readonly string profilesDirectory = Path.Combine(AppContext.BaseDirectory, "profiles");
        private readonly string patternsDirectory = Path.Combine(AppContext.BaseDirectory, "Patterns");
        private readonly Dictionary<string, BindingControlSet> bindingControls = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> activeKeyNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> turboHoldStartedUtc = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PatternStep> customPatternPoints = new();

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

        private ViGEmClient client;
        private IXbox360Controller xboxController;
        private IDualShock4Controller ds4Controller;
        private GlobalInputHook inputHook;

        private bool isConnected;
        private bool useXbox;
        private bool isTemporarilyDisengaged;
        private bool wheelDisengageEnabled = true;
        private bool isJitterBindDown;
        private int patternStepIndex;
        private DateTime? jitterHeldSinceUtc;
        private DateTime? disengagedUntilUtc;

        private sealed class PatternStep
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Delay { get; set; } // Added per-step delay support
        }

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
            Text = "ViGEm Stick Drift UI V8";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 860);
            MinimumSize = new Size(1120, 760);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            SuspendLayout();

            titleLabel = new Label
            {
                AutoSize = true,
                Text = "Virtual Right Stick Drift V8",
                Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
                Margin = new Padding(0, 0, 20, 0)
            };

            controllerTypeBox = new ComboBox
            {
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 10, 0)
            };
            controllerTypeBox.Items.AddRange(new object[] { "Xbox 360", "PS4" });
            controllerTypeBox.SelectedItem = "PS4";

            connectButton = new Button
            {
                Width = 82,
                Height = 30,
                Text = "Connect",
                Margin = new Padding(0, 0, 8, 0)
            };
            connectButton.Click += ConnectButton_Click;

            disconnectButton = new Button
            {
                Width = 92,
                Height = 30,
                Text = "Disconnect",
                Enabled = false,
                Margin = new Padding(0, 0, 16, 0)
            };
            disconnectButton.Click += DisconnectButton_Click;

            var profileLabel = new Label
            {
                AutoSize = true,
                Text = "Profile",
                Margin = new Padding(0, 6, 8, 0)
            };

            profileComboBox = new ComboBox
            {
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 0, 8, 0)
            };

            saveProfileButton = new Button
            {
                Width = 56,
                Height = 30,
                Text = "Save",
                Margin = new Padding(0, 0, 6, 0)
            };
            saveProfileButton.Click += SaveProfileButton_Click;

            loadProfileButton = new Button
            {
                Width = 56,
                Height = 30,
                Text = "Load",
                Margin = new Padding(0, 0, 6, 0)
            };
            loadProfileButton.Click += LoadProfileButton_Click;

            deleteProfileButton = new Button
            {
                Width = 58,
                Height = 30,
                Text = "Delete"
            };
            deleteProfileButton.Click += DeleteProfileButton_Click;

            var headerFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(18, 12, 18, 0),
                WrapContents = false,
                AutoScroll = true
            };
            headerFlow.Controls.Add(titleLabel);
            headerFlow.Controls.Add(controllerTypeBox);
            headerFlow.Controls.Add(connectButton);
            headerFlow.Controls.Add(disconnectButton);
            headerFlow.Controls.Add(profileLabel);
            headerFlow.Controls.Add(profileComboBox);
            headerFlow.Controls.Add(saveProfileButton);
            headerFlow.Controls.Add(loadProfileButton);
            headerFlow.Controls.Add(deleteProfileButton);

            valueLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(20, 0, 20, 0),
                Text = "Base downward drift: 0%"
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(20, 0, 20, 0),
                Text = "Status: Not connected"
            };

            hintLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(20, 0, 20, 0),
                AutoSize = false,
                Text = "V8 keeps jitter centered around the stick instead of drifting off-axis, uses a softer default jitter radius, supports PC-key-to-controller bindings, and still lets wheel or checked keys temporarily neutralize the stick."
            };

            var driftBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Base drift",
                Padding = new Padding(12)
            };

            var driftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7
            };
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            driftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var topLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Centered / neutral",
                TextAlign = ContentAlignment.MiddleCenter
            };

            driftSlider = new TrackBar
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TickFrequency = 10,
                LargeChange = 10,
                SmallChange = 1
            };
            driftSlider.Scroll += DriftSlider_Scroll;

            var bottomLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "More downward drift",
                TextAlign = ContentAlignment.MiddleCenter
            };

            resetButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Reset"
            };
            resetButton.Click += ResetButton_Click;

            driftLayout.Controls.Add(topLabel, 0, 0);
            driftLayout.Controls.Add(driftSlider, 0, 1);
            driftLayout.Controls.Add(bottomLabel, 0, 2);
            driftLayout.Controls.Add(resetButton, 0, 3);
            AddLabeledCombo(driftLayout, 4, "Jitter trigger", out jitterHoldKeyBox, GetJitterTriggerChoices(), 95);
            AddLabeledNumeric(driftLayout, 5, "Extra drift on hold %", out holdExtraDriftBox, 0, 35, 8, 140);
            AddLabeledCombo(driftLayout, 6, "Pull direction", out pullDirectionBox, new[] { "Left", "Center", "Right" }, 95);
            driftBox.Controls.Add(driftLayout);

            var jitterBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Jitter / shake behavior",
                Padding = new Padding(12)
            };

            var jitterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            jitterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            jitterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddLabeledCombo(jitterLayout, 0, "Pattern", out jitterPatternBox, new[] { "Off", "Shake", "Circle", "Custom" }, 110);
            jitterPatternBox.SelectedIndexChanged += JitterPatternBox_SelectedIndexChanged;
            AddLabeledNumeric(jitterLayout, 1, "Start delay (ms)", out jitterDelayBox, 0, 5000, 120, 110);
            AddLabeledNumeric(jitterLayout, 2, "Centered radius %", out jitterStrengthBox, 0, 45, 18, 110);

            var jitterHelpLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "When jitter is active it stays centered and does not stack extra drift. If the jitter pattern is off, the hold trigger still works as extra drift only."
            };
            jitterLayout.Controls.Add(jitterHelpLabel, 0, 3);
            jitterBox.Controls.Add(jitterLayout);

            var customPatternBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Custom pattern editor",
                Padding = new Padding(12)
            };

            var customLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5
            };
            customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            customLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var customPatternHelpLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "Left click inside the graph to add points. Right click near a point to remove it. The app loops through the points in order and recenters them so the jitter stays around the middle. Clicked points get a default " + DefaultPatternStepDelayMs + "ms delay; edit the text box to fine-tune timing per step."
            };
            customLayout.Controls.Add(customPatternHelpLabel, 0, 0);
            customLayout.SetColumnSpan(customPatternHelpLabel, 2);

            patternPreviewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(0, 4, 8, 4)
            };
            patternPreviewPanel.Paint += PatternPreviewPanel_Paint;
            patternPreviewPanel.MouseClick += PatternPreviewPanel_MouseClick;
            customLayout.Controls.Add(patternPreviewPanel, 0, 1);

            var presetFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 4)
            };

            useShakePresetButton = new Button { Width = 88, Height = 30, Text = "Shake" };
            useShakePresetButton.Click += UseShakePresetButton_Click;
            useCirclePresetButton = new Button { Width = 88, Height = 30, Text = "Circle" };
            useCirclePresetButton.Click += UseCirclePresetButton_Click;
            clearPatternButton = new Button { Width = 88, Height = 30, Text = "Clear" };
            clearPatternButton.Click += ClearPatternButton_Click;
            presetFlow.Controls.Add(useShakePresetButton);
            presetFlow.Controls.Add(useCirclePresetButton);
            presetFlow.Controls.Add(clearPatternButton);
            customLayout.Controls.Add(presetFlow, 1, 1);

            var customPatternLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Pattern text (x:y:delayMs|x:y:delayMs|...)",
                TextAlign = ContentAlignment.MiddleLeft
            };
            customLayout.Controls.Add(customPatternLabel, 0, 2);
            customLayout.SetColumnSpan(customPatternLabel, 2);

            customPatternTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = "-18:0:10|18:0:10|-10:10:10|10:-10:10"
            };
            customPatternTextBox.TextChanged += CustomPatternTextBox_TextChanged;
            customLayout.Controls.Add(customPatternTextBox, 0, 3);
            customLayout.SetColumnSpan(customPatternTextBox, 2);

            var patternLibraryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 0)
            };

            var patternLibraryLabel = new Label
            {
                AutoSize = true,
                Text = "Library",
                Margin = new Padding(0, 6, 8, 0)
            };

            patternComboBox = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 0, 8, 0)
            };

            savePatternButton = new Button
            {
                Width = 56,
                Height = 30,
                Text = "Save",
                Margin = new Padding(0, 0, 6, 0)
            };
            savePatternButton.Click += SavePatternButton_Click;

            loadPatternButton = new Button
            {
                Width = 56,
                Height = 30,
                Text = "Load",
                Margin = new Padding(0, 0, 6, 0)
            };
            loadPatternButton.Click += LoadPatternButton_Click;

            deletePatternButton = new Button
            {
                Width = 58,
                Height = 30,
                Text = "Delete"
            };
            deletePatternButton.Click += DeletePatternButton_Click;

            patternLibraryFlow.Controls.Add(patternLibraryLabel);
            patternLibraryFlow.Controls.Add(patternComboBox);
            patternLibraryFlow.Controls.Add(savePatternButton);
            patternLibraryFlow.Controls.Add(loadPatternButton);
            patternLibraryFlow.Controls.Add(deletePatternButton);
            customLayout.Controls.Add(patternLibraryFlow, 0, 4);
            customLayout.SetColumnSpan(patternLibraryFlow, 2);

            customPatternBox.Controls.Add(customLayout);

            var disengageBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Disengage / disable",
                Padding = new Padding(12)
            };

            var disengageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            disengageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            disengageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            disengageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            disengageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            wheelDisengageToggleButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Wheel disengage: ON"
            };
            wheelDisengageToggleButton.Click += WheelDisengageToggleButton_Click;
            disengageLayout.Controls.Add(wheelDisengageToggleButton, 0, 0);

            var delayRow = new Panel { Dock = DockStyle.Fill };
            var disableDurationLabel = new Label { Left = 0, Top = 8, Width = 130, Text = "Disable duration (ms)" };
            scrollDisengageBox = new NumericUpDown
            {
                Left = 140,
                Top = 4,
                Width = 90,
                Minimum = 0,
                Maximum = 5000,
                Value = 500
            };
            delayRow.Controls.Add(disableDurationLabel);
            delayRow.Controls.Add(scrollDisengageBox);
            disengageLayout.Controls.Add(delayRow, 0, 1);

            disableKeysList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            PopulateDisableKeys();
            disengageLayout.Controls.Add(disableKeysList, 0, 2);

            var disableHelpLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Press any checked key to temporarily neutralize the stick while looting.",
                TextAlign = ContentAlignment.MiddleLeft
            };
            disengageLayout.Controls.Add(disableHelpLabel, 0, 3);
            disengageBox.Controls.Add(disengageLayout);

            GroupBox bindingBox = BuildBindingBox();

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 10, 18, 18),
                ColumnCount = 3,
                RowCount = 2
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

            contentLayout.Controls.Add(driftBox, 0, 0);
            contentLayout.SetRowSpan(driftBox, 2);
            contentLayout.Controls.Add(jitterBox, 1, 0);
            contentLayout.Controls.Add(customPatternBox, 2, 0);
            contentLayout.Controls.Add(disengageBox, 1, 1);
            contentLayout.Controls.Add(bindingBox, 2, 1);

            updateTimer = new System.Windows.Forms.Timer { Interval = 10 };
            updateTimer.Tick += UpdateTimer_Tick;

            Controls.Add(contentLayout);
            Controls.Add(hintLabel);
            Controls.Add(statusLabel);
            Controls.Add(valueLabel);
            Controls.Add(headerFlow);
            FormClosing += MainForm_FormClosing;

            ResumeLayout();
        }

        private GroupBox BuildBindingBox()
        {
            var bindingBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "PC key -> controller",
                Padding = new Padding(12)
            };

            var bindingLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1 + ((BindingActions.Length + 1) / 2)
            };
            bindingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bindingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bindingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var helpLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = "Bind keyboard keys to virtual controller actions, then optionally enable Turbo (rapid fire) per action with a custom Hz rate. Leave a slot on None if you do not want that action mapped."
            };
            bindingLayout.Controls.Add(helpLabel, 0, 0);
            bindingLayout.SetColumnSpan(helpLabel, 2);

            int row = 1;
            for (int index = 0; index < BindingActions.Length; index += 2)
            {
                bindingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                bindingLayout.Controls.Add(CreateBindingSelector(BindingActions[index]), 0, row);

                if (index + 1 < BindingActions.Length)
                {
                    bindingLayout.Controls.Add(CreateBindingSelector(BindingActions[index + 1]), 1, row);
                }

                row++;
            }

            bindingBox.Controls.Add(bindingLayout);
            return bindingBox;
        }

        private Control CreateBindingSelector(string actionName)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 2)
            };

            var label = new Label
            {
                Left = 0,
                Top = 8,
                Width = 90,
                Text = actionName
            };

            var comboBox = new ComboBox
            {
                Left = 94,
                Top = 4,
                Width = 72,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var turboToggle = new CheckBox
            {
                Left = 171,
                Top = 7,
                Width = 56,
                Text = "Turbo"
            };

            var turboHzBox = new NumericUpDown
            {
                Left = 230,
                Top = 4,
                Width = 52,
                Minimum = 1,
                Maximum = 30,
                Value = 12
            };

            var hzLabel = new Label
            {
                Left = 286,
                Top = 8,
                Width = 24,
                Text = "Hz"
            };

            foreach (string choice in GetKeyChoices())
            {
                comboBox.Items.Add(choice);
            }

            comboBox.SelectedItem = "None";
            panel.Controls.Add(label);
            panel.Controls.Add(comboBox);
            panel.Controls.Add(turboToggle);
            panel.Controls.Add(turboHzBox);
            panel.Controls.Add(hzLabel);

            bindingControls[actionName] = new BindingControlSet
            {
                KeySelector = comboBox,
                TurboToggle = turboToggle,
                TurboHzBox = turboHzBox
            };
            return panel;
        }

        private void AddLabeledNumeric(TableLayoutPanel parent, int row, string labelText, out NumericUpDown numeric, int min, int max, int value, int labelWidth)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var label = new Label
            {
                Left = 0,
                Top = 8,
                Width = labelWidth,
                Text = labelText
            };
            numeric = new NumericUpDown
            {
                Left = labelWidth + 8,
                Top = 4,
                Width = 90,
                Minimum = min,
                Maximum = max,
                Value = value
            };
            panel.Controls.Add(label);
            panel.Controls.Add(numeric);
            parent.Controls.Add(panel, 0, row);
        }

        private void AddLabeledCombo(TableLayoutPanel parent, int row, string labelText, out ComboBox comboBox, IEnumerable<string> items, int labelWidth)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var label = new Label
            {
                Left = 0,
                Top = 8,
                Width = labelWidth,
                Text = labelText
            };
            comboBox = new ComboBox
            {
                Left = labelWidth + 8,
                Top = 4,
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            foreach (string item in items)
            {
                comboBox.Items.Add(item);
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }

            panel.Controls.Add(label);
            panel.Controls.Add(comboBox);
            parent.Controls.Add(panel, 0, row);
        }

        private static string[] GetKeyChoices()
        {
            var keys = new List<string>
            {
                "None",
                "Shift",
                "Ctrl",
                "Alt",
                "Space",
                "Tab",
                "Enter",
                "Escape",
                "Backspace",
                "Up",
                "Down",
                "Left",
                "Right"
            };

            keys.AddRange(Enumerable.Range(0, 10).Select(i => i.ToString(CultureInfo.InvariantCulture)));

            for (char c = 'A'; c <= 'Z'; c++)
            {
                keys.Add(c.ToString());
            }

            for (int i = 1; i <= 12; i++)
            {
                keys.Add("F" + i.ToString(CultureInfo.InvariantCulture));
            }

            return keys.ToArray();
        }

        private static string[] GetJitterTriggerChoices()
        {
            return new[]
            {
                "None",
                "Mouse Left",
                "Mouse Right",
                "Shift",
                "Ctrl",
                "Alt",
                "Space",
                "Tab",
                "Q",
                "E",
                "F",
                "C",
                "V"
            };
        }

        private void PopulateDisableKeys()
        {
            foreach (string keyName in new[]
                     {
                         "Tab", "E", "F", "Q", "R", "Shift", "Ctrl", "Alt", "Space", "C", "V"
                     })
            {
                disableKeysList.Items.Add(keyName, keyName == "Tab" || keyName == "E");
            }
        }

        private void InitializeInputHook()
        {
            inputHook = new GlobalInputHook();
            inputHook.WheelScrolled += InputHook_WheelScrolled;
            inputHook.LeftButtonStateChanged += InputHook_LeftButtonStateChanged;
            inputHook.RightButtonStateChanged += InputHook_RightButtonStateChanged;
            inputHook.KeyStateChanged += InputHook_KeyStateChanged;
            inputHook.Start();
        }

        private void InputHook_WheelScrolled(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => InputHook_WheelScrolled(sender, e)));
                return;
            }

            if (!wheelDisengageEnabled)
            {
                return;
            }

            EngageTemporaryDisengage("mouse wheel");
        }

        private void InputHook_KeyStateChanged(object sender, KeyStateChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => InputHook_KeyStateChanged(sender, e)));
                return;
            }

            string normalizedKeyName = NormalizeKeyName(e.Key);
            if (e.IsDown)
            {
                activeKeyNames.Add(normalizedKeyName);
            }
            else
            {
                activeKeyNames.Remove(normalizedKeyName);
            }

            if (e.IsDown && IsDisableKeySelected(e.Key))
            {
                EngageTemporaryDisengage($"key {normalizedKeyName}");
            }

            string configuredJitterKeyName = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
            if (string.Equals(configuredJitterKeyName, normalizedKeyName, StringComparison.OrdinalIgnoreCase))
            {
                SetJitterBindState(e.IsDown);
            }
        }

        private void InputHook_LeftButtonStateChanged(object sender, bool isDown)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => InputHook_LeftButtonStateChanged(sender, isDown)));
                return;
            }

            string configuredJitterKeyName = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
            if (string.Equals(configuredJitterKeyName, "Mouse Left", StringComparison.OrdinalIgnoreCase))
            {
                SetJitterBindState(isDown);
            }
        }

        private void InputHook_RightButtonStateChanged(object sender, bool isDown)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => InputHook_RightButtonStateChanged(sender, isDown)));
                return;
            }

            string configuredJitterKeyName = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
            if (string.Equals(configuredJitterKeyName, "Mouse Right", StringComparison.OrdinalIgnoreCase))
            {
                SetJitterBindState(isDown);
            }
        }

        private void SetJitterBindState(bool isDown)
        {
            if (isDown)
            {
                if (!isJitterBindDown)
                {
                    isJitterBindDown = true;
                    jitterHeldSinceUtc = DateTime.UtcNow;
                    patternStepIndex = 0;
                }
            }
            else
            {
                isJitterBindDown = false;
                jitterHeldSinceUtc = null;
                patternStepIndex = 0;
            }
        }

        private void EngageTemporaryDisengage(string source)
        {
            int disengageMs = (int)scrollDisengageBox.Value;
            if (disengageMs <= 0)
            {
                isTemporarilyDisengaged = false;
                disengagedUntilUtc = null;
                return;
            }

            isTemporarilyDisengaged = true;
            disengagedUntilUtc = DateTime.UtcNow.AddMilliseconds(disengageMs);
            statusLabel.Text = $"Status: Temporarily disabled by {source}";
            SendNeutralStick();
        }

        private bool IsDisableKeySelected(Keys key)
        {
            string normalizedKeyName = NormalizeKeyName(key);

            foreach (object item in disableKeysList.CheckedItems)
            {
                if (string.Equals(item?.ToString(), normalizedKeyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeKeyName(Keys key)
        {
            return key switch
            {
                Keys.LShiftKey => "Shift",
                Keys.RShiftKey => "Shift",
                Keys.ShiftKey => "Shift",
                Keys.LControlKey => "Ctrl",
                Keys.RControlKey => "Ctrl",
                Keys.ControlKey => "Ctrl",
                Keys.LMenu => "Alt",
                Keys.RMenu => "Alt",
                Keys.Menu => "Alt",
                Keys.Space => "Space",
                Keys.Return => "Enter",
                Keys.Escape => "Escape",
                Keys.Back => "Backspace",
                Keys.Left => "Left",
                Keys.Right => "Right",
                Keys.Up => "Up",
                Keys.Down => "Down",
                Keys.D0 => "0",
                Keys.D1 => "1",
                Keys.D2 => "2",
                Keys.D3 => "3",
                Keys.D4 => "4",
                Keys.D5 => "5",
                Keys.D6 => "6",
                Keys.D7 => "7",
                Keys.D8 => "8",
                Keys.D9 => "9",
                _ => key.ToString()
            };
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                CleanupController();

                client = new ViGEmClient();
                useXbox = controllerTypeBox.SelectedItem?.ToString() == "Xbox 360";

                if (useXbox)
                {
                    xboxController = client.CreateXbox360Controller();
                    xboxController.Connect();
                    statusLabel.Text = "Status: Xbox 360 connected";
                }
                else
                {
                    ds4Controller = client.CreateDualShock4Controller();
                    ds4Controller.Connect();
                    statusLabel.Text = "Status: PS4 connected";
                }

                isConnected = true;
                controllerTypeBox.Enabled = false;
                connectButton.Enabled = false;
                disconnectButton.Enabled = true;

                updateTimer.Start();
                SendCurrentStickValue();
            }
            catch (Exception ex)
            {
                CleanupController();
                MessageBox.Show(
                    "Failed to connect to ViGEm.\n\nMake sure ViGEmBus is installed.\n\n" + ex.Message,
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            CleanupController();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            driftSlider.Value = 0;
            valueLabel.Text = "Base downward drift: 0%";
            SendCurrentStickValue();
        }

        private void DriftSlider_Scroll(object sender, EventArgs e)
        {
            valueLabel.Text = $"Base downward drift: {driftSlider.Value}%";
            SendCurrentStickValue();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (isTemporarilyDisengaged && disengagedUntilUtc.HasValue && DateTime.UtcNow >= disengagedUntilUtc.Value)
            {
                isTemporarilyDisengaged = false;
                disengagedUntilUtc = null;
            }

            SendCurrentStickValue();
        }
        private void SendCurrentStickValue()
        {
            if (!isConnected)
            {
                return;
            }

            if (isTemporarilyDisengaged)
            {
                SendNeutralStick();
                if (disengagedUntilUtc.HasValue)
                {
                    statusLabel.Text = "Status: Temporarily disabled";
                }
                    updateTimer.Interval = 10;
                return;
            }

            int percentX = GetBasePullDirectionX(driftSlider.Value);
            int percentY = driftSlider.Value;
            bool waitingForDelay = false;
            bool jitterActive = false;
            int nextInterval = 10;
            string selectedPattern = jitterPatternBox.SelectedItem?.ToString() ?? "Shake";
            List<PatternStep> configuredPattern = GetPatternSteps(selectedPattern);
            bool canUseJitterPattern = !string.Equals(selectedPattern, "Off", StringComparison.OrdinalIgnoreCase)
                && configuredPattern.Count > 0;

            if (isJitterBindDown)
            {
                bool delayElapsed = true;
                if (jitterHeldSinceUtc.HasValue)
                {
                    double elapsedMs = (DateTime.UtcNow - jitterHeldSinceUtc.Value).TotalMilliseconds;
                    delayElapsed = elapsedMs >= (int)jitterDelayBox.Value;
                    waitingForDelay = !delayElapsed;
                }

                if (delayElapsed && canUseJitterPattern)
                {
                    PatternStep step = GetCurrentPatternStep(configuredPattern);
                    percentX = ClampPercent(percentX + step.X);
                    percentY = ClampPercent(percentY + step.Y);
                    nextInterval = step.Delay;
                    jitterActive = true;
                }
                else
                {
                    percentY = ClampPercent(percentY + (int)holdExtraDriftBox.Value);
                }
            }
            else
            {
                patternStepIndex = 0;
            }
                updateTimer.Interval = nextInterval;
            try
            {
                if (useXbox && xboxController != null)
                {
                    xboxController.SetAxisValue(Xbox360Axis.RightThumbX, PercentToXboxAxis(percentX));
                    xboxController.SetAxisValue(Xbox360Axis.RightThumbY, PercentToXboxAxis(percentY));
                    ApplyXboxBindings();
                    xboxController.SubmitReport();
                }
                else if (!useXbox && ds4Controller != null)
                {
                    ds4Controller.SetAxisValue(DualShock4Axis.RightThumbX, PercentToDs4Axis(percentX));
                    ds4Controller.SetAxisValue(DualShock4Axis.RightThumbY, PercentToDs4Axis(percentY));
                    ApplyDs4Bindings();
                    ds4Controller.SubmitReport();
                }

                string controllerName = GetConnectedControllerName();
                string bindKey = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift";
                if (jitterActive)
                {
                    statusLabel.Text = $"Status: {controllerName} connected - centered jitter active on {bindKey}";
                }
                else if (isJitterBindDown && waitingForDelay)
                {
                    statusLabel.Text = $"Status: {controllerName} connected - waiting for jitter delay on {bindKey}";
                }
                else if (isJitterBindDown)
                {
                    statusLabel.Text = $"Status: {controllerName} connected - extra drift hold active on {bindKey}";
                }
                else
                {
                    statusLabel.Text = $"Status: {controllerName} connected - base drift active";
                }
            }
            catch (Exception ex)
            {
                updateTimer.Stop();
                statusLabel.Text = "Status: Update failed";
                MessageBox.Show(
                    "Failed while updating the virtual controller.\n\n" + ex.Message,
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                CleanupController();
            }
        }

        private void ApplyXboxBindings()
        {
            xboxController.SetButtonState(Xbox360Button.A, IsBindingPressed(ActionACross));
            xboxController.SetButtonState(Xbox360Button.B, IsBindingPressed(ActionBCircle));
            xboxController.SetButtonState(Xbox360Button.X, IsBindingPressed(ActionXSquare));
            xboxController.SetButtonState(Xbox360Button.Y, IsBindingPressed(ActionYTriangle));
            xboxController.SetButtonState(Xbox360Button.LeftShoulder, IsBindingPressed(ActionLbL1));
            xboxController.SetButtonState(Xbox360Button.RightShoulder, IsBindingPressed(ActionRbR1));
            xboxController.SetButtonState(Xbox360Button.Back, IsBindingPressed(ActionBackShare));
            xboxController.SetButtonState(Xbox360Button.Start, IsBindingPressed(ActionStartOptions));
            xboxController.SetButtonState(Xbox360Button.LeftThumb, IsBindingPressed(ActionLsL3));
            xboxController.SetButtonState(Xbox360Button.RightThumb, IsBindingPressed(ActionRsR3));
            xboxController.SetButtonState(Xbox360Button.Up, IsBindingPressed(ActionDPadUp));
            xboxController.SetButtonState(Xbox360Button.Down, IsBindingPressed(ActionDPadDown));
            xboxController.SetButtonState(Xbox360Button.Left, IsBindingPressed(ActionDPadLeft));
            xboxController.SetButtonState(Xbox360Button.Right, IsBindingPressed(ActionDPadRight));
            xboxController.SetSliderValue(Xbox360Slider.LeftTrigger, IsBindingPressed(ActionLtL2) ? byte.MaxValue : (byte)0);
            xboxController.SetSliderValue(Xbox360Slider.RightTrigger, IsBindingPressed(ActionRtR2) ? byte.MaxValue : (byte)0);
        }

        private void ApplyDs4Bindings()
        {
            ds4Controller.SetButtonState(DualShock4Button.Cross, IsBindingPressed(ActionACross));
            ds4Controller.SetButtonState(DualShock4Button.Circle, IsBindingPressed(ActionBCircle));
            ds4Controller.SetButtonState(DualShock4Button.Square, IsBindingPressed(ActionXSquare));
            ds4Controller.SetButtonState(DualShock4Button.Triangle, IsBindingPressed(ActionYTriangle));
            ds4Controller.SetButtonState(DualShock4Button.ShoulderLeft, IsBindingPressed(ActionLbL1));
            ds4Controller.SetButtonState(DualShock4Button.ShoulderRight, IsBindingPressed(ActionRbR1));
            ds4Controller.SetButtonState(DualShock4Button.TriggerLeft, IsBindingPressed(ActionLtL2));
            ds4Controller.SetButtonState(DualShock4Button.TriggerRight, IsBindingPressed(ActionRtR2));
            ds4Controller.SetButtonState(DualShock4Button.Share, IsBindingPressed(ActionBackShare));
            ds4Controller.SetButtonState(DualShock4Button.Options, IsBindingPressed(ActionStartOptions));
            ds4Controller.SetButtonState(DualShock4Button.ThumbLeft, IsBindingPressed(ActionLsL3));
            ds4Controller.SetButtonState(DualShock4Button.ThumbRight, IsBindingPressed(ActionRsR3));
            ds4Controller.SetSliderValue(DualShock4Slider.LeftTrigger, IsBindingPressed(ActionLtL2) ? byte.MaxValue : (byte)0);
            ds4Controller.SetSliderValue(DualShock4Slider.RightTrigger, IsBindingPressed(ActionRtR2) ? byte.MaxValue : (byte)0);
            ds4Controller.SetDPadDirection(GetDs4DPadDirection());
        }

        private DualShock4DPadDirection GetDs4DPadDirection()
        {
            bool up = IsBindingPressed(ActionDPadUp);
            bool down = IsBindingPressed(ActionDPadDown);
            bool left = IsBindingPressed(ActionDPadLeft);
            bool right = IsBindingPressed(ActionDPadRight);

            if (up && right && !down && !left)
            {
                return DualShock4DPadDirection.Northeast;
            }

            if (up && left && !down && !right)
            {
                return DualShock4DPadDirection.Northwest;
            }

            if (down && right && !up && !left)
            {
                return DualShock4DPadDirection.Southeast;
            }

            if (down && left && !up && !right)
            {
                return DualShock4DPadDirection.Southwest;
            }

            if (up && !down)
            {
                return DualShock4DPadDirection.North;
            }

            if (down && !up)
            {
                return DualShock4DPadDirection.South;
            }

            if (left && !right)
            {
                return DualShock4DPadDirection.West;
            }

            if (right && !left)
            {
                return DualShock4DPadDirection.East;
            }

            return DualShock4DPadDirection.None;
        }

        private bool IsBindingPressed(string actionName)
        {
            if (!bindingControls.TryGetValue(actionName, out BindingControlSet controls))
            {
                return false;
            }

            string configuredKey = controls.KeySelector.SelectedItem?.ToString() ?? "None";
            if (string.Equals(configuredKey, "None", StringComparison.OrdinalIgnoreCase))
            {
                turboHoldStartedUtc.Remove(actionName);
                return false;
            }

            bool isHeld = activeKeyNames.Contains(configuredKey);
            if (!isHeld)
            {
                turboHoldStartedUtc.Remove(actionName);
                return false;
            }

            if (!controls.TurboToggle.Checked)
            {
                turboHoldStartedUtc.Remove(actionName);
                return true;
            }

            int turboHz = Math.Max(1, (int)controls.TurboHzBox.Value);
            if (!turboHoldStartedUtc.TryGetValue(actionName, out DateTime heldSinceUtc))
            {
                heldSinceUtc = DateTime.UtcNow;
                turboHoldStartedUtc[actionName] = heldSinceUtc;
            }

            double cycleMs = 1000.0 / turboHz;
            double elapsedMs = (DateTime.UtcNow - heldSinceUtc).TotalMilliseconds;
            double positionInCycle = elapsedMs % cycleMs;
            return positionInCycle < (cycleMs / 2.0);
        }

        private string GetConnectedControllerName()
        {
            return useXbox ? "Xbox 360" : "PS4";
        }

        private PatternStep GetCurrentPatternStep(List<PatternStep> pattern)
        {
            if (pattern.Count == 0)
            {
                return new PatternStep();
            }

            PatternStep currentStep = pattern[Math.Clamp(patternStepIndex, 0, pattern.Count - 1)];
            patternStepIndex = (patternStepIndex + 1) % pattern.Count;
            return currentStep;
        }

        private List<PatternStep> GetPatternSteps(string selectedPattern)
        {
            int radius = Math.Clamp((int)jitterStrengthBox.Value, 0, 45);
            if (string.Equals(selectedPattern, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return new List<PatternStep>();
            }

            List<PatternStep> pattern = new List<PatternStep>();

            if (string.Equals(selectedPattern, "Circle", StringComparison.OrdinalIgnoreCase))
            {
                pattern = BuildCirclePattern(radius);
                // Default circle steps to the standard update interval
                pattern.ForEach(s => s.Delay = DefaultPatternStepDelayMs);
            }
            else if (string.Equals(selectedPattern, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                // Parse directly from textbox text to catch X, Y, and Delay values dynamically.
                // Delay is optional per segment for backward compatibility with older saved patterns.
                string text = customPatternTextBox.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (string segment in text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        string[] parts = segment.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2 &&
                            int.TryParse(parts[0], out int x) &&
                            int.TryParse(parts[1], out int y))
                        {
                            int delay = DefaultPatternStepDelayMs;
                            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedDelay))
                            {
                                delay = Math.Max(1, parsedDelay);
                            }

                            pattern.Add(new PatternStep { X = ClampPercent(x), Y = ClampPercent(y), Delay = delay });
                        }
                    }
                }
            }
            else
            {
                pattern = BuildShakePattern(radius);
                pattern.ForEach(s => s.Delay = DefaultPatternStepDelayMs);
            }

            return CenterAndScalePattern(pattern, radius);
        }
        private static List<PatternStep> CenterAndScalePattern(List<PatternStep> steps, int radius)
        {
            if (steps.Count == 0)
            {
                return new List<PatternStep>();
            }
            double averageX = steps.Average(step => step.X);
            double averageY = steps.Average(step => step.Y);

            var centered = steps
                .Select(step => new PatternStep
                {
                    X = step.X - (int)Math.Round(averageX),
                    Y = step.Y - (int)Math.Round(averageY),
                    Delay = step.Delay
                })
                .ToList();

            int maxMagnitude = centered
                .Select(step => Math.Max(Math.Abs(step.X), Math.Abs(step.Y)))
                .DefaultIfEmpty(0)
                .Max();

            if (maxMagnitude <= 0 || radius <= 0)
            {
                return centered;
            }
            double scale = radius / (double)maxMagnitude;
            return centered
                .Select(step => new PatternStep
                {
                    X = ClampPercent((int)Math.Round(step.X * scale)),
                    Y = ClampPercent((int)Math.Round(step.Y * scale)),
                    Delay = step.Delay
                })
                .ToList();
        }

        private static List<PatternStep> BuildShakePattern(int radius)
        {
            int wide = Math.Max(4, radius);
            int tight = Math.Max(2, (int)Math.Round(radius * 0.45));

            return new List<PatternStep>
            {
                new() { X = -wide, Y = 0 },
                new() { X = wide, Y = 0 },
                new() { X = -tight, Y = tight },
                new() { X = tight, Y = -tight }
            };
        }

        private static List<PatternStep> BuildCirclePattern(int radius)
        {
            var steps = new List<PatternStep>();
            const int count = 16;
            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2 * i) / count;
                steps.Add(new PatternStep
                {
                    X = ClampPercent((int)Math.Round(Math.Cos(angle) * radius)),
                    Y = ClampPercent((int)Math.Round(Math.Sin(angle) * radius))
                });
            }

            return steps;
        }

        private void SendNeutralStick()
        {
            if (!isConnected)
            {
                return;
            }

            try
            {
                if (useXbox && xboxController != null)
                {
                    xboxController.SetAxisValue(Xbox360Axis.RightThumbX, 0);
                    xboxController.SetAxisValue(Xbox360Axis.RightThumbY, 0);
                    ApplyXboxBindings();
                    xboxController.SubmitReport();
                }
                else if (!useXbox && ds4Controller != null)
                {
                    ds4Controller.SetAxisValue(DualShock4Axis.RightThumbX, 128);
                    ds4Controller.SetAxisValue(DualShock4Axis.RightThumbY, 128);
                    ApplyDs4Bindings();
                    ds4Controller.SubmitReport();
                }
            }
            catch
            {
            }
        }

        private void WheelDisengageToggleButton_Click(object sender, EventArgs e)
        {
            wheelDisengageEnabled = !wheelDisengageEnabled;
            RefreshWheelDisengageButtonText();
        }

        private void RefreshWheelDisengageButtonText()
        {
            wheelDisengageToggleButton.Text = wheelDisengageEnabled ? "Wheel disengage: ON" : "Wheel disengage: OFF";
        }

        private void UseShakePresetButton_Click(object sender, EventArgs e)
        {
            customPatternPoints.Clear();
            customPatternPoints.AddRange(new[]
            {
                new PatternStep { X = -18, Y = 0, Delay = DefaultPatternStepDelayMs },
                new PatternStep { X = 18, Y = 0, Delay = DefaultPatternStepDelayMs },
                new PatternStep { X = -10, Y = 10, Delay = DefaultPatternStepDelayMs },
                new PatternStep { X = 10, Y = -10, Delay = DefaultPatternStepDelayMs }
            });
            SyncCustomPatternTextFromPoints();
        }

        private void UseCirclePresetButton_Click(object sender, EventArgs e)
        {
            customPatternPoints.Clear();
            foreach (PatternStep step in BuildCirclePattern(18))
            {
                customPatternPoints.Add(new PatternStep { X = step.X, Y = step.Y, Delay = DefaultPatternStepDelayMs });
            }

            SyncCustomPatternTextFromPoints();
        }

        private void ClearPatternButton_Click(object sender, EventArgs e)
        {
            customPatternPoints.Clear();
            SyncCustomPatternTextFromPoints();
        }

        private void CustomPatternTextBox_TextChanged(object sender, EventArgs e)
        {
            ParseCustomPatternText(customPatternTextBox.Text);
            patternPreviewPanel.Invalidate();
        }

        private void ParseCustomPatternText(string text)
        {
            customPatternPoints.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (string segment in text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = segment.Split(':', StringSplitOptions.TrimEntries);

                // X and Y are required; Delay is optional so older x:y-only patterns still load.
                if (parts.Length < 2)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) continue;

                int delay = DefaultPatternStepDelayMs;
                if (parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDelay))
                {
                    delay = Math.Max(1, parsedDelay);
                }

                customPatternPoints.Add(new PatternStep { X = ClampPercent(x), Y = ClampPercent(y), Delay = delay });
            }
        }

        private void SyncCustomPatternTextFromPoints()
        {
            customPatternTextBox.TextChanged -= CustomPatternTextBox_TextChanged;
            customPatternTextBox.Text = string.Join("|", customPatternPoints.Select(step => $"{step.X}:{step.Y}:{step.Delay}"));
            customPatternTextBox.TextChanged += CustomPatternTextBox_TextChanged;
            patternPreviewPanel.Invalidate();
        }

        private void PatternPreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            graphics.Clear(Color.White);

            using var axisPen = new Pen(Color.LightGray, 1);
            using var borderPen = new Pen(Color.Gray, 1);
            using var linePen = new Pen(Color.DodgerBlue, 2);
            using var pointBrush = new SolidBrush(Color.Crimson);
            using var activeBrush = new SolidBrush(Color.ForestGreen);

            int midX = rect.Width / 2;
            int midY = rect.Height / 2;

            graphics.DrawRectangle(borderPen, 0, 0, Math.Max(0, rect.Width - 1), Math.Max(0, rect.Height - 1));
            graphics.DrawLine(axisPen, midX, 0, midX, rect.Height);
            graphics.DrawLine(axisPen, 0, midY, rect.Width, midY);
            graphics.DrawString("X", Font, Brushes.Gray, rect.Width - 16, midY + 2);
            graphics.DrawString("Y", Font, Brushes.Gray, midX + 2, 2);

            if (customPatternPoints.Count == 0)
            {
                graphics.DrawString("Plot points here", Font, Brushes.Gray, 20, Math.Max(12, rect.Height / 2 - 10));
                return;
            }

            Point[] screenPoints = customPatternPoints.Select(ConvertPatternPointToScreen).ToArray();
            if (screenPoints.Length > 1)
            {
                graphics.DrawLines(linePen, screenPoints);
            }

            for (int i = 0; i < screenPoints.Length; i++)
            {
                Brush brush = i == 0 ? activeBrush : pointBrush;
                Point point = screenPoints[i];
                graphics.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
            }
        }

        private void PatternPreviewPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PatternStep newStep = ConvertScreenPointToPattern(e.Location);
                customPatternPoints.Add(newStep);
                SyncCustomPatternTextFromPoints();
            }
            else if (e.Button == MouseButtons.Right && customPatternPoints.Count > 0)
            {
                int removeIndex = FindNearestPatternPointIndex(e.Location);
                if (removeIndex >= 0)
                {
                    customPatternPoints.RemoveAt(removeIndex);
                    SyncCustomPatternTextFromPoints();
                }
            }
        }

        private int FindNearestPatternPointIndex(Point location)
        {
            const int maxDistanceSquared = 12 * 12;
            int closestIndex = -1;
            int closestDistanceSquared = int.MaxValue;

            for (int i = 0; i < customPatternPoints.Count; i++)
            {
                Point screenPoint = ConvertPatternPointToScreen(customPatternPoints[i]);
                int dx = screenPoint.X - location.X;
                int dy = screenPoint.Y - location.Y;
                int distanceSquared = dx * dx + dy * dy;
                if (distanceSquared <= maxDistanceSquared && distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private Point ConvertPatternPointToScreen(PatternStep step)
        {
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            int width = Math.Max(1, rect.Width - 1);
            int height = Math.Max(1, rect.Height - 1);
            int x = (int)Math.Round(((step.X + 100) / 200.0) * width);
            int y = (int)Math.Round(((100 - step.Y) / 200.0) * height);
            return new Point(Math.Clamp(x, 0, width), Math.Clamp(y, 0, height));
        }

        private PatternStep ConvertScreenPointToPattern(Point screenPoint)
        {
            Rectangle rect = patternPreviewPanel.ClientRectangle;
            int x = ClampPercent((int)Math.Round((screenPoint.X / (double)Math.Max(1, rect.Width - 1)) * 200 - 100));
            int y = ClampPercent((int)Math.Round(100 - (screenPoint.Y / (double)Math.Max(1, rect.Height - 1)) * 200));
            return new PatternStep { X = x, Y = y, Delay = DefaultPatternStepDelayMs };
        }

        private void JitterPatternBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            patternStepIndex = 0;
        }

        private static short PercentToXboxAxis(int percent)
        {
            int clamped = ClampPercent(percent);
            return (short)Math.Round(clamped * (short.MaxValue / 100.0));
        }

        private static byte PercentToDs4Axis(int percent)
        {
            int clamped = ClampPercent(percent);
            return (byte)Math.Clamp((int)Math.Round(128 + (clamped * 127.0 / 100.0)), 0, 255);
        }

        private static int ClampPercent(int value)
        {
            return Math.Clamp(value, -100, 100);
        }

        private int GetBasePullDirectionX(int baseDriftPercent)
        {
            string pullDirection = pullDirectionBox.SelectedItem?.ToString() ?? "Center";
            return pullDirection switch
            {
                "Left" => -baseDriftPercent,
                "Right" => baseDriftPercent,
                _ => 0
            };
        }

        private void EnsureProfilesDirectory()
        {
            Directory.CreateDirectory(profilesDirectory);
        }

        private void EnsurePatternsDirectory()
        {
            Directory.CreateDirectory(patternsDirectory);
        }

        private void ApplyDefaults()
        {
            profileComboBox.Text = "Default";
            valueLabel.Text = "Base downward drift: 0%";
            wheelDisengageEnabled = true;
            RefreshWheelDisengageButtonText();
            controllerTypeBox.SelectedItem = "PS4";
            jitterHoldKeyBox.SelectedItem = "Shift";
            jitterPatternBox.SelectedItem = "Shake";
            pullDirectionBox.SelectedItem = "Center";
            holdExtraDriftBox.Value = 8;
            jitterStrengthBox.Value = 18;

            foreach (string actionName in BindingActions)
            {
                if (bindingControls.TryGetValue(actionName, out BindingControlSet controls))
                {
                    controls.KeySelector.SelectedItem = "None";
                    controls.TurboToggle.Checked = false;
                    controls.TurboHzBox.Value = 12;
                }
            }

            ParseCustomPatternText(customPatternTextBox.Text);
            patternPreviewPanel.Invalidate();
        }

        private void SaveProfileButton_Click(object sender, EventArgs e)
        {
            string profileName = (profileComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Enter a profile name first.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var profile = new InputProfile
            {
                Name = profileName,
                ControllerType = controllerTypeBox.SelectedItem?.ToString() ?? "PS4",
                BaseDriftPercent = driftSlider.Value,
                BasePullDirection = pullDirectionBox.SelectedItem?.ToString() ?? "Center",
                HoldExtraDriftPercent = (int)holdExtraDriftBox.Value,
                JitterDelayMs = (int)jitterDelayBox.Value,
                JitterStrengthPercent = (int)jitterStrengthBox.Value,
                ScrollDisengageMs = (int)scrollDisengageBox.Value,
                WheelDisengageEnabled = wheelDisengageEnabled,
                JitterHoldKey = jitterHoldKeyBox.SelectedItem?.ToString() ?? "Shift",
                JitterPatternType = jitterPatternBox.SelectedItem?.ToString() ?? "Shake",
                CustomPatternPoints = customPatternTextBox.Text,
                DisableKeys = disableKeysList.CheckedItems.Cast<object>().Select(item => item.ToString()).ToList(),
                KeyBindings = bindingControls.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.KeySelector.SelectedItem?.ToString() ?? "None",
                    StringComparer.OrdinalIgnoreCase),
                TurboEnabled = bindingControls.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.TurboToggle.Checked,
                    StringComparer.OrdinalIgnoreCase),
                TurboHz = bindingControls.ToDictionary(
                    pair => pair.Key,
                    pair => (int)pair.Value.TurboHzBox.Value,
                    StringComparer.OrdinalIgnoreCase)
            };

            string path = GetProfilePath(profileName);
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            LoadProfileList(profileName);
            statusLabel.Text = $"Status: Saved profile '{profileName}'";
        }

        private void LoadProfileButton_Click(object sender, EventArgs e)
        {
            string profileName = (profileComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Choose or type a profile name.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = GetProfilePath(profileName);
            if (!File.Exists(path))
            {
                MessageBox.Show("That profile does not exist yet.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            InputProfile profile = JsonSerializer.Deserialize<InputProfile>(File.ReadAllText(path));
            if (profile == null)
            {
                MessageBox.Show("Failed to read that profile.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ApplyProfile(profile);
            statusLabel.Text = $"Status: Loaded profile '{profile.Name}'";
        }

        private void DeleteProfileButton_Click(object sender, EventArgs e)
        {
            string profileName = (profileComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Choose a profile to delete.", "Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = GetProfilePath(profileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            LoadProfileList();
            statusLabel.Text = $"Status: Deleted profile '{profileName}'";
        }

        private void LoadProfileList(string selectedProfileName = null)
        {
            profileComboBox.Items.Clear();

            IEnumerable<string> profileNames = Directory
                .EnumerateFiles(profilesDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            foreach (string name in profileNames)
            {
                profileComboBox.Items.Add(name);
            }

            if (!string.IsNullOrWhiteSpace(selectedProfileName))
            {
                profileComboBox.Text = selectedProfileName;
            }
            else if (profileComboBox.Items.Count > 0)
            {
                profileComboBox.SelectedIndex = 0;
            }
        }

        private void SavePatternButton_Click(object sender, EventArgs e)
        {
            string patternName = (patternComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(patternName))
            {
                MessageBox.Show("Enter a pattern name first.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (customPatternPoints.Count == 0)
            {
                MessageBox.Show("There are no pattern points to save. Add some points first.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = GetPatternPath(patternName);
            File.WriteAllLines(path, customPatternPoints.Select(step => $"{step.X},{step.Y},{step.Delay}"));
            LoadPatternList(patternName);
            statusLabel.Text = $"Status: Saved pattern '{patternName}'";
        }

        private void LoadPatternButton_Click(object sender, EventArgs e)
        {
            string patternName = (patternComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(patternName))
            {
                MessageBox.Show("Choose or type a pattern name.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = GetPatternPath(patternName);
            if (!File.Exists(path))
            {
                MessageBox.Show("That pattern does not exist yet.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<PatternStep> loadedPoints = ParsePatternFile(File.ReadAllLines(path));
            if (loadedPoints.Count == 0)
            {
                MessageBox.Show("That pattern file doesn't contain any valid points.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            customPatternPoints.Clear();
            customPatternPoints.AddRange(loadedPoints);
            SyncCustomPatternTextFromPoints();
            statusLabel.Text = $"Status: Loaded pattern '{patternName}'";
        }

        private void DeletePatternButton_Click(object sender, EventArgs e)
        {
            string patternName = (patternComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(patternName))
            {
                MessageBox.Show("Choose a pattern to delete.", "Pattern Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = GetPatternPath(patternName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            LoadPatternList();
            statusLabel.Text = $"Status: Deleted pattern '{patternName}'";
        }

        // Reads the Patterns/*.txt line-by-line format: one "x,y,delay" point per line (delay optional).
        private List<PatternStep> ParsePatternFile(IEnumerable<string> lines)
        {
            var points = new List<PatternStep>();

            foreach (string rawLine in lines)
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',', StringSplitOptions.TrimEntries);

                // X and Y are required; Delay is optional so x,y-only lines still load.
                if (parts.Length < 2)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) continue;

                int delay = DefaultPatternStepDelayMs;
                if (parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDelay))
                {
                    delay = Math.Max(1, parsedDelay);
                }

                points.Add(new PatternStep { X = ClampPercent(x), Y = ClampPercent(y), Delay = delay });
            }

            return points;
        }

        private string GetPatternPath(string patternName)
        {
            string safeName = string.Concat(patternName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Pattern";
            }

            return Path.Combine(patternsDirectory, safeName + ".txt");
        }

        private void LoadPatternList(string selectedPatternName = null)
        {
            patternComboBox.Items.Clear();

            IEnumerable<string> patternNames = Directory
                .EnumerateFiles(patternsDirectory, "*.txt")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            foreach (string name in patternNames)
            {
                patternComboBox.Items.Add(name);
            }

            if (!string.IsNullOrWhiteSpace(selectedPatternName))
            {
                patternComboBox.Text = selectedPatternName;
            }
            else if (patternComboBox.Items.Count > 0)
            {
                patternComboBox.SelectedIndex = 0;
            }
        }

        private void ApplyProfile(InputProfile profile)
        {
            profileComboBox.Text = profile.Name;
            controllerTypeBox.SelectedItem = controllerTypeBox.Items.Contains(profile.ControllerType) ? profile.ControllerType : "PS4";
            driftSlider.Value = Math.Clamp(profile.BaseDriftPercent, driftSlider.Minimum, driftSlider.Maximum);
            string pullDirection = string.IsNullOrWhiteSpace(profile.BasePullDirection) ? "Center" : profile.BasePullDirection;
            if (!pullDirectionBox.Items.Contains(pullDirection))
            {
                pullDirection = "Center";
            }

            pullDirectionBox.SelectedItem = pullDirection;
            holdExtraDriftBox.Value = Math.Clamp(profile.HoldExtraDriftPercent, (int)holdExtraDriftBox.Minimum, (int)holdExtraDriftBox.Maximum);
            jitterDelayBox.Value = Math.Clamp(profile.JitterDelayMs, (int)jitterDelayBox.Minimum, (int)jitterDelayBox.Maximum);
            jitterStrengthBox.Value = Math.Clamp(profile.JitterStrengthPercent, (int)jitterStrengthBox.Minimum, (int)jitterStrengthBox.Maximum);
            scrollDisengageBox.Value = Math.Clamp(profile.ScrollDisengageMs, (int)scrollDisengageBox.Minimum, (int)scrollDisengageBox.Maximum);

            string jitterHoldKey = string.IsNullOrWhiteSpace(profile.JitterHoldKey) ? "Shift" : profile.JitterHoldKey;
            if (!jitterHoldKeyBox.Items.Contains(jitterHoldKey))
            {
                jitterHoldKey = "Shift";
            }

            jitterHoldKeyBox.SelectedItem = jitterHoldKey;

            string jitterPatternType = string.IsNullOrWhiteSpace(profile.JitterPatternType) ? "Shake" : profile.JitterPatternType;
            if (!jitterPatternBox.Items.Contains(jitterPatternType))
            {
                jitterPatternType = "Shake";
            }

            jitterPatternBox.SelectedItem = jitterPatternType;

            wheelDisengageEnabled = profile.WheelDisengageEnabled;
            RefreshWheelDisengageButtonText();

            for (int i = 0; i < disableKeysList.Items.Count; i++)
            {
                string keyName = disableKeysList.Items[i]?.ToString() ?? string.Empty;
                bool isChecked = profile.DisableKeys?.Any(key => string.Equals(key, keyName, StringComparison.OrdinalIgnoreCase)) == true;
                disableKeysList.SetItemChecked(i, isChecked);
            }

            foreach (string actionName in BindingActions)
            {
                string selectedKey = "None";
                if (profile.KeyBindings != null && profile.KeyBindings.TryGetValue(actionName, out string savedKey) && !string.IsNullOrWhiteSpace(savedKey))
                {
                    selectedKey = savedKey;
                }

                if (bindingControls.TryGetValue(actionName, out BindingControlSet controls))
                {
                    if (!controls.KeySelector.Items.Contains(selectedKey))
                    {
                        selectedKey = "None";
                    }

                    controls.KeySelector.SelectedItem = selectedKey;

                    bool turboEnabled = profile.TurboEnabled?.TryGetValue(actionName, out bool savedTurboEnabled) == true && savedTurboEnabled;
                    controls.TurboToggle.Checked = turboEnabled;

                    int turboHz = 12;
                    if (profile.TurboHz?.TryGetValue(actionName, out int savedTurboHz) == true)
                    {
                        turboHz = savedTurboHz;
                    }

                    controls.TurboHzBox.Value = Math.Clamp(turboHz, (int)controls.TurboHzBox.Minimum, (int)controls.TurboHzBox.Maximum);
                }
            }

            customPatternTextBox.Text = string.IsNullOrWhiteSpace(profile.CustomPatternPoints)
                ? "-18:0:10|18:0:10|-10:10:10|10:-10:10"
                : profile.CustomPatternPoints;

            valueLabel.Text = $"Base downward drift: {driftSlider.Value}%";
            SendCurrentStickValue();
        }

        private string GetProfilePath(string profileName)
        {
            string safeName = string.Concat(profileName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "Profile";
            }

            return Path.Combine(profilesDirectory, safeName + ".json");
        }

        private void CleanupController()
        {
            updateTimer.Stop();

            try
            {
                xboxController?.Disconnect();
            }
            catch
            {
            }

            try
            {
                ds4Controller?.Disconnect();
            }
            catch
            {
            }

            xboxController = null;
            ds4Controller = null;
            client = null;
            isConnected = false;
            isTemporarilyDisengaged = false;
            isJitterBindDown = false;
            jitterHeldSinceUtc = null;
            disengagedUntilUtc = null;
            patternStepIndex = 0;
            turboHoldStartedUtc.Clear();

            controllerTypeBox.Enabled = true;
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
            statusLabel.Text = "Status: Not connected";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                inputHook?.Dispose();
            }
            catch
            {
            }

            CleanupController();
        }
    }
}
