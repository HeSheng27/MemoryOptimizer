using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    internal sealed class MainForm : Form
    {
        private readonly Settings _settings;
        private readonly bool _startHidden;
        private readonly bool _previewMode;
        private readonly System.Windows.Forms.Timer _uiTimer;
        private readonly ToolTip _tips;

        private CardPanel _cardOverview;
        private CardPanel _cardOptions;
        private CardPanel _cardAuto;
        private CardPanel _cardLog;
        private MemoryGauge _gauge;
        private CheckBox _chkTrim;
        private CheckBox _chkStandby;
        private CheckBox _chkCache;
        private CheckBox _chkStartup;
        private CheckBox _chkStartMin;
        private CheckBox _chkCloseTray;
        private FlatButton _btnOptimize;
        private FlatButton _btnClearLog;
        private FlatButton _btnExclude;
        private FlatButton _btnElevate;
        private NumericUpDown _numThreshold;
        private NumericUpDown _numCooldown;
        private SwitchToggle _switchAuto;
        private Label _lblLastRun;
        private Label _lblStatus;
        private LogView _logView;

        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayAutoItem;

        private bool _loading = true;
        private bool _isOptimizing;
        private bool _exiting;
        private bool _allowVisible;
        private bool _trayBalloonShown;
        private bool _wsBusy;
        private int _tick;
        private DateTime _lastOptimizeUtc = DateTime.MinValue;
        private long _totalFreed;
        private MemorySnapshot _lastSnapshot;

        public MainForm(Settings settings, bool startHidden, bool previewMode)
        {
            _settings = settings ?? new Settings();
            _startHidden = startHidden;
            _previewMode = previewMode;

            Text = "内存优化器";
            Font = Theme.Body;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            ClientSize = new Size(820, 720);
            MinimumSize = new Size(780, 660);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            DoubleBuffered = true;
            KeyPreview = true;
            try { Icon = AppIcon.Load(); }
            catch { }

            _tips = new ToolTip();
            _tips.AutoPopDelay = 12000;
            _tips.InitialDelay = 400;
            _tips.ReshowDelay = 100;

            BuildUi();
            ApplySettings();
            BuildTray();
            WriteStartupLog();

            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 1000;
            _uiTimer.Tick += OnUiTick;
            _uiTimer.Start();

            RefreshDashboard();
        }

        // ────────────────────────────────────────────────────────────────
        // 界面构建
        // ────────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            SuspendLayout();

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Theme.Bg;
            root.Padding = new Padding(14, 14, 14, 8);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.Margin = new Padding(0);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            Controls.Add(root);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = Theme.Bg;
            grid.ColumnCount = 1;
            grid.RowCount = 4;
            grid.Margin = new Padding(0);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 186f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 154f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 142f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(grid, 0, 0);

            // 内存概览
            _cardOverview = new CardPanel();
            _cardOverview.Title = "内存概览";
            _cardOverview.HeaderNote = "每 1 秒刷新";
            _cardOverview.Dock = DockStyle.Fill;
            _gauge = new MemoryGauge();
            _gauge.Dock = DockStyle.Fill;
            _cardOverview.Controls.Add(_gauge);
            grid.Controls.Add(_cardOverview, 0, 0);

            // 优化项目
            _cardOptions = new CardPanel();
            _cardOptions.Title = "优化项目";
            _cardOptions.HeaderNote = "清理瞬间可能造成短暂卡顿";
            _cardOptions.Padding = new Padding(18, 46, 18, 12);
            _cardOptions.Dock = DockStyle.Fill;
            grid.Controls.Add(_cardOptions, 0, 1);

            _btnExclude = new FlatButton();
            _btnExclude.SetCompact();
            _btnExclude.Primary = false;
            _btnExclude.Text = "排除名单";
            _btnExclude.Size = new Size(90, 26);
            _btnExclude.Click += OnExcludeClick;
            _cardOptions.AddHeaderControl(_btnExclude);
            _tips.SetToolTip(_btnExclude, "指定不参与清理的进程；系统关键进程始终会被跳过。");

            TableLayoutPanel optionsLayout = new TableLayoutPanel();
            optionsLayout.Dock = DockStyle.Fill;
            optionsLayout.ColumnCount = 1;
            optionsLayout.RowCount = 2;
            optionsLayout.Margin = new Padding(0);
            optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _cardOptions.Controls.Add(optionsLayout);

            FlowLayoutPanel checks = new FlowLayoutPanel();
            checks.Dock = DockStyle.Fill;
            checks.WrapContents = false;
            checks.Margin = new Padding(0);
            checks.Padding = new Padding(0);
            optionsLayout.Controls.Add(checks, 0, 0);

            EventHandler optionChanged = new EventHandler(OnOptionChanged);
            _chkTrim = CreateCheck("压缩进程工作集", "对每个可访问的进程调用 EmptyWorkingSet，把暂时不用的内存页移出物理内存。", optionChanged);
            _chkTrim.Margin = new Padding(0, 5, 36, 0);
            _chkStandby = CreateCheck("清理待机内存列表", "清空 Standby List（系统预读缓存）。需要管理员权限。", optionChanged);
            _chkStandby.Margin = new Padding(0, 5, 36, 0);
            _chkCache = CreateCheck("清理系统文件缓存", "刷新 System File Cache。需要管理员权限。", optionChanged);
            _chkCache.Margin = new Padding(0, 5, 0, 0);
            checks.Controls.Add(_chkTrim);
            checks.Controls.Add(_chkStandby);
            checks.Controls.Add(_chkCache);

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 2;
            actions.RowCount = 1;
            actions.Margin = new Padding(0, 6, 0, 0);
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            optionsLayout.Controls.Add(actions, 0, 1);

            _btnOptimize = new FlatButton();
            _btnOptimize.Text = "一键优化";
            _btnOptimize.Dock = DockStyle.Fill;
            _btnOptimize.Margin = new Padding(0, 4, 12, 2);
            _btnOptimize.Click += delegate { StartOptimize(false); };
            actions.Controls.Add(_btnOptimize, 0, 0);
            _tips.SetToolTip(_btnOptimize, "立即执行勾选的优化项目（快捷键 F5）。");

            _lblLastRun = new Label();
            _lblLastRun.Text = "尚未执行优化";
            _lblLastRun.Dock = DockStyle.Fill;
            _lblLastRun.TextAlign = ContentAlignment.MiddleLeft;
            _lblLastRun.Font = Theme.Caps;
            _lblLastRun.ForeColor = Theme.TextMuted;
            _lblLastRun.AutoEllipsis = true;
            _lblLastRun.Margin = new Padding(0);
            actions.Controls.Add(_lblLastRun, 1, 0);

            // 自动优化
            _cardAuto = new CardPanel();
            _cardAuto.Title = "自动优化";
            _cardAuto.Padding = new Padding(18, 46, 18, 12);
            _cardAuto.Dock = DockStyle.Fill;
            grid.Controls.Add(_cardAuto, 0, 2);

            TableLayoutPanel autoLayout = new TableLayoutPanel();
            autoLayout.Dock = DockStyle.Fill;
            autoLayout.ColumnCount = 1;
            autoLayout.RowCount = 2;
            autoLayout.Margin = new Padding(0);
            autoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            autoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            autoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _cardAuto.Controls.Add(autoLayout);

            TableLayoutPanel autoRow = new TableLayoutPanel();
            autoRow.Dock = DockStyle.Fill;
            autoRow.ColumnCount = 8;
            autoRow.RowCount = 1;
            autoRow.Margin = new Padding(0);
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62f));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66f));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            autoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));
            autoLayout.Controls.Add(autoRow, 0, 0);

            autoRow.Controls.Add(CreateInlineLabel("内存占用达到"), 0, 0);
            _numThreshold = CreateNumber(85, 1, 99, 54);
            _numThreshold.ValueChanged += OnThresholdChanged;
            autoRow.Controls.Add(_numThreshold, 1, 0);
            autoRow.Controls.Add(CreateInlineLabel("% 时自动清理，冷却"), 2, 0);
            _numCooldown = CreateNumber(120, 5, 3600, 62);
            _numCooldown.ValueChanged += OnCooldownChanged;
            autoRow.Controls.Add(_numCooldown, 3, 0);
            autoRow.Controls.Add(CreateInlineLabel("秒"), 4, 0);
            autoRow.Controls.Add(CreateInlineLabel("启用自动优化"), 6, 0);

            _switchAuto = new SwitchToggle();
            _switchAuto.Anchor = AnchorStyles.Left;
            _switchAuto.Margin = new Padding(0);
            _switchAuto.CheckedChanged += OnAutoSwitchChanged;
            autoRow.Controls.Add(_switchAuto, 7, 0);

            FlowLayoutPanel switches = new FlowLayoutPanel();
            switches.Dock = DockStyle.Fill;
            switches.FlowDirection = FlowDirection.LeftToRight;
            switches.WrapContents = true;
            switches.Margin = new Padding(0, 2, 0, 0);
            autoLayout.Controls.Add(switches, 0, 1);

            _chkStartup = CreateCheck("开机自动启动", "登录 Windows 后自动在托盘运行（写入 HKCU 启动项）。", new EventHandler(OnStartupCheckedChanged));
            _chkStartup.Margin = new Padding(0, 4, 22, 0);
            _chkStartMin = CreateCheck("启动时最小化到托盘", "开机启动时不弹出主界面。", new EventHandler(OnStartMinCheckedChanged));
            _chkStartMin.Margin = new Padding(0, 4, 22, 0);
            _chkCloseTray = CreateCheck("关闭时最小化到托盘", "点击关闭按钮时不退出，自动优化继续在后台运行。", new EventHandler(OnCloseTrayCheckedChanged));
            _chkCloseTray.Margin = new Padding(0, 4, 0, 0);
            switches.Controls.Add(_chkStartup);
            switches.Controls.Add(_chkStartMin);
            switches.Controls.Add(_chkCloseTray);

            // 运行日志
            _cardLog = new CardPanel();
            _cardLog.Title = "运行日志";
            _cardLog.Padding = new Padding(18, 46, 18, 12);
            _cardLog.Dock = DockStyle.Fill;
            grid.Controls.Add(_cardLog, 0, 3);

            _btnClearLog = new FlatButton();
            _btnClearLog.SetCompact();
            _btnClearLog.Primary = false;
            _btnClearLog.Text = "清空";
            _btnClearLog.Click += delegate
            {
                if (_logView != null) _logView.Clear();
                AppendLogToBox("日志已清空。", LogLevel.Info);
            };
            _cardLog.AddHeaderControl(_btnClearLog);

            _logView = new LogView();
            _logView.Dock = DockStyle.Fill;
            _logView.Margin = new Padding(0);
            _cardLog.Controls.Add(_logView);

            // 底部状态栏
            TableLayoutPanel status = new TableLayoutPanel();
            status.Dock = DockStyle.Fill;
            status.ColumnCount = 2;
            status.RowCount = 1;
            status.Margin = new Padding(0);
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156f));
            root.Controls.Add(status, 0, 1);

            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Font = Theme.Caps;
            _lblStatus.ForeColor = Theme.TextMuted;
            _lblStatus.AutoEllipsis = true;
            _lblStatus.Margin = new Padding(0);
            status.Controls.Add(_lblStatus, 0, 0);

            _btnElevate = new FlatButton();
            _btnElevate.SetCompact();
            _btnElevate.Primary = false;
            _btnElevate.Text = "以管理员身份重启";
            _btnElevate.Size = new Size(150, 24);
            _btnElevate.Anchor = AnchorStyles.Right;
            _btnElevate.Margin = new Padding(0, 3, 0, 3);
            _btnElevate.Click += OnElevateClick;
            status.Controls.Add(_btnElevate, 1, 0);

            if (Native.IsAdministrator())
                _btnElevate.Visible = false;

            ResumeLayout(true);
        }

        private CheckBox CreateCheck(string text, string tooltip, EventHandler changed)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = true;
            checkBox.Font = Theme.Body;
            checkBox.ForeColor = Theme.Text;
            checkBox.BackColor = Theme.Card;
            checkBox.Cursor = Cursors.Hand;
            checkBox.Anchor = AnchorStyles.Left;
            checkBox.Margin = new Padding(0);
            if (changed != null) checkBox.CheckedChanged += changed;
            if (_tips != null && !string.IsNullOrEmpty(tooltip)) _tips.SetToolTip(checkBox, tooltip);
            return checkBox;
        }

        private Label CreateInlineLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = Theme.BodySmall;
            label.ForeColor = Theme.TextDim;
            label.BackColor = Theme.Card;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 0, 6, 0);
            return label;
        }

        private NumericUpDown CreateNumber(int value, int minimum, int maximum, int width)
        {
            NumericUpDown number = new NumericUpDown();
            number.Minimum = minimum;
            number.Maximum = maximum;
            number.Value = ClampUtil.Int(value, minimum, maximum);
            number.Font = Theme.BodySmall;
            number.ForeColor = Theme.Text;
            number.BackColor = Theme.Card;
            number.BorderStyle = BorderStyle.FixedSingle;
            number.TextAlign = HorizontalAlignment.Center;
            number.Width = width;
            number.Anchor = AnchorStyles.Left;
            number.Margin = new Padding(0, 0, 6, 0);
            return number;
        }

        private void BuildTray()
        {
            if (_previewMode) return;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Font = Theme.BodySmall;

            ToolStripMenuItem showItem = new ToolStripMenuItem("打开主界面");
            showItem.Click += delegate { ShowFromTray(); };
            _trayMenu.Items.Add(showItem);

            ToolStripMenuItem optimizeItem = new ToolStripMenuItem("立即优化");
            optimizeItem.Click += delegate { StartOptimize(false); };
            _trayMenu.Items.Add(optimizeItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayAutoItem = new ToolStripMenuItem("自动优化");
            _trayAutoItem.Checked = _settings.AutoEnabled;
            _trayAutoItem.Click += delegate { ApplyAutoState(!_settings.AutoEnabled, true); };
            _trayMenu.Items.Add(_trayAutoItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.Icon = Icon;
            _tray.Text = "内存优化器";
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
            _tray.Visible = true;
        }

        private void WriteStartupLog()
        {
            AppendLogToBox("内存优化器 v" + Application.ProductVersion + " 已启动。", LogLevel.Info);
            if (Native.IsAdministrator())
                AppendLogToBox("当前为管理员权限，可以执行全部清理项目。", LogLevel.Success);
            else
                AppendLogToBox("当前为普通权限，清理待机列表和文件缓存会被系统拒绝。", LogLevel.Warn);

            AppendLogToBox(string.Format("自动优化{0}：内存占用达到 {1}% 时清理，两次清理至少间隔 {2} 秒。",
                _settings.AutoEnabled ? "已开启" : "已关闭",
                _settings.ThresholdPercent, _settings.CooldownSeconds), LogLevel.Info);

            if (_settings.ProtectForeground)
                AppendLogToBox("前台程序保护已开启，正在使用的程序不会被清理。", LogLevel.Info);
            if (_settings.GetExcludedNames().Count > 0)
                AppendLogToBox("排除名单：" + DescribeExclusions(), LogLevel.Info);
        }

        // ────────────────────────────────────────────────────────────────
        // 配置 / 状态
        // ────────────────────────────────────────────────────────────────
        private void ApplySettings()
        {
            _loading = true;
            try
            {
                _chkTrim.Checked = _settings.TrimWorkingSets;
                _chkStandby.Checked = _settings.PurgeStandby;
                _chkCache.Checked = _settings.ClearFileCache;

                _numThreshold.Value = ClampUtil.Dec(_settings.ThresholdPercent, _numThreshold.Minimum, _numThreshold.Maximum);
                _numCooldown.Value = ClampUtil.Dec(_settings.CooldownSeconds, _numCooldown.Minimum, _numCooldown.Maximum);

                _chkStartup.Checked = Settings.IsAutoStartEnabled();
                _settings.StartWithWindows = _chkStartup.Checked;
                _chkStartMin.Checked = _settings.StartMinimized;
                _chkCloseTray.Checked = _settings.CloseToTray;

                _switchAuto.Checked = _settings.AutoEnabled;
                if (_trayAutoItem != null) _trayAutoItem.Checked = _settings.AutoEnabled;
            }
            finally
            {
                _loading = false;
            }
            UpdateAutoHeader();
        }

        private void UpdateAutoHeader()
        {
            if (_settings.AutoEnabled)
            {
                _cardAuto.HeaderNote = string.Format("已启用 · 达到 {0}% 自动清理", _settings.ThresholdPercent);
                _cardAuto.HeaderNoteColor = Theme.Success;
            }
            else
            {
                _cardAuto.HeaderNote = "已关闭";
                _cardAuto.HeaderNoteColor = Theme.TextMuted;
            }
        }

        private void ApplyAutoState(bool enabled, bool writeLog)
        {
            _settings.AutoEnabled = enabled;
            _loading = true;
            try
            {
                _switchAuto.Checked = enabled;
                if (_trayAutoItem != null) _trayAutoItem.Checked = enabled;
            }
            finally
            {
                _loading = false;
            }
            _settings.Save();
            UpdateAutoHeader();
            if (writeLog)
                AppendLogToBox(enabled ? "自动优化已开启。" : "自动优化已关闭。", LogLevel.Info);
        }

        private void OnOptionChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.TrimWorkingSets = _chkTrim.Checked;
            _settings.PurgeStandby = _chkStandby.Checked;
            _settings.ClearFileCache = _chkCache.Checked;
            _settings.Save();
        }

        private void OnAutoSwitchChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ApplyAutoState(_switchAuto.Checked, true);
        }

        private void OnThresholdChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.ThresholdPercent = (int)_numThreshold.Value;
            _settings.Save();
            UpdateAutoHeader();
        }

        private void OnCooldownChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.CooldownSeconds = (int)_numCooldown.Value;
            _settings.Save();
        }

        private void OnStartupCheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            bool enabled = _chkStartup.Checked;
            string error;
            if (!Settings.SetAutoStart(enabled, out error))
            {
                AppendLogToBox("设置开机自启失败：" + error, LogLevel.Error);
                _loading = true;
                try { _chkStartup.Checked = !enabled; }
                finally { _loading = false; }
                return;
            }
            _settings.StartWithWindows = enabled;
            _settings.Save();
            AppendLogToBox(enabled ? "已开启开机自动启动。" : "已关闭开机自动启动。", LogLevel.Info);
        }

        private void OnStartMinCheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.StartMinimized = _chkStartMin.Checked;
            _settings.Save();
        }

        private void OnCloseTrayCheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _settings.CloseToTray = _chkCloseTray.Checked;
            _settings.Save();
        }

        private void OnExcludeClick(object sender, EventArgs e)
        {
            string current = _settings.ExcludedProcesses ?? string.Empty;
            using (ExcludeForm form = new ExcludeForm(current, _settings.ProtectForeground))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;

                string text = form.Names ?? string.Empty;
                if (text.Length > 2000) text = text.Substring(0, 2000);
                _settings.ExcludedProcesses = Settings.NormalizeExclusions(text);
                _settings.ProtectForeground = form.ProtectForeground;
                _settings.Save();
                AppendLogToBox("排除名单已更新：" + DescribeExclusions(), LogLevel.Info);
            }
        }

        private string DescribeExclusions()
        {
            List<string> names = _settings.GetExcludedNames();
            if (names.Count == 0)
                return "未设置额外排除项（系统关键进程仍会自动跳过）。";
            return string.Join("、", names.ToArray());
        }

        private void OnElevateClick(object sender, EventArgs e)
        {
            if (Native.IsAdministrator()) return;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Application.ExecutablePath;
                info.Arguments = "--elevated";
                info.UseShellExecute = true;
                info.Verb = "runas";
                Process.Start(info);
                ExitApplication();
            }
            catch (Exception ex)
            {
                AppendLogToBox("无法以管理员身份重启：" + ex.Message, LogLevel.Error);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // 刷新 / 自动优化
        // ────────────────────────────────────────────────────────────────
        private void OnUiTick(object sender, EventArgs e)
        {
            _tick++;
            MemorySnapshot snapshot = CaptureSnapshot();
            _lastSnapshot = snapshot;
            _gauge.UpdateSnapshot(snapshot, true);
            UpdateStatusText();

            if (_tick % 3 == 0 || _lastSnapshot.WorkingSetSum < 0)
                ScheduleWorkingSetUpdate();

            if (!_previewMode && !_isOptimizing && _settings.AutoEnabled)
                CheckAuto(snapshot);
        }

        /// <summary>采集快照，并沿用上一次统计到的进程工作集合计，避免界面每秒闪回“统计中”。</summary>
        private MemorySnapshot CaptureSnapshot()
        {
            MemorySnapshot snapshot = MemorySnapshot.Capture(false);
            if (_lastSnapshot != null && _lastSnapshot.WorkingSetSum >= 0)
                snapshot.WorkingSetSum = _lastSnapshot.WorkingSetSum;
            return snapshot;
        }

        private void RefreshDashboard()
        {
            MemorySnapshot snapshot = CaptureSnapshot();
            _lastSnapshot = snapshot;
            _gauge.UpdateSnapshot(snapshot, true);
            UpdateStatusText();
            if (IsHandleCreated)
                ScheduleWorkingSetUpdate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ScheduleWorkingSetUpdate();
        }

        private void UpdateStatusText()
        {
            string admin = Native.IsAdministrator() ? "管理员权限" : "普通权限";
            string auto = _settings.AutoEnabled ? "自动优化运行中" : "自动优化已关闭";
            _lblStatus.Text = string.Format("v{0} · {1} · {2}", Application.ProductVersion, admin, auto);
            if (_tips != null)
                _tips.SetToolTip(_lblStatus, "配置文件：" + Settings.FilePath);
        }

        private void ScheduleWorkingSetUpdate()
        {
            if (_wsBusy || !IsHandleCreated) return;
            _wsBusy = true;
            System.Threading.Tasks.Task.Run(delegate
            {
                long sum = 0;
                try { sum = MemorySnapshot.SumWorkingSets(); }
                catch { }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _wsBusy = false;
                        if (_lastSnapshot != null)
                        {
                            _lastSnapshot.WorkingSetSum = sum;
                            _gauge.UpdateSnapshot(_lastSnapshot, false);
                        }
                    }));
                }
                catch
                {
                    _wsBusy = false;
                }
            });
        }

        private void CheckAuto(MemorySnapshot snapshot)
        {
            double percent = snapshot.UsedPercent;
            if (percent < _settings.ThresholdPercent) return;

            if (_lastOptimizeUtc != DateTime.MinValue &&
                (DateTime.UtcNow - _lastOptimizeUtc).TotalSeconds < _settings.CooldownSeconds)
                return;

            AppendLogToBox(string.Format("内存占用 {0:0.0}% 已达到 {1}% 阈值，开始自动优化。",
                percent, _settings.ThresholdPercent), LogLevel.Info);
            StartOptimize(true);
        }

        private void StartOptimize(bool automatic)
        {
            if (_isOptimizing) return;

            OptimizeOptions options = new OptimizeOptions();
            options.TrimWorkingSets = _settings.TrimWorkingSets;
            options.PurgeStandby = _settings.PurgeStandby;
            options.ClearFileCache = _settings.ClearFileCache;
            options.ExcludedProcesses = _settings.GetExcludedNames();
            options.ProtectForeground = _settings.ProtectForeground;

            if (!options.TrimWorkingSets && !options.PurgeStandby && !options.ClearFileCache)
            {
                AppendLogToBox("没有勾选任何优化项目，请至少选择一项。", LogLevel.Warn);
                return;
            }

            _isOptimizing = true;
            _lastOptimizeUtc = DateTime.UtcNow;
            _btnOptimize.Enabled = false;
            _btnOptimize.Text = "正在优化…";
            _switchAuto.Enabled = false;

            System.Threading.Tasks.Task.Run(delegate
            {
                OptimizeResult result = null;
                try
                {
                    result = Optimizer.Run(options, Log);
                }
                catch (Exception ex)
                {
                    Log("优化过程出现异常：" + ex.Message, LogLevel.Error);
                }

                try
                {
                    BeginInvoke(new Action(delegate { FinishOptimize(result, automatic); }));
                }
                catch { }
            });
        }

        private void FinishOptimize(OptimizeResult result, bool automatic)
        {
            _isOptimizing = false;
            _btnOptimize.Enabled = true;
            _btnOptimize.Text = "一键优化";
            _switchAuto.Enabled = true;

            if (result != null)
            {
                _totalFreed += result.FreedBytes;
                _lblLastRun.ForeColor = Theme.TextMuted;
                _lblLastRun.Text = string.Format("上次优化 {0} · 释放约 {1} · 累计 {2}",
                    DateTime.Now.ToString("HH:mm:ss"), Fmt.Bytes(result.FreedBytes), Fmt.Bytes(_totalFreed));

                _cardOverview.HeaderNote = "上次释放约 " + Fmt.Bytes(result.FreedBytes);
                _cardOverview.HeaderNoteColor = result.FreedBytes > 0 ? Theme.Success : Theme.TextMuted;

                if (automatic && !Visible && _tray != null)
                {
                    _tray.ShowBalloonTip(3000, "自动优化完成",
                        string.Format("本次释放约 {0}，当前可用 {1}。",
                            Fmt.Bytes(result.FreedBytes), Fmt.Bytes(result.AvailableAfter)),
                        ToolTipIcon.Info);
                }
            }
            else
            {
                _lblLastRun.ForeColor = Theme.Danger;
                _lblLastRun.Text = "上次优化未完成";
            }

            RefreshDashboard();
        }

        internal void TriggerOptimizeForPreview()
        {
            StartOptimize(false);
        }

        // ────────────────────────────────────────────────────────────────
        // 日志
        // ────────────────────────────────────────────────────────────────
        private void Log(string message, LogLevel level)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string, LogLevel>(Log), message, level);
                    return;
                }
                AppendLogToBox(message, level);
            }
            catch { }
        }

        private void AppendLogToBox(string message, LogLevel level)
        {
            if (_logView != null && !_logView.IsDisposed)
                _logView.Append(message, level);
            FileLog.Write(message, level);
        }

        // ────────────────────────────────────────────────────────────────
        // 窗口 / 托盘
        // ────────────────────────────────────────────────────────────────
        protected override void SetVisibleCore(bool value)
        {
            if (value && _startHidden && !_allowVisible)
            {
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                StartOptimize(false);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;
            if (_tray != null && !_trayBalloonShown)
            {
                _trayBalloonShown = true;
                _tray.ShowBalloonTip(2500, "内存优化器",
                    "已最小化到托盘，自动优化仍在后台运行。双击图标可重新打开。",
                    ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            _allowVisible = true;
            ShowInTaskbar = true;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ExitApplication()
        {
            _exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && !_previewMode && e.CloseReason == CloseReason.UserClosing && _settings.CloseToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            _uiTimer.Stop();
            try { _settings.Save(); }
            catch { }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_trayMenu != null)
            {
                _trayMenu.Dispose();
                _trayMenu = null;
            }
            if (_tips != null)
            {
                _tips.Dispose();
            }

            base.OnFormClosing(e);
        }
    }
}
