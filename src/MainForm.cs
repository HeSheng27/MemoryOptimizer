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
        private FlatButton _btnLanguage;
        private NumericUpDown _numThreshold;
        private NumericUpDown _numCooldown;
        private SwitchToggle _switchAuto;
        private Label _lblLastRun;
        private Label _lblStatus;
        private Label _lblAutoPrefix;
        private Label _lblAutoSuffix;
        private Label _lblAutoSeconds;
        private Label _lblAutoSwitch;
        private LogView _logView;
        private TableLayoutPanel _statusPanel;

        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayShowItem;
        private ToolStripMenuItem _trayOptimizeItem;
        private ToolStripMenuItem _trayAutoItem;
        private ToolStripMenuItem _trayExitItem;

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

        private bool _hasLastRun;
        private bool _lastRunFailed;
        private long _lastFreed;
        private DateTime _lastRunTime;

        public MainForm(Settings settings, bool startHidden, bool previewMode)
        {
            _settings = settings ?? new Settings();
            _startHidden = startHidden;
            _previewMode = previewMode;

            Text = Loc.T("app.title");
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
            ApplyLanguage();
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
            _cardOverview.Title = Loc.T("card.overview");
            _cardOverview.HeaderNote = Loc.T("note.live");
            _cardOverview.Dock = DockStyle.Fill;
            _gauge = new MemoryGauge();
            _gauge.Dock = DockStyle.Fill;
            _cardOverview.Controls.Add(_gauge);
            grid.Controls.Add(_cardOverview, 0, 0);

            // 优化项目
            _cardOptions = new CardPanel();
            _cardOptions.Title = Loc.T("card.options");
            _cardOptions.HeaderNote = Loc.T("note.optionsHint");
            _cardOptions.Padding = new Padding(18, 46, 18, 12);
            _cardOptions.Dock = DockStyle.Fill;
            grid.Controls.Add(_cardOptions, 0, 1);

            _btnExclude = new FlatButton();
            _btnExclude.SetCompact();
            _btnExclude.Primary = false;
            _btnExclude.Text = Loc.T("btn.exclude");
            _btnExclude.Size = new Size(92, 26);
            _btnExclude.Click += OnExcludeClick;
            _cardOptions.AddHeaderControl(_btnExclude);

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
            _chkTrim = CreateCheck(Loc.T("opt.trim"), optionChanged);
            _chkTrim.Margin = new Padding(0, 5, 36, 0);
            _chkStandby = CreateCheck(Loc.T("opt.standby"), optionChanged);
            _chkStandby.Margin = new Padding(0, 5, 36, 0);
            _chkCache = CreateCheck(Loc.T("opt.cache"), optionChanged);
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
            _btnOptimize.Text = Loc.T("btn.optimize");
            _btnOptimize.Dock = DockStyle.Fill;
            _btnOptimize.Margin = new Padding(0, 4, 12, 2);
            _btnOptimize.Click += delegate { StartOptimize(false); };
            actions.Controls.Add(_btnOptimize, 0, 0);

            _lblLastRun = new Label();
            _lblLastRun.Text = Loc.T("last.none");
            _lblLastRun.Dock = DockStyle.Fill;
            _lblLastRun.TextAlign = ContentAlignment.MiddleLeft;
            _lblLastRun.Font = Theme.Caps;
            _lblLastRun.ForeColor = Theme.TextMuted;
            _lblLastRun.AutoEllipsis = true;
            _lblLastRun.Margin = new Padding(0);
            actions.Controls.Add(_lblLastRun, 1, 0);

            // 自动优化
            _cardAuto = new CardPanel();
            _cardAuto.Title = Loc.T("card.auto");
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

            _lblAutoPrefix = CreateInlineLabel(Loc.T("auto.prefix"));
            autoRow.Controls.Add(_lblAutoPrefix, 0, 0);
            _numThreshold = CreateNumber(85, 1, 99, 54);
            _numThreshold.ValueChanged += OnThresholdChanged;
            autoRow.Controls.Add(_numThreshold, 1, 0);
            _lblAutoSuffix = CreateInlineLabel(Loc.T("auto.suffix"));
            autoRow.Controls.Add(_lblAutoSuffix, 2, 0);
            _numCooldown = CreateNumber(120, 5, 3600, 62);
            _numCooldown.ValueChanged += OnCooldownChanged;
            autoRow.Controls.Add(_numCooldown, 3, 0);
            _lblAutoSeconds = CreateInlineLabel(Loc.T("auto.seconds"));
            autoRow.Controls.Add(_lblAutoSeconds, 4, 0);
            _lblAutoSwitch = CreateInlineLabel(Loc.T("auto.switch"));
            autoRow.Controls.Add(_lblAutoSwitch, 6, 0);

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

            _chkStartup = CreateCheck(Loc.T("chk.startup"), new EventHandler(OnStartupCheckedChanged));
            _chkStartup.Margin = new Padding(0, 4, 22, 0);
            _chkStartMin = CreateCheck(Loc.T("chk.startMin"), new EventHandler(OnStartMinCheckedChanged));
            _chkStartMin.Margin = new Padding(0, 4, 22, 0);
            _chkCloseTray = CreateCheck(Loc.T("chk.closeTray"), new EventHandler(OnCloseTrayCheckedChanged));
            _chkCloseTray.Margin = new Padding(0, 4, 0, 0);
            switches.Controls.Add(_chkStartup);
            switches.Controls.Add(_chkStartMin);
            switches.Controls.Add(_chkCloseTray);

            // 运行日志
            _cardLog = new CardPanel();
            _cardLog.Title = Loc.T("card.log");
            _cardLog.Padding = new Padding(18, 46, 18, 12);
            _cardLog.Dock = DockStyle.Fill;
            grid.Controls.Add(_cardLog, 0, 3);

            _btnClearLog = new FlatButton();
            _btnClearLog.SetCompact();
            _btnClearLog.Primary = false;
            _btnClearLog.Text = Loc.T("btn.clear");
            _btnClearLog.Click += delegate
            {
                if (_logView != null) _logView.Clear();
                AppendLogToBox(Loc.T("log.cleared"), LogLevel.Info);
            };
            _cardLog.AddHeaderControl(_btnClearLog);

            _logView = new LogView();
            _logView.Dock = DockStyle.Fill;
            _logView.Margin = new Padding(0);
            _cardLog.Controls.Add(_logView);

            // 底部状态栏
            TableLayoutPanel status = new TableLayoutPanel();
            status.Dock = DockStyle.Fill;
            status.ColumnCount = 3;
            status.RowCount = 1;
            status.Margin = new Padding(0);
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156f));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176f));
            root.Controls.Add(status, 0, 1);
            _statusPanel = status;

            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Font = Theme.Caps;
            _lblStatus.ForeColor = Theme.TextMuted;
            _lblStatus.AutoEllipsis = true;
            _lblStatus.Margin = new Padding(0);
            status.Controls.Add(_lblStatus, 0, 0);

            _btnLanguage = new FlatButton();
            _btnLanguage.SetCompact();
            _btnLanguage.Primary = false;
            _btnLanguage.Text = Loc.NativeName(Loc.Current);
            _btnLanguage.Size = new Size(148, 24);
            _btnLanguage.Anchor = AnchorStyles.Right;
            _btnLanguage.Margin = new Padding(0, 3, 6, 3);
            _btnLanguage.Click += delegate { ChooseLanguage(); };
            status.Controls.Add(_btnLanguage, 1, 0);

            _btnElevate = new FlatButton();
            _btnElevate.SetCompact();
            _btnElevate.Primary = false;
            _btnElevate.Text = Loc.T("btn.elevate");
            _btnElevate.Size = new Size(168, 24);
            _btnElevate.Anchor = AnchorStyles.Right;
            _btnElevate.Margin = new Padding(0, 3, 0, 3);
            _btnElevate.Click += OnElevateClick;
            status.Controls.Add(_btnElevate, 2, 0);

            if (Native.IsAdministrator())
                _btnElevate.Visible = false;

            ResumeLayout(true);
        }

        private void ChooseLanguage()
        {
            using (LanguageForm form = new LanguageForm(_settings.Language))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                ChangeLanguage(form.SelectedCode);
            }
        }

        private void ChangeLanguage(string code)
        {
            _settings.Language = code;
            Loc.Initialize(code);
            _settings.Save();
            ApplyLanguage();
            AppendLogToBox(Loc.T("log.langChanged"), LogLevel.Info);
        }

        private void UpdateLanguageButton()
        {
            if (_btnLanguage == null) return;
            _btnLanguage.Text = Loc.NativeName(Loc.Current);
        }

        /// <summary>状态栏按钮按文字长度调整宽度，避免长语种被裁切。</summary>
        private void AdjustStatusButtons()
        {
            if (_statusPanel == null || _btnLanguage == null || _btnElevate == null) return;

            int languageWidth = MeasureButtonWidth(_btnLanguage, 96, 230);
            _btnLanguage.Size = new Size(languageWidth, 24);
            _statusPanel.ColumnStyles[1].Width = languageWidth + 6;

            int elevateWidth = MeasureButtonWidth(_btnElevate, 130, 300);
            _btnElevate.Size = new Size(elevateWidth, 24);
            _statusPanel.ColumnStyles[2].Width = elevateWidth + 4;
        }

        private static int MeasureButtonWidth(Control control, int minimum, int maximum)
        {
            Size size = TextRenderer.MeasureText(control.Text, control.Font, new Size(4096, 4096),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            int width = size.Width + 24;
            if (width < minimum) width = minimum;
            if (width > maximum) width = maximum;
            return width;
        }

        private CheckBox CreateCheck(string text, EventHandler changed)
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

            _trayShowItem = new ToolStripMenuItem(Loc.T("tray.show"));
            _trayShowItem.Click += delegate { ShowFromTray(); };
            _trayMenu.Items.Add(_trayShowItem);

            _trayOptimizeItem = new ToolStripMenuItem(Loc.T("tray.optimize"));
            _trayOptimizeItem.Click += delegate { StartOptimize(false); };
            _trayMenu.Items.Add(_trayOptimizeItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayAutoItem = new ToolStripMenuItem(Loc.T("tray.auto"));
            _trayAutoItem.Checked = _settings.AutoEnabled;
            _trayAutoItem.Click += delegate { ApplyAutoState(!_settings.AutoEnabled, true); };
            _trayMenu.Items.Add(_trayAutoItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayExitItem = new ToolStripMenuItem(Loc.T("tray.exit"));
            _trayExitItem.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(_trayExitItem);

            _tray = new NotifyIcon();
            _tray.Icon = Icon;
            _tray.Text = Loc.T("tray.tooltip");
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
            _tray.Visible = true;
        }

        private void WriteStartupLog()
        {
            AppendLogToBox(Loc.F("log.started", Application.ProductVersion), LogLevel.Info);
            AppendLogToBox(Native.IsAdministrator() ? Loc.T("log.admin") : Loc.T("log.user"),
                Native.IsAdministrator() ? LogLevel.Success : LogLevel.Warn);

            if (_settings.AutoEnabled)
                AppendLogToBox(Loc.F("log.autoOn", _settings.ThresholdPercent, _settings.CooldownSeconds), LogLevel.Info);
            else
                AppendLogToBox(Loc.T("log.autoOff"), LogLevel.Info);

            if (_settings.ProtectForeground)
                AppendLogToBox(Loc.T("log.foreground"), LogLevel.Info);
            if (_settings.GetExcludedNames().Count > 0)
                AppendLogToBox(Loc.F("log.exclusions", DescribeExclusions()), LogLevel.Info);
        }

        // ────────────────────────────────────────────────────────────────
        // 配置 / 语言
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

        /// <summary>切换语言后刷新所有文字（不重建窗口，日志保留原语言的历史记录）。</summary>
        private void ApplyLanguage()
        {
            Text = Loc.T("app.title");
            _cardOverview.Title = Loc.T("card.overview");
            _cardOptions.Title = Loc.T("card.options");
            _cardOptions.HeaderNote = Loc.T("note.optionsHint");
            _cardAuto.Title = Loc.T("card.auto");
            _cardLog.Title = Loc.T("card.log");

            _chkTrim.Text = Loc.T("opt.trim");
            _chkStandby.Text = Loc.T("opt.standby");
            _chkCache.Text = Loc.T("opt.cache");
            _btnOptimize.Text = _isOptimizing ? Loc.T("btn.optimizing") : Loc.T("btn.optimize");
            _btnExclude.Text = Loc.T("btn.exclude");
            _btnClearLog.Text = Loc.T("btn.clear");
            _btnElevate.Text = Loc.T("btn.elevate");
            UpdateLanguageButton();

            _lblAutoPrefix.Text = Loc.T("auto.prefix");
            _lblAutoSuffix.Text = Loc.T("auto.suffix");
            _lblAutoSeconds.Text = Loc.T("auto.seconds");
            _lblAutoSwitch.Text = Loc.T("auto.switch");

            _chkStartup.Text = Loc.T("chk.startup");
            _chkStartMin.Text = Loc.T("chk.startMin");
            _chkCloseTray.Text = Loc.T("chk.closeTray");

            ApplyTooltips();
            UpdateLastRunLabel();
            UpdateAutoHeader();
            UpdateOverviewNote();
            UpdateStatusText();
            UpdateTrayTexts();
            if (_logView != null) _logView.ApplyLanguage();
            AdjustStatusButtons();
            if (_gauge != null) _gauge.Invalidate();
            Invalidate(true);
        }

        private void ApplyTooltips()
        {
            if (_tips == null) return;
            _tips.SetToolTip(_chkTrim, Loc.T("opt.trimTip"));
            _tips.SetToolTip(_chkStandby, Loc.T("opt.standbyTip"));
            _tips.SetToolTip(_chkCache, Loc.T("opt.cacheTip"));
            _tips.SetToolTip(_btnOptimize, Loc.T("btn.optimizeTip"));
            _tips.SetToolTip(_btnExclude, Loc.T("btn.excludeTip"));
            _tips.SetToolTip(_chkStartup, Loc.T("chk.startupTip"));
            _tips.SetToolTip(_chkStartMin, Loc.T("chk.startMinTip"));
            _tips.SetToolTip(_chkCloseTray, Loc.T("chk.closeTrayTip"));
            _tips.SetToolTip(_btnLanguage, Loc.T("status.langTip"));
            _tips.SetToolTip(_lblStatus, Loc.F("status.config", Settings.FilePath));
        }

        private void UpdateTrayTexts()
        {
            if (_tray == null) return;
            _tray.Text = Loc.T("tray.tooltip");
            if (_trayShowItem != null) _trayShowItem.Text = Loc.T("tray.show");
            if (_trayOptimizeItem != null) _trayOptimizeItem.Text = Loc.T("tray.optimize");
            if (_trayAutoItem != null) _trayAutoItem.Text = Loc.T("tray.auto");
            if (_trayExitItem != null) _trayExitItem.Text = Loc.T("tray.exit");
        }

        private void UpdateLastRunLabel()
        {
            if (_lblLastRun == null) return;

            if (_lastRunFailed)
            {
                _lblLastRun.Text = Loc.T("last.failed");
                _lblLastRun.ForeColor = Theme.Danger;
                return;
            }

            if (_hasLastRun)
            {
                _lblLastRun.Text = Loc.F("last.result",
                    _lastRunTime.ToString("HH:mm:ss"), Fmt.Bytes(_lastFreed), Fmt.Bytes(_totalFreed));
                _lblLastRun.ForeColor = Theme.TextMuted;
            }
            else
            {
                _lblLastRun.Text = Loc.T("last.none");
                _lblLastRun.ForeColor = Theme.TextMuted;
            }
        }

        private void UpdateOverviewNote()
        {
            if (_cardOverview == null) return;
            if (_hasLastRun && !_lastRunFailed)
            {
                _cardOverview.HeaderNote = Loc.F("note.lastFreed", Fmt.Bytes(_lastFreed));
                _cardOverview.HeaderNoteColor = _lastFreed > 0 ? Theme.Success : Theme.TextMuted;
            }
            else
            {
                _cardOverview.HeaderNote = Loc.T("note.live");
                _cardOverview.HeaderNoteColor = Theme.TextMuted;
            }
        }

        private void UpdateAutoHeader()
        {
            if (_cardAuto == null) return;
            if (_settings.AutoEnabled)
            {
                _cardAuto.HeaderNote = Loc.F("note.autoOn", _settings.ThresholdPercent);
                _cardAuto.HeaderNoteColor = Theme.Success;
            }
            else
            {
                _cardAuto.HeaderNote = Loc.T("note.autoOff");
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
                AppendLogToBox(Loc.T(enabled ? "log.autoEnabled" : "log.autoDisabled"), LogLevel.Info);
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
                AppendLogToBox(Loc.F("log.startupFailed", error), LogLevel.Error);
                _loading = true;
                try { _chkStartup.Checked = !enabled; }
                finally { _loading = false; }
                return;
            }
            _settings.StartWithWindows = enabled;
            _settings.Save();
            AppendLogToBox(Loc.T(enabled ? "log.startupOn" : "log.startupOff"), LogLevel.Info);
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

        private string DescribeExclusions()
        {
            List<string> names = _settings.GetExcludedNames();
            if (names.Count == 0)
                return Loc.T("log.exclusionsNone");
            return string.Join("、", names.ToArray());
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
                AppendLogToBox(Loc.F("log.exclusionsUpdated", DescribeExclusions()), LogLevel.Info);
            }
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
                AppendLogToBox(Loc.F("log.elevateFailed", ex.Message), LogLevel.Error);
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
            string admin = Native.IsAdministrator() ? Loc.T("status.admin") : Loc.T("status.user");
            string auto = _settings.AutoEnabled ? Loc.T("status.autoOn") : Loc.T("status.autoOff");
            _lblStatus.Text = Loc.F("status.text", Application.ProductVersion, admin, auto);
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

            AppendLogToBox(Loc.F("log.autoTrigger", percent, _settings.ThresholdPercent), LogLevel.Info);
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
                AppendLogToBox(Loc.T("log.noOptions"), LogLevel.Warn);
                return;
            }

            _isOptimizing = true;
            _lastOptimizeUtc = DateTime.UtcNow;
            _btnOptimize.Enabled = false;
            _btnOptimize.Text = Loc.T("btn.optimizing");
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
                    Log(Loc.F("log.exception", ex.Message), LogLevel.Error);
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
            _btnOptimize.Text = Loc.T("btn.optimize");
            _switchAuto.Enabled = true;

            if (result != null)
            {
                _totalFreed += result.FreedBytes;
                _lastFreed = result.FreedBytes;
                _lastRunTime = DateTime.Now;
                _hasLastRun = true;
                _lastRunFailed = false;
                UpdateLastRunLabel();
                UpdateOverviewNote();

                if (automatic && !Visible && _tray != null)
                {
                    _tray.ShowBalloonTip(3000, Loc.T("tray.doneTitle"),
                        Loc.F("tray.doneText", Fmt.Bytes(result.FreedBytes), Fmt.Bytes(result.AvailableAfter)),
                        ToolTipIcon.Info);
                }
            }
            else
            {
                _lastRunFailed = true;
                _hasLastRun = true;
                UpdateLastRunLabel();
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
                _tray.ShowBalloonTip(2500, Loc.T("tray.hideTitle"), Loc.T("tray.hideText"), ToolTipIcon.Info);
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
