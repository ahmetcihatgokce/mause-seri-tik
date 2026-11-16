using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MauseSeriTik
{
    internal sealed class MainForm : Form
    {
        private const int TargetHotKey = 1;
        private const int ToggleHotKey = 2;
        private const int StopHotKey = 3;
        private const int AimColorHotKey = 4;
        private const string SprintHint = "W basılıyken oyunun Shift tuşu tutulur.";
        private static readonly Color Back = Color.FromArgb(17, 22, 29);
        private static readonly Color Surface = Color.FromArgb(33, 40, 50);
        private static readonly Color Foreground = Color.FromArgb(242, 245, 249);
        private static readonly Color Muted = Color.FromArgb(173, 185, 201);
        private static readonly Color Accent = Color.FromArgb(114, 226, 179);
        private static readonly Color Error = Color.FromArgb(255, 164, 150);

        private readonly ComboBox mode = new ComboBox();
        private readonly NumericUpDown rate = new NumericUpDown();
        private readonly NumericUpDown count = new NumericUpDown();
        private readonly CheckBox aimEnabled;
        private readonly CheckBox sprintEnabled;
        private readonly Label aimHelp;
        private readonly Label sprintHelp;
        private readonly Label targetLabel;
        private readonly Label status;
        private readonly Label hotkeys;
        private readonly Button capture;
        private readonly Button toggle;
        private readonly Timer UIRefresh = new Timer { Interval = 200 };
        private readonly uint processId = (uint)Process.GetCurrentProcess().Id;
        private TargetWindow target;
        private ClickEngine engine;
        private SprintAssist sprint;
        private Color? aimColor;
        private DateTime captureAt;
        private string notice;
        private string stopKeyText;
        private bool captureKeyRegistered;
        private bool toggleKeyRegistered;
        private bool stopKeyRegistered;
        private bool aimColorKeyRegistered;
        private bool closing;

        internal MainForm()
        {
            Text = "Mause Seri Tık";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = true;
            MaximizeBox = false;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScroll = true;
            ClientSize = new Size(494, 630);
            BackColor = Back;
            ForeColor = Foreground;
            Font = new Font("Segoe UI", 10f);

            Label heading = AddLabel("Mause Seri Tık", 24, 20, 446, 44, Accent);
            heading.Font = new Font("Segoe UI Semibold", 23f);
            AddLabel("Sol tuş tekrarı yalnızca seçilen pencerede çalışır.", 25, 69, 450, 30, Muted);

            AddLabel("Hedef pencere", 24, 117, 270, 22, Foreground);
            targetLabel = AddLabel("Henüz seçilmedi", 24, 143, 285, 36, Muted);
            targetLabel.AutoEllipsis = true;
            targetLabel.TextAlign = ContentAlignment.MiddleLeft;
            capture = AddButton("Pencere seç", 326, 139, 144, 38, false);
            capture.AccessibleDescription = "Hedef pencereye geçiş için dört saniye verir.";
            capture.Click += delegate { BeginCapture(); };

            AddLabel("Tıklama biçimi", 24, 196, 380, 22, Foreground);
            mode.SetBounds(24, 222, 446, 32);
            mode.BackColor = Surface;
            mode.ForeColor = Foreground;
            mode.DropDownStyle = ComboBoxStyle.DropDownList;
            mode.FlatStyle = FlatStyle.Flat;
            mode.AccessibleName = "Tıklama biçimi";
            mode.Items.AddRange(new object[] { "Basılı tut", "Sınırlı seri" });
            mode.SelectedIndex = 0;
            Controls.Add(mode);

            AddLabel("Hedef hız (tık/sn)", 24, 273, 210, 22, Foreground);
            AddLabel("Seri sınırı", 255, 273, 215, 22, Foreground);
            AddNumber(rate, "Hedef hız, saniyede tıklama", 24, 299, 1, 500, 30);
            rate.Width = 109;
            Button maxRate = AddButton("En hızlı", 140, 294, 99, 33, false);
            maxRate.AccessibleDescription = "Hedef hızı saniyede 500 tıklamaya ayarlar.";
            maxRate.Click += delegate { rate.Value = rate.Maximum; };
            AddNumber(count, "Seri sınırı", 255, 299, 2, 20, 5);
            mode.SelectedIndexChanged += delegate { Configure(); };
            rate.ValueChanged += delegate { Configure(); };
            count.ValueChanged += delegate { Configure(); };

            aimEnabled = AddCheckBox("Renkle nişan yardımı", 24, 340, 264);
            aimEnabled.AccessibleDescription = "İlk otomatik tıklamadan önce, seçilen renge göre nişanı düzeltmeyi dener.";
            aimEnabled.CheckedChanged += delegate { ConfigureAim(); };
            Button pickColor = AddButton("Hedef rengi seç", 302, 340, 168, 34, false);
            pickColor.Click += delegate { PickAimColor(); };
            aimHelp = AddLabel("Hedef rengi seçin. Tam ekranda, nişangâh ortadayken kullanın.",
                24, 380, 446, 46, Muted);

            sprintEnabled = AddCheckBox("Otomatik koşma", 24, 426, 446);
            sprintEnabled.AccessibleDescription = SprintHint;
            sprintEnabled.CheckedChanged += delegate { RefreshState(); };
            sprintHelp = AddLabel(SprintHint, 24, 454, 446, 34, Muted);

            toggle = AddButton("Etkinleştir", 24, 493, 446, 42, true);
            toggle.Enabled = false;
            toggle.Click += delegate { Toggle(); };
            status = AddLabel("Kapalı · Önce bir hedef pencere seçin.", 24, 543, 446, 45, Muted);
            status.TextAlign = ContentAlignment.MiddleLeft;
            hotkeys = AddLabel("F7 pencere seç      F8 aç/kapat      F9 acil durdur", 24, 596, 446, 24, Muted);

            UIRefresh.Tick += delegate { RefreshState(); };
            AcceptButton = toggle;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            if (Height > workingArea.Height)
            {
                Height = workingArea.Height;
                Top = workingArea.Top;
            }
            engine = new ClickEngine();
            sprint = new SprintAssist(this);
            captureKeyRegistered = Register(TargetHotKey, Keys.F7, 0);
            toggleKeyRegistered = Register(ToggleHotKey, Keys.F8, 0);
            stopKeyRegistered = Register(StopHotKey, Keys.F9, 0);
            aimColorKeyRegistered = Register(AimColorHotKey, Keys.F6, 0);
            stopKeyText = "F9";
            if (!stopKeyRegistered)
            {
                stopKeyRegistered = Register(StopHotKey, Keys.F9, 0x0002 | 0x0004);
                stopKeyText = "Ctrl+Shift+F9";
            }
            hotkeys.Text = (captureKeyRegistered ? "F7 pencere seç      " : "") +
                (toggleKeyRegistered ? "F8 aç/kapat      " : "") +
                (stopKeyRegistered ? stopKeyText + " acil durdur" : "Acil durdurma tuşu kullanılamıyor");

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            Configure();
            ConfigureAim();
            RefreshState();
            UIRefresh.Start();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmHotkey && engine != null && !closing)
            {
                switch (message.WParam.ToInt32())
                {
                    case TargetHotKey:
                        CaptureTarget();
                        break;
                    case ToggleHotKey:
                        Toggle();
                        break;
                    case StopHotKey:
                        captureAt = DateTime.MinValue;
                        engine.Disarm();
                        notice = "Acil durdurma uygulandı.";
                        RefreshState();
                        break;
                    case AimColorHotKey:
                        SampleAimColor();
                        break;
                }
            }
            base.WndProc(ref message);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closing)
            {
                closing = true;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                if (sprint != null)
                    sprint.Configure(null, false);
                if (engine != null && !engine.HasStopped)
                    engine.RequestShutdown();
            }
            if (engine != null && !engine.HasStopped)
                e.Cancel = e.CloseReason != CloseReason.WindowsShutDown;
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UIRefresh.Dispose();
            if (captureKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, TargetHotKey);
            if (toggleKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, ToggleHotKey);
            if (stopKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, StopHotKey);
            if (aimColorKeyRegistered)
                NativeMethods.UnregisterHotKey(Handle, AimColorHotKey);
            if (sprint != null)
                sprint.Dispose();
            if (engine != null && engine.HasStopped)
                engine.Dispose();
            base.OnFormClosed(e);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
                SuspendAutomation();
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect ||
                e.Reason == SessionSwitchReason.ConsoleDisconnect)
                SuspendAutomation();
        }

        private void SuspendAutomation()
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    try { BeginInvoke((Action)SuspendAutomation); }
                    catch (InvalidOperationException) { }
                }
                return;
            }
            if (!closing)
            {
                captureAt = DateTime.MinValue;
                engine.Disarm();
                RefreshState();
            }
        }

        private void BeginCapture()
        {
            engine.Disarm();
            notice = null;
            captureAt = DateTime.UtcNow.AddSeconds(4);
            RefreshState();
        }

        private void CaptureTarget()
        {
            captureAt = DateTime.MinValue;
            engine.Disarm();
            TargetWindow selected = TargetWindow.Capture(processId);
            if (selected == null)
            {
                notice = "Hedef pencereye geçip F7’ye basın veya yeniden pencere seçin.";
            }
            else
            {
                target = selected;
                engine.SetTarget(target);
                targetLabel.Text = target.Title;
                notice = null;
            }
            RefreshState();
        }

        private void Toggle()
        {
            if (!stopKeyRegistered || closing || captureAt != DateTime.MinValue)
                return;

            notice = null;
            if (engine.Armed)
                engine.Disarm();
            else if (!engine.Arm())
                notice = "Önce açık bir hedef pencere seçin.";

            RefreshState();
        }

        private void Configure()
        {
            count.Enabled = mode.SelectedIndex != (int)ClickMode.Hold;
            if (engine != null)
                engine.Configure((ClickMode)mode.SelectedIndex, (int)rate.Value, (int)count.Value);
        }

        private void ConfigureAim()
        {
            Color? color = aimColor;
            if (engine != null)
                engine.ConfigureAim(aimEnabled.Checked && color.HasValue
                    ? new AimSettings(color.Value, radius: 80, tolerance: 24, sensitivity: 0.45) : null);

            string selection = color.HasValue ? "Renk #" + (color.Value.ToArgb() & 0xFFFFFF).ToString("X6") + "." :
                "Hedef rengi seçin.";
            string shortcut = aimColorKeyRegistered ? " İmleci renge getirip F6'ya basın." : string.Empty;
            aimHelp.ForeColor = Muted;
            aimHelp.Text = selection + shortcut + Environment.NewLine + "Tam ekranda, nişangâh ortadayken kullanın.";
        }

        private void PickAimColor()
        {
            using (var dialog = new ColorDialog { Color = aimColor.GetValueOrDefault(Color.White), FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                aimColor = dialog.Color;
                ConfigureAim();
            }
        }

        private void SampleAimColor()
        {
            NativeMethods.Point cursor;
            Color color;
            if (target == null || !NativeMethods.GetCursorPos(out cursor) || !target.AcceptsPoint(cursor))
            {
                aimHelp.ForeColor = Error;
                aimHelp.Text = "Önce hedef pencereye geçin. İmleci hedef rengine getirip F6'ya basın.";
                return;
            }
            if (!AimAssist.TryReadColor(new Point(cursor.X, cursor.Y), out color))
            {
                aimHelp.ForeColor = Error;
                aimHelp.Text = "Renk okunamadı. F6 ile tekrar deneyin veya hedef rengini seçin.";
                return;
            }
            aimColor = color;
            ConfigureAim();
        }

        private void RefreshState()
        {
            if (closing)
            {
                if (engine == null || engine.HasStopped)
                    Close();
                return;
            }
            if (engine == null)
                return;

            if (captureAt != DateTime.MinValue && DateTime.UtcNow >= captureAt)
            {
                CaptureTarget();
                return;
            }

            if (target != null && !target.Exists())
            {
                target = null;
                engine.SetTarget(null);
                targetLabel.Text = "Hedef pencere kapandı";
                notice = null;
            }

            bool armed = engine.Armed;
            if (sprint != null)
            {
                sprint.Configure(target, sprintEnabled.Checked && armed);
                string sprintFailure = sprint.HookInstalled ? sprint.Failure :
                    "Windows koşma dinleyicisi açılamadı (kod: " + sprint.HookError + ").";
                bool sprintFailed = sprintEnabled.Checked && !string.IsNullOrEmpty(sprintFailure);
                sprintHelp.ForeColor = sprintFailed ? Error : Muted;
                sprintHelp.Text = sprintFailed ? sprintFailure : SprintHint;
            }
            toggle.Text = armed ? "Durdur" : "Etkinleştir";
            toggle.Enabled = target != null && engine.HookInstalled && stopKeyRegistered &&
                captureAt == DateTime.MinValue;

            status.ForeColor = armed ? Accent : Muted;
            if (!engine.HookInstalled || !stopKeyRegistered || engine.Failure != null)
            {
                status.ForeColor = Error;
                if (!engine.HookInstalled)
                    status.Text = "Windows fare dinleyicisi açılamadı (kod: " + engine.HookError + ").";
                else if (!stopKeyRegistered)
                    status.Text = "Acil durdurma tuşu başka bir uygulamada kullanımda.";
                else
                    status.Text = engine.Failure;
            }
            else if (captureAt != DateTime.MinValue)
            {
                int seconds = Math.Max(1, (int)Math.Ceiling((captureAt - DateTime.UtcNow).TotalSeconds));
                status.Text = "Hedef pencereye geçin · " + seconds + " sn";
            }
            else if (armed)
                status.Text = target.IsForeground() ? "Etkin · " + stopKeyText + " ile durdurun." :
                    "Hazır · Hedef pencereyi öne getirin.";
            else
                status.Text = notice ?? (target == null ? "Kapalı · Önce bir hedef pencere seçin." :
                    (toggleKeyRegistered ? "Kapalı · Etkinleştirmek için düğmeye veya F8’e basın." :
                    "Kapalı · Etkinleştir düğmesine basın."));
        }

        private bool Register(int id, Keys key, uint modifiers)
        {
            return NativeMethods.RegisterHotKey(Handle, id, NativeMethods.ModNoRepeat | modifiers, (uint)key);
        }

        private Label AddLabel(string text, int x, int y, int width, int height, Color color)
        {
            var label = new Label { Text = text, ForeColor = color, UseMnemonic = false };
            label.SetBounds(x, y, width, height);
            Controls.Add(label);
            return label;
        }

        private Button AddButton(string text, int x, int y, int width, int height, bool primary)
        {
            var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Surface, ForeColor = primary ? Back : Foreground };
            button.FlatAppearance.BorderSize = 0;
            button.SetBounds(x, y, width, height);
            Controls.Add(button);
            return button;
        }

        private CheckBox AddCheckBox(string text, int x, int y, int width)
        {
            var checkBox = new CheckBox { Text = text, ForeColor = Foreground, UseMnemonic = false };
            checkBox.SetBounds(x, y, width, 29);
            Controls.Add(checkBox);
            return checkBox;
        }

        private void AddNumber(NumericUpDown number, string name, int x, int y, int min, int max, int start)
        {
            number.SetBounds(x, y, 215, 32);
            number.Minimum = min;
            number.Maximum = max;
            number.Value = start;
            number.BorderStyle = BorderStyle.FixedSingle;
            number.BackColor = Surface;
            number.ForeColor = Foreground;
            number.AccessibleName = name;
            Controls.Add(number);
        }
    }
}
