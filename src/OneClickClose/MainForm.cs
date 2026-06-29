using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OneClickClose.Core;

namespace OneClickClose
{
    public sealed class MainForm : Form
    {
        private readonly string configPath;
        private readonly UserPreferencesStore preferences;
        private readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private ClosePlan currentPlan;
        private Label candidateCount;
        private Label gracefulCount;
        private Label forceCount;
        private Label protectedCount;
        private Label lastRefreshLabel;
        private ProcessGridView candidateGrid;
        private ProcessGridView overviewCandidateGrid;
        private ProcessGridView protectedGrid;
        private RichTextBox logBox;
        private Label logEmptyLabel;
        private ModernProgressBar progressBar;
        private ModernButton refreshButton;
        private ModernButton previewButton;
        private ModernButton closeButton;
        private ContextMenuStrip candidateMenu;
        private CancellationTokenSource cleanupCts;
        private SidebarPanel sidebar;
        private Panel contentArea;
        private Panel pageOverview;
        private Panel pageCandidate;
        private Panel pageProtected;
        private Panel pageLog;
        private Panel pageConfig;
        private Panel pageLearning;
        private string currentPage = "overview";
        private List<ProcessGroupRow> allCandidateRows = new List<ProcessGroupRow>();
        private List<ProcessGroupRow> allProtectedRows = new List<ProcessGroupRow>();
        private TextBox candidateSearchBox;
        private TextBox protectedSearchBox;
        private ComboBox actionFilter;
        private Label candidatePageCount;
        private Label protectedPageCount;
        private TabBar configTabBar;
        private Panel configTabLists;
        private Panel configTabAdvanced;
        private Panel configTabLearning;
        private ListBox targetList;
        private ListBox protectedList;
        private ListBox forceList;
        private TextBox targetInput;
        private TextBox protectedInput;
        private TextBox forceInput;
        private NumericUpDown waitSecondsInput;
        private NumericUpDown gracefulTimeoutInput;
        private NumericUpDown queryTimeoutInput;
        private Panel suggestionsContainer;
        private Panel learningPageSuggestionsContainer;
        private Label candidateEmptyLabel;
        private Label protectedEmptyLabel;
        private RichTextBox fullLogBox;
        private readonly List<string> logHistory = new List<string>();
        private ToolTip tooltips;
        private Panel onboardingOverlay;
        private bool scanInProgress;
        private bool cleanupInProgress;
        public bool SuppressAutoScan { get; set; }

        // Color aliases — all colors now live in Theme.cs
        private Color background    { get { return Theme.Background; } }
        private Color secondaryPanel { get { return Theme.SecondaryPanel; } }
        private Color card          { get { return Theme.Card; } }
        private Color cardSoft      { get { return Theme.CardSoft; } }
        private Color rowAlt        { get { return Theme.RowAlt; } }
        private Color rowHover      { get { return Theme.RowHover; } }
        private Color border        { get { return Theme.Border; } }
        private Color buttonBorder  { get { return Theme.ButtonBorder; } }
        private Color text          { get { return Theme.Text; } }
        private Color muted         { get { return Theme.Muted; } }
        private Color titleText     { get { return Theme.TitleText; } }
        private Color primary       { get { return Theme.Primary; } }
        private Color primaryHover  { get { return Theme.PrimaryHover; } }
        private Color danger        { get { return Theme.Danger; } }
        private Color dangerHover   { get { return Theme.DangerHover; } }
        private Color protect       { get { return Theme.Protect; } }
        private Color force         { get { return Theme.Force; } }
        private Color purple        { get { return Theme.Purple; } }

        public MainForm(string configPath)
        {
            this.configPath = configPath;
            preferences = UserPreferencesStore.Load();
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Image image in iconCache.Values)
                {
                    image.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            Text = Program.DisplayName;
            AutoScaleMode = AutoScaleMode.Dpi;
            Width = 1240;
            Height = 800;
            MinimumSize = new Size(1080, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = background;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            sidebar = new SidebarPanel();
            sidebar.SidebarBackground = background;
            sidebar.BackColor = background;
            sidebar.AccentColor = primary;
            sidebar.AddNavItem("overview", "概览", "\uE80F");
            sidebar.AddNavItem("candidate", "候选进程", "\uE756");
            sidebar.AddNavItem("protected", "保护列表", "\uE72E");
            sidebar.AddNavItem("log", "运行日志", "\uE7BA");
            sidebar.AddNavItem("config", "配置", "\uE713");
            sidebar.AddNavItem("learning", "学习建议", "\uE9D5");
            sidebar.NavigationRequested += OnNavigationRequested;
            sidebar.Dock = DockStyle.Fill;

            contentArea = new Panel();
            contentArea.Dock = DockStyle.Fill;
            contentArea.BackColor = background;
            contentArea.Padding = new Padding(20, 18, 20, 18);

            TableLayoutPanel rootLayout = new TableLayoutPanel();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.ColumnCount = 2;
            rootLayout.RowCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.Controls.Add(sidebar, 0, 0);
            rootLayout.Controls.Add(contentArea, 1, 0);
            rootLayout.Padding = new Padding(0);
            rootLayout.Margin = new Padding(0);
            Controls.Add(rootLayout);

            pageOverview = CreatePagePanel();
            pageCandidate = CreatePagePanel();
            pageProtected = CreatePagePanel();
            pageLog = CreatePagePanel();
            pageConfig = CreatePagePanel();
            pageLearning = CreatePagePanel();

            contentArea.Controls.Add(pageOverview);
            contentArea.Controls.Add(pageCandidate);
            contentArea.Controls.Add(pageProtected);
            contentArea.Controls.Add(pageLog);
            contentArea.Controls.Add(pageConfig);
            contentArea.Controls.Add(pageLearning);

            InitGrids();

            tooltips = new ToolTip();
            tooltips.BackColor = secondaryPanel;
            tooltips.ForeColor = text;

            BuildOverviewPage();
            BuildCandidatePage();
            BuildProtectedPage();
            BuildConfigPage();
            BuildPlaceholderPages();

            SwitchPage("overview");

            Load += MainFormLoad;
            Shown += MainFormShown;
        }

        private Panel CreatePagePanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Visible = false;
            return panel;
        }

        private void OnNavigationRequested(object sender, string pageId)
        {
            SwitchPage(pageId);
        }

        private void SwitchPage(string pageId)
        {
            currentPage = pageId;
            pageOverview.Visible = pageId == "overview";
            pageCandidate.Visible = pageId == "candidate";
            pageProtected.Visible = pageId == "protected";
            pageLog.Visible = pageId == "log";
            pageConfig.Visible = pageId == "config";
            pageLearning.Visible = pageId == "learning";
            sidebar.SetActiveById(pageId);
        }

        private void BuildOverviewPage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageOverview.Controls.Add(root);

            root.Controls.Add(BuildActionBar(), 0, 0);
            root.Controls.Add(BuildMiniMetrics(), 0, 1);
            root.Controls.Add(BuildOverviewWorkspace(), 0, 2);
        }

        private Control BuildOverviewWorkspace()
        {
            TableLayoutPanel workspace = new TableLayoutPanel();
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Color.Transparent;
            workspace.ColumnCount = 2;
            workspace.RowCount = 1;
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

            overviewCandidateGrid = MakeGrid();
            AddOverviewCandidateColumns(overviewCandidateGrid);
            overviewCandidateGrid.MultiSelect = false;
            overviewCandidateGrid.ContextMenuStrip = candidateMenu;
            overviewCandidateGrid.MouseDown += CandidateGridMouseDown;
            RoundedPanel candidateCard = BuildOverviewCandidateCard();
            workspace.Controls.Add(candidateCard, 0, 0);

            Control logSummary = BuildLogSummary();
            logSummary.Margin = new Padding(12, 0, 0, 0);
            workspace.Controls.Add(logSummary, 1, 0);
            return workspace;
        }

        private RoundedPanel BuildOverviewCandidateCard()
        {
            RoundedPanel cardPanel = MakeCard();
            cardPanel.Padding = new Padding(14, 12, 14, 14);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.RowCount = 2;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            cardPanel.Controls.Add(layout);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label titleLabel = new Label();
            titleLabel.Text = "候选进程";
            titleLabel.ForeColor = titleText;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            titleLabel.Location = new Point(0, 3);
            titleLabel.AutoSize = true;
            header.Controls.Add(titleLabel);

            Label hintLabel = new Label();
            hintLabel.Text = "按风险评分排序，先看这里再清理";
            hintLabel.ForeColor = muted;
            hintLabel.BackColor = Color.Transparent;
            hintLabel.Font = new Font(Font.FontFamily, 8.5F);
            hintLabel.Location = new Point(1, 27);
            hintLabel.AutoSize = true;
            header.Controls.Add(hintLabel);

            ModernButton openListButton = MakeButton("打开完整列表", card, rowHover, secondaryPanel, text, 112, true);
            openListButton.Height = 30;
            openListButton.Width = 112;
            openListButton.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            openListButton.Dock = DockStyle.Right;
            openListButton.Margin = new Padding(0);
            openListButton.Click += delegate { SwitchPage("candidate"); };
            header.Controls.Add(openListButton);

            layout.Controls.Add(header, 0, 0);
            overviewCandidateGrid.Dock = DockStyle.Fill;
            layout.Controls.Add(overviewCandidateGrid, 0, 1);
            return cardPanel;
        }

        private void BuildPlaceholderPages()
        {
            BuildFullLogPage();
            BuildLearningPage();
        }

        private void BuildFullLogPage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageLog.Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label titleLabel = new Label();
            titleLabel.Text = "运行日志";
            titleLabel.ForeColor = titleText;
            titleLabel.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(0, 4);
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "完整记录扫描、清理、保护与错误信息，支持复制和清空。";
            subtitleLabel.ForeColor = muted;
            subtitleLabel.Font = new Font(Font.FontFamily, 9F);
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.AutoSize = true;
            subtitleLabel.Location = new Point(2, 36);
            header.Controls.Add(subtitleLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.Width = 156;
            buttons.BackColor = Color.Transparent;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 8, 0, 0);

            ModernButton clearLogBtn = MakeButton("清空", card, rowHover, secondaryPanel, muted, 66, true);
            clearLogBtn.Height = 32;
            clearLogBtn.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            clearLogBtn.Click += delegate
            {
                logHistory.Clear();
                if (logBox != null) logBox.Clear();
                if (fullLogBox != null) fullLogBox.Clear();
            };
            buttons.Controls.Add(clearLogBtn);

            ModernButton copyLogBtn = MakeButton("复制", card, rowHover, secondaryPanel, muted, 66, true);
            copyLogBtn.Height = 32;
            copyLogBtn.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
            copyLogBtn.Click += delegate
            {
                string textToCopy = fullLogBox != null ? fullLogBox.Text : (logBox != null ? logBox.Text : "");
                if (!string.IsNullOrEmpty(textToCopy)) Clipboard.SetText(textToCopy);
            };
            buttons.Controls.Add(copyLogBtn);

            header.Controls.Add(buttons);
            root.Controls.Add(header, 0, 0);

            RoundedPanel cardPanel = MakeCard();
            cardPanel.Padding = new Padding(10);
            fullLogBox = CreateLogBox();
            cardPanel.Controls.Add(fullLogBox);
            root.Controls.Add(cardPanel, 0, 1);
        }

        private void BuildLearningPage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageLearning.Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label titleLabel = new Label();
            titleLabel.Text = "学习建议";
            titleLabel.ForeColor = titleText;
            titleLabel.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(0, 4);
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "根据你的移除和清理习惯，推荐加入保护名单或强制清理名单。";
            subtitleLabel.ForeColor = muted;
            subtitleLabel.Font = new Font(Font.FontFamily, 9F);
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.AutoSize = true;
            subtitleLabel.Location = new Point(2, 36);
            header.Controls.Add(subtitleLabel);
            root.Controls.Add(header, 0, 0);

            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.BackColor = Color.Transparent;
            scrollHost.AutoScroll = true;

            learningPageSuggestionsContainer = new Panel();
            learningPageSuggestionsContainer.Dock = DockStyle.Top;
            learningPageSuggestionsContainer.BackColor = Color.Transparent;
            learningPageSuggestionsContainer.AutoSize = true;

            scrollHost.Controls.Add(learningPageSuggestionsContainer);
            root.Controls.Add(scrollHost, 0, 1);
            LoadSuggestionsIntoContainer(learningPageSuggestionsContainer);
        }

        private RichTextBox CreateLogBox()
        {
            RichTextBox box = new RichTextBox();
            box.Dock = DockStyle.Fill;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.CardSoft;
            box.ForeColor = text;
            box.Font = new Font("Consolas", 9.5F);
            box.ScrollBars = RichTextBoxScrollBars.Vertical;
            box.Margin = new Padding(0);
            return box;
        }

        private Control BuildActionBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Color.Transparent;

            Label titleLabel = new Label();
            titleLabel.Text = "概览";
            titleLabel.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            titleLabel.ForeColor = titleText;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(0, 6);
            bar.Controls.Add(titleLabel);

            lastRefreshLabel = new Label();
            lastRefreshLabel.Text = "等待扫描";
            lastRefreshLabel.Font = new Font(Font.FontFamily, 9F);
            lastRefreshLabel.ForeColor = muted;
            lastRefreshLabel.BackColor = Color.Transparent;
            lastRefreshLabel.AutoSize = true;
            lastRefreshLabel.Location = new Point(70, 14);
            bar.Controls.Add(lastRefreshLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.Width = 370;
            buttons.BackColor = Color.Transparent;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 8, 0, 0);

            closeButton = MakeButton("⚡ 一键清理", danger, dangerHover, dangerHover, Color.White, 132, false);
            closeButton.Click += CloseButtonClick;
            buttons.Controls.Add(closeButton);

            previewButton = MakeButton("查看详情", card, rowHover, secondaryPanel, text, 104, true);
            previewButton.Click += PreviewButtonClick;
            buttons.Controls.Add(previewButton);

            refreshButton = MakeButton("重新扫描", card, rowHover, secondaryPanel, text, 104, true);
            refreshButton.Click += RefreshButtonClick;
            buttons.Controls.Add(refreshButton);

            bar.Controls.Add(buttons);
            return bar;
        }

        private Control BuildMiniMetrics()
        {
            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Dock = DockStyle.Fill;
            metrics.BackColor = Color.Transparent;
            metrics.ColumnCount = 4;
            metrics.RowCount = 1;
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            candidateCount = new Label();
            gracefulCount = new Label();
            forceCount = new Label();
            protectedCount = new Label();

            metrics.Controls.Add(MiniMetric("待处理", candidateCount, primary, "\uE80F", "扫描发现的后台候选进程总数"), 0, 0);
            metrics.Controls.Add(MiniMetric("温和关闭", gracefulCount, protect, "\uE8EE", "将发送关闭请求（WM_CLOSE）温和退出"), 1, 0);
            metrics.Controls.Add(MiniMetric("强制清理", forceCount, force, "\uE7BA", "仅限强制白名单内的进程才会强制终止"), 2, 0);
            metrics.Controls.Add(MiniMetric("已保护", protectedCount, purple, "\uE72E", "受保护不会被清理的进程数量"), 3, 0);
            return metrics;
        }

        private RoundedPanel MiniMetric(string label, Label value, Color accent, string icon, string tooltip)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 6, 10, 10);
            panel.FillColor = Theme.CardGradientTop;
            panel.FillColor2 = Theme.CardGradientBottom;
            panel.UseGradient = true;
            panel.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical;
            panel.BorderColor = border;
            panel.DrawHighlight = true;
            panel.Radius = 10;

            Panel accentLine = new Panel();
            accentLine.Dock = DockStyle.Bottom;
            accentLine.Height = 2;
            accentLine.BackColor = Color.FromArgb(190, accent);
            panel.Controls.Add(accentLine);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 2;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            layout.Padding = new Padding(12, 8, 12, 8);

            Label iconLabel = new Label();
            iconLabel.Text = icon;
            iconLabel.Font = new Font("Segoe MDL2 Assets", 12F);
            iconLabel.ForeColor = Color.FromArgb(150, accent);
            iconLabel.BackColor = Color.Transparent;
            iconLabel.Dock = DockStyle.Fill;
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            layout.SetRowSpan(iconLabel, 2);
            layout.Controls.Add(iconLabel, 0, 0);

            value.Text = "0";
            value.ForeColor = accent;
            value.BackColor = Color.Transparent;
            value.Font = new Font("Segoe UI Semilight", 21F, FontStyle.Regular, GraphicsUnit.Point);
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.BottomLeft;
            layout.Controls.Add(value, 1, 0);

            Label labelControl = new Label();
            labelControl.Text = label;
            labelControl.ForeColor = muted;
            labelControl.BackColor = Color.Transparent;
            labelControl.Font = new Font(Font.FontFamily, 9F);
            labelControl.Dock = DockStyle.Fill;
            labelControl.TextAlign = ContentAlignment.TopLeft;
            layout.Controls.Add(labelControl, 1, 1);

            panel.Controls.Add(layout);
            layout.BringToFront();

            if (tooltips != null && !string.IsNullOrEmpty(tooltip))
            {
                tooltips.SetToolTip(panel, tooltip);
                tooltips.SetToolTip(layout, tooltip);
            }

            return panel;
        }

        private Control BuildLogSummary()
        {
            RoundedPanel logCard = new RoundedPanel();
            logCard.Dock = DockStyle.Fill;
            logCard.FillColor = card;
            logCard.FillColor2 = card;
            logCard.BorderColor = border;
            logCard.DrawHighlight = true;
            logCard.Radius = 10;
            logCard.Padding = new Padding(12);

            progressBar = new ModernProgressBar();
            progressBar.Dock = DockStyle.Top;
            progressBar.Height = 10;
            progressBar.Value = 0;
            progressBar.Visible = false;
            progressBar.TrackColor = secondaryPanel;
            progressBar.BarColor = primary;
            progressBar.BorderColor = border;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 52;
            header.BackColor = Color.Transparent;

            Label titleLabel = new Label();
            titleLabel.Text = "运行日志";
            titleLabel.ForeColor = titleText;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            titleLabel.Location = new Point(0, 2);
            titleLabel.AutoSize = true;
            header.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "最近状态";
            subtitleLabel.ForeColor = muted;
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.Font = new Font(Font.FontFamily, 8.5F);
            subtitleLabel.Location = new Point(0, 24);
            subtitleLabel.AutoSize = true;
            header.Controls.Add(subtitleLabel);

            FlowLayoutPanel logBtns = new FlowLayoutPanel();
            logBtns.Dock = DockStyle.Right;
            logBtns.Width = 138;
            logBtns.BackColor = Color.Transparent;
            logBtns.FlowDirection = FlowDirection.RightToLeft;
            logBtns.WrapContents = false;
            logBtns.Padding = new Padding(0, 0, 0, 0);

            ModernButton clearLogBtn = MakeButton("清空", card, rowHover, secondaryPanel, muted, 62, true);
            clearLogBtn.Height = 28;
            clearLogBtn.Font = new Font(Font.FontFamily, 8.5F);
            clearLogBtn.Click += delegate { if (logBox != null) logBox.Clear(); };
            logBtns.Controls.Add(clearLogBtn);

            ModernButton copyLogBtn = MakeButton("复制", card, rowHover, secondaryPanel, muted, 62, true);
            copyLogBtn.Height = 28;
            copyLogBtn.Font = new Font(Font.FontFamily, 8.5F);
            copyLogBtn.Click += delegate
            {
                if (logBox != null && logBox.TextLength > 0)
                    Clipboard.SetText(logBox.Text);
            };
            logBtns.Controls.Add(copyLogBtn);

            header.Controls.Add(logBtns);

            logBox = CreateLogBox();
            logBox.Visible = false;

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.Transparent;
            body.Padding = new Padding(0, 6, 0, 0);

            logEmptyLabel = new Label();
            logEmptyLabel.Dock = DockStyle.Fill;
            logEmptyLabel.BackColor = Theme.CardSoft;
            logEmptyLabel.ForeColor = muted;
            logEmptyLabel.Font = new Font(Font.FontFamily, 9F);
            logEmptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            logEmptyLabel.Text = "等待扫描或清理后显示日志";
            logEmptyLabel.Visible = true;

            body.Controls.Add(logEmptyLabel);
            body.Controls.Add(logBox);
            body.Controls.Add(progressBar);
            logBox.BringToFront();

            logCard.Controls.Add(body);
            logCard.Controls.Add(header);
            return logCard;
        }

        private void InitGrids()
        {
            candidateGrid = MakeGrid();
            AddCandidateColumns(candidateGrid);
            AttachCandidateMenu();
            candidateGrid.MultiSelect = true;

            protectedGrid = MakeGrid();
            AddProtectedColumns(protectedGrid);
        }

        private void BuildCandidatePage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageCandidate.Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label title = new Label();
            title.Text = "候选进程";
            title.ForeColor = titleText;
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(0, 4);
            header.Controls.Add(title);

            candidatePageCount = new Label();
            candidatePageCount.Text = "0 个进程";
            candidatePageCount.ForeColor = muted;
            candidatePageCount.Font = new Font(Font.FontFamily, 9.5F);
            candidatePageCount.BackColor = Color.Transparent;
            candidatePageCount.AutoSize = true;
            candidatePageCount.Location = new Point(120, 10);
            header.Controls.Add(candidatePageCount);

            root.Controls.Add(header, 0, 0);

            Panel filterBar = new Panel();
            filterBar.Dock = DockStyle.Fill;
            filterBar.BackColor = Color.Transparent;

            TextBox innerCandidateSearch;
            Panel searchWrapper = MakeSearchBoxWrapped("搜索进程名...", out innerCandidateSearch);
            candidateSearchBox = innerCandidateSearch;
            searchWrapper.Location = new Point(0, 8);
            candidateSearchBox.TextChanged += delegate { ApplyCandidateFilter(); };
            filterBar.Controls.Add(searchWrapper);

            actionFilter = new ComboBox();
            actionFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            actionFilter.Location = new Point(260, 8);
            actionFilter.Size = new Size(140, 32);
            actionFilter.BackColor = secondaryPanel;
            actionFilter.ForeColor = text;
            actionFilter.FlatStyle = FlatStyle.Flat;
            actionFilter.Font = new Font(Font.FontFamily, 9F);
            actionFilter.Items.Add("全部动作");
            actionFilter.Items.Add(ProcessPlanner.ActionGraceful);
            actionFilter.Items.Add(ProcessPlanner.ActionForce);
            actionFilter.Items.Add(ProcessPlanner.ActionReport);
            actionFilter.SelectedIndex = 0;
            actionFilter.SelectedIndexChanged += delegate { ApplyCandidateFilter(); };
            filterBar.Controls.Add(actionFilter);

            root.Controls.Add(filterBar, 0, 1);

            RoundedPanel gridCard = MakeCard();
            gridCard.Padding = new Padding(4);
            gridCard.Margin = new Padding(0);
            candidateGrid.Dock = DockStyle.Fill;
            candidateEmptyLabel = MakeEmptyStateLabel("暂无候选进程\n点击“重新扫描”检查当前后台应用");
            gridCard.Controls.Add(candidateEmptyLabel);
            gridCard.Controls.Add(candidateGrid);
            candidateGrid.BringToFront();
            root.Controls.Add(gridCard, 0, 2);
        }

        private void BuildProtectedPage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageProtected.Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label title = new Label();
            title.Text = "保护列表";
            title.ForeColor = titleText;
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(0, 4);
            header.Controls.Add(title);

            protectedPageCount = new Label();
            protectedPageCount.Text = "0 个进程";
            protectedPageCount.ForeColor = muted;
            protectedPageCount.Font = new Font(Font.FontFamily, 9.5F);
            protectedPageCount.BackColor = Color.Transparent;
            protectedPageCount.AutoSize = true;
            protectedPageCount.Location = new Point(120, 10);
            header.Controls.Add(protectedPageCount);

            root.Controls.Add(header, 0, 0);

            Panel filterBar = new Panel();
            filterBar.Dock = DockStyle.Fill;
            filterBar.BackColor = Color.Transparent;

            TextBox innerProtectedSearch;
            Panel protectedSearchWrapper = MakeSearchBoxWrapped("搜索进程名...", out innerProtectedSearch);
            protectedSearchBox = innerProtectedSearch;
            protectedSearchWrapper.Location = new Point(0, 8);
            protectedSearchBox.TextChanged += delegate { ApplyProtectedFilter(); };
            filterBar.Controls.Add(protectedSearchWrapper);

            Label truncHint = new Label();
            truncHint.Text = "最多显示 90 条";
            truncHint.ForeColor = muted;
            truncHint.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Italic);
            truncHint.BackColor = Color.Transparent;
            truncHint.AutoSize = true;
            truncHint.Location = new Point(260, 14);
            filterBar.Controls.Add(truncHint);

            root.Controls.Add(filterBar, 0, 1);

            RoundedPanel gridCard = MakeCard();
            gridCard.Padding = new Padding(4);
            gridCard.Margin = new Padding(0);
            protectedGrid.Dock = DockStyle.Fill;
            protectedEmptyLabel = MakeEmptyStateLabel("暂无保护进程\n保护名单中的应用会在扫描后显示在这里");
            gridCard.Controls.Add(protectedEmptyLabel);
            gridCard.Controls.Add(protectedGrid);
            protectedGrid.BringToFront();
            root.Controls.Add(gridCard, 0, 2);
        }

        private TextBox MakeSearchBox(string placeholder)
        {
            TextBox box = new TextBox();
            box.Width = 240;
            box.Height = 32;
            box.BackColor = secondaryPanel;
            box.ForeColor = text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = new Font(Font.FontFamily, 9.5F);
            box.Tag = placeholder;
            box.Text = placeholder;
            box.ForeColor = muted;
            box.GotFocus += SearchBoxGotFocus;
            box.LostFocus += SearchBoxLostFocus;
            return box;
        }

        private Panel MakeSearchBoxWrapped(string placeholder, out TextBox innerBox)
        {
            RoundedPanel wrapper = new RoundedPanel();
            wrapper.FillColor = secondaryPanel;
            wrapper.BorderColor = border;
            wrapper.Radius = 6;
            wrapper.Size = new Size(240, 32);

            TextBox box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.BackColor = secondaryPanel;
            box.ForeColor = muted;
            box.BorderStyle = BorderStyle.None;
            box.Font = new Font(Font.FontFamily, 9.5F);
            box.Tag = placeholder;
            box.Text = placeholder;
            box.Margin = new Padding(8, 0, 8, 0);
            box.GotFocus += SearchBoxGotFocus;
            box.LostFocus += SearchBoxLostFocus;
            wrapper.Controls.Add(box);
            innerBox = box;
            return wrapper;
        }

        private void SearchBoxGotFocus(object sender, EventArgs e)
        {
            TextBox box = (TextBox)sender;
            string placeholder = (box.Tag ?? "") as string;
            if (box.Text == placeholder)
            {
                box.Text = "";
                box.ForeColor = text;
            }
        }

        private void SearchBoxLostFocus(object sender, EventArgs e)
        {
            TextBox box = (TextBox)sender;
            string placeholder = (box.Tag ?? "") as string;
            if (string.IsNullOrEmpty(box.Text))
            {
                box.Text = placeholder;
                box.ForeColor = muted;
            }
        }

        private string GetSearchText(TextBox box)
        {
            string placeholder = (box.Tag ?? "") as string;
            if (box.Text == placeholder || string.IsNullOrWhiteSpace(box.Text))
            {
                return "";
            }
            return box.Text.Trim();
        }

        private void ApplyCandidateFilter()
        {
            candidateGrid.Rows.Clear();
            FillOverviewCandidateGrid();
            string search = GetSearchText(candidateSearchBox);
            string actionFilterValue = "";
            if (actionFilter != null && actionFilter.SelectedIndex > 0)
            {
                actionFilterValue = actionFilter.SelectedItem as string ?? "";
            }

            int count = 0;
            foreach (ProcessGroupRow row in allCandidateRows)
            {
                if (!string.IsNullOrEmpty(search) && row.Process.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(actionFilterValue) && !string.Equals(row.Action, actionFilterValue, StringComparison.Ordinal))
                {
                    continue;
                }

                int index = candidateGrid.Rows.Add(GetProcessIcon(row.Path), row.Process, row.Count.ToString(), row.Action, row.RiskScore.ToString(), row.MemoryMb + " MB", row.Note);
                DataGridViewRow gridRow = candidateGrid.Rows[index];
                gridRow.Tag = row;
                ApplyRowTone(gridRow, row.IsHighRisk || row.Action == ProcessPlanner.ActionReport);
                count++;
            }

            candidatePageCount.Text = count.ToString() + " / " + allCandidateRows.Count.ToString() + " 个进程";
            if (candidateEmptyLabel != null)
            {
                candidateEmptyLabel.Visible = count == 0;
                candidateGrid.Visible = count > 0;
            }
        }

        private void FillOverviewCandidateGrid()
        {
            if (overviewCandidateGrid == null)
            {
                return;
            }

            overviewCandidateGrid.Rows.Clear();
            foreach (ProcessGroupRow row in allCandidateRows.Take(10))
            {
                int index = overviewCandidateGrid.Rows.Add(GetProcessIcon(row.Path), row.Process, row.Action, row.RiskScore.ToString(), row.Note);
                DataGridViewRow gridRow = overviewCandidateGrid.Rows[index];
                gridRow.Tag = row;
                ApplyRowTone(gridRow, row.IsHighRisk || row.Action == ProcessPlanner.ActionReport);
            }
        }

        private void ApplyProtectedFilter()
        {
            protectedGrid.Rows.Clear();
            string search = GetSearchText(protectedSearchBox);

            int count = 0;
            foreach (ProcessGroupRow row in allProtectedRows)
            {
                if (!string.IsNullOrEmpty(search) && row.Process.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int index = protectedGrid.Rows.Add(GetProcessIcon(row.Path), row.Process, row.Count.ToString(), row.RiskScore.ToString(), row.Note);
                DataGridViewRow gridRow = protectedGrid.Rows[index];
                gridRow.Tag = row;
                ApplyRowTone(gridRow, true);
                count++;
            }

            protectedPageCount.Text = count.ToString() + " / " + allProtectedRows.Count.ToString() + " 个进程";
            if (protectedEmptyLabel != null)
            {
                protectedEmptyLabel.Visible = count == 0;
                protectedGrid.Visible = count > 0;
            }
        }

        private Label MakeEmptyStateLabel(string caption)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.BackColor = Color.Transparent;
            label.ForeColor = muted;
            label.Font = new Font(Font.FontFamily, 11F, FontStyle.Regular);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Text = caption;
            label.Visible = false;
            return label;
        }

        private void BuildConfigPage()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pageConfig.Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.Transparent;

            Label title = new Label();
            title.Text = "配置";
            title.ForeColor = titleText;
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(0, 4);
            header.Controls.Add(title);

            FlowLayoutPanel headerButtons = new FlowLayoutPanel();
            headerButtons.Dock = DockStyle.Right;
            headerButtons.Width = 200;
            headerButtons.BackColor = Color.Transparent;
            headerButtons.FlowDirection = FlowDirection.RightToLeft;
            headerButtons.WrapContents = false;
            headerButtons.Padding = new Padding(0, 8, 0, 0);

            ModernButton saveBtn = MakeButton("保存配置", primary, primaryHover, primary, Color.White, 110, false);
            saveBtn.Click += SaveConfigClick;
            headerButtons.Controls.Add(saveBtn);

            ModernButton reloadBtn = MakeButton("重新加载", card, rowHover, secondaryPanel, text, 90, true);
            reloadBtn.Click += delegate { LoadConfigIntoUI(); };
            headerButtons.Controls.Add(reloadBtn);

            header.Controls.Add(headerButtons);
            root.Controls.Add(header, 0, 0);

            configTabBar = new TabBar();
            configTabBar.Dock = DockStyle.Fill;
            configTabBar.BackColor = secondaryPanel;
            configTabBar.AccentColor = primary;
            configTabBar.DividerColor = border;
            configTabBar.AddTab("lists", "名单配置");
            configTabBar.AddTab("advanced", "高级设置");
            configTabBar.AddTab("learning", "学习记录");
            configTabBar.TabSelected += delegate(object s, string tabId) { SwitchConfigTab(tabId); };
            root.Controls.Add(configTabBar, 0, 1);

            configTabLists = CreatePagePanel();
            configTabAdvanced = CreatePagePanel();
            configTabLearning = CreatePagePanel();
            configTabLists.Visible = true;

            Panel tabHost = new Panel();
            tabHost.Dock = DockStyle.Fill;
            tabHost.BackColor = Color.Transparent;
            tabHost.Controls.Add(configTabLists);
            tabHost.Controls.Add(configTabAdvanced);
            tabHost.Controls.Add(configTabLearning);
            root.Controls.Add(tabHost, 0, 2);

            BuildNameListsTab();
            BuildAdvancedTab();
            BuildLearningTab();
            LoadConfigIntoUI();
        }

        private void SwitchConfigTab(string tabId)
        {
            configTabLists.Visible = tabId == "lists";
            configTabAdvanced.Visible = tabId == "advanced";
            configTabLearning.Visible = tabId == "learning";
        }

        private void BuildNameListsTab()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 3;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));

            targetList = new ListBox();
            protectedList = new ListBox();
            forceList = new ListBox();
            targetInput = new TextBox();
            protectedInput = new TextBox();
            forceInput = new TextBox();

            layout.Controls.Add(BuildListEditor("目标名单", "进入候选列表的软件", targetList, targetInput), 0, 0);
            layout.Controls.Add(BuildListEditor("保护名单", "永远不会主动关闭", protectedList, protectedInput), 1, 0);
            layout.Controls.Add(BuildListEditor("强制清理名单", "只允许这些进程 Kill", forceList, forceInput), 2, 0);

            configTabLists.Controls.Add(layout);
        }

        private RoundedPanel BuildListEditor(string title, string subtitle, ListBox list, TextBox input)
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 10, 0);
            panel.Padding = new Padding(14);
            panel.FillColor = card;
            panel.BorderColor = border;
            panel.Radius = 8;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 5;
            layout.ColumnCount = 1;
            layout.BackColor = Color.Transparent;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.Controls.Add(layout);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.ForeColor = titleText;
            titleLabel.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
            titleLabel.Dock = DockStyle.Fill;
            layout.Controls.Add(titleLabel, 0, 0);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.ForeColor = muted;
            subtitleLabel.Font = new Font(Font.FontFamily, 8.5F);
            subtitleLabel.Dock = DockStyle.Fill;
            layout.Controls.Add(subtitleLabel, 0, 1);

            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = secondaryPanel;
            list.ForeColor = text;
            list.Font = new Font(Font.FontFamily, 9F);
            layout.Controls.Add(list, 0, 2);

            input.Dock = DockStyle.Fill;
            input.BorderStyle = BorderStyle.None;
            input.BackColor = secondaryPanel;
            input.ForeColor = text;
            input.Font = new Font(Font.FontFamily, 9F);
            input.Margin = new Padding(8, 6, 8, 6);
            layout.Controls.Add(WrapInput(input), 0, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.BackColor = Color.Transparent;

            ModernButton addButton = MakeButton("添加", primary, primaryHover, primary, Color.White, 72, false);
            addButton.Height = 34;
            addButton.Click += delegate { AddInputToList(list, input); };
            buttons.Controls.Add(addButton);

            ModernButton removeButton = MakeButton("移除", danger, dangerHover, danger, Color.White, 72, false);
            removeButton.Height = 34;
            removeButton.Click += delegate { RemoveSelectedFromList(list); };
            buttons.Controls.Add(removeButton);

            layout.Controls.Add(buttons, 0, 4);
            return panel;
        }

        private void BuildAdvancedTab()
        {
            RoundedPanel cardPanel = new RoundedPanel();
            cardPanel.Dock = DockStyle.Fill;
            cardPanel.FillColor = card;
            cardPanel.BorderColor = border;
            cardPanel.Radius = 8;
            cardPanel.Padding = new Padding(24);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.Height = 240;
            layout.BackColor = Color.Transparent;
            layout.ColumnCount = 2;
            layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

            Label sectionTitle = new Label();
            sectionTitle.Text = "超时参数";
            sectionTitle.ForeColor = titleText;
            sectionTitle.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold);
            sectionTitle.BackColor = Color.Transparent;
            sectionTitle.Dock = DockStyle.Fill;
            layout.Controls.Add(sectionTitle, 0, 0);
            layout.SetColumnSpan(sectionTitle, 2);

            waitSecondsInput = MakeNumericUpDown(1, 60, 5);
            AddAdvancedRow(layout, 1, "等待时间（秒）", "发送关闭请求后等待进程自行退出的时间", waitSecondsInput);

            gracefulTimeoutInput = MakeNumericUpDown(1, 60, 5);
            AddAdvancedRow(layout, 2, "温和关闭超时（秒）", "发送关闭请求后的最长等待时间", gracefulTimeoutInput);

            queryTimeoutInput = MakeNumericUpDown(1, 30, 3);
            AddAdvancedRow(layout, 3, "查询超时（秒）", "WMI 查询进程信息的最长等待时间", queryTimeoutInput);

            cardPanel.Controls.Add(layout);
            configTabAdvanced.Controls.Add(cardPanel);
        }

        private NumericUpDown MakeNumericUpDown(int min, int max, int value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            n.Width = 100;
            n.Height = 30;
            n.BackColor = secondaryPanel;
            n.ForeColor = text;
            n.Font = new Font(Font.FontFamily, 10F);
            return n;
        }

        private void AddAdvancedRow(TableLayoutPanel layout, int row, string label, string description, NumericUpDown control)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.ForeColor = titleText;
            lbl.Font = new Font(Font.FontFamily, 10F);
            lbl.BackColor = Color.Transparent;
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(lbl, 0, row);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.BackColor = Color.Transparent;
            control.Location = new Point(0, 4);
            right.Controls.Add(control);

            Label desc = new Label();
            desc.Text = description;
            desc.ForeColor = muted;
            desc.Font = new Font(Font.FontFamily, 8.5F);
            desc.BackColor = Color.Transparent;
            desc.AutoSize = true;
            desc.Location = new Point(110, 10);
            right.Controls.Add(desc);

            layout.Controls.Add(right, 1, row);
        }

        private void BuildLearningTab()
        {
            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.BackColor = Color.Transparent;
            scrollHost.AutoScroll = true;

            suggestionsContainer = new Panel();
            suggestionsContainer.Dock = DockStyle.Top;
            suggestionsContainer.BackColor = Color.Transparent;
            suggestionsContainer.AutoSize = true;

            scrollHost.Controls.Add(suggestionsContainer);
            configTabLearning.Controls.Add(scrollHost);
        }

        private void LoadConfigIntoUI()
        {
            AppConfig config = AppConfig.Load(configPath);
            FillListBox(targetList, config.targetNames);
            FillListBox(protectedList, config.protectedNames);
            FillListBox(forceList, config.forceAllowedNames);
            waitSecondsInput.Value = Math.Max(waitSecondsInput.Minimum, Math.Min(waitSecondsInput.Maximum, config.waitSeconds));
            gracefulTimeoutInput.Value = Math.Max(gracefulTimeoutInput.Minimum, Math.Min(gracefulTimeoutInput.Maximum, config.gracefulTimeoutSeconds));
            queryTimeoutInput.Value = Math.Max(queryTimeoutInput.Minimum, Math.Min(queryTimeoutInput.Maximum, config.queryTimeoutSeconds));
            LoadSuggestionsIntoTab();
        }

        private void FillListBox(ListBox list, string[] names)
        {
            list.Items.Clear();
            if (names == null) return;
            string[] sorted = names.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (string name in sorted)
            {
                list.Items.Add(name);
            }
        }

        private void AddInputToList(ListBox list, TextBox input)
        {
            string value = (input.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value) || value == (input.Tag as string ?? ""))
            {
                return;
            }
            foreach (string item in list.Items)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("[INFO] 重复项：" + value + "，已跳过。");
                    input.Text = "";
                    return;
                }
            }
            list.Items.Add(value);
            input.Text = "";
            SortListBox(list);
        }

        private void RemoveSelectedFromList(ListBox list)
        {
            if (list.SelectedIndex >= 0)
            {
                list.Items.RemoveAt(list.SelectedIndex);
            }
        }

        private void SortListBox(ListBox list)
        {
            List<string> items = new List<string>();
            foreach (string item in list.Items) items.Add(item);
            items.Sort(StringComparer.OrdinalIgnoreCase);
            list.Items.Clear();
            foreach (string item in items) list.Items.Add(item);
        }

        private string[] ListBoxToArray(ListBox list)
        {
            List<string> items = new List<string>();
            foreach (string item in list.Items)
            {
                if (!string.IsNullOrWhiteSpace(item)) items.Add(item.Trim());
            }
            return items.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void SaveConfigClick(object sender, EventArgs e)
        {
            AppConfig config = AppConfig.Load(configPath);
            config.targetNames = ListBoxToArray(targetList);
            config.protectedNames = ListBoxToArray(protectedList);
            config.forceAllowedNames = ListBoxToArray(forceList);
            config.waitSeconds = (int)waitSecondsInput.Value;
            config.gracefulTimeoutSeconds = (int)gracefulTimeoutInput.Value;
            config.queryTimeoutSeconds = (int)queryTimeoutInput.Value;

            List<string> conflicts = DetectConfigConflicts(config);
            if (conflicts.Count > 0)
            {
                string message = "检测到名单冲突：\n\n" + string.Join("\n", conflicts) + "\n\n是否仍然保存？";
                if (MessageBox.Show(message, "配置冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
            }

            AppConfig.Save(configPath, config);
            AppendLog("[OK] 配置已保存。");
        }

        private List<string> DetectConfigConflicts(AppConfig config)
        {
            List<string> conflicts = new List<string>();
            HashSet<string> targetSet = config.TargetSet();
            HashSet<string> protectedSet = config.ProtectedSet();
            HashSet<string> forceSet = config.ForceSet();

            List<string> tp = new List<string>();
            foreach (string t in targetSet)
            {
                if (protectedSet.Contains(t)) tp.Add(t);
            }
            if (tp.Count > 0)
            {
                conflicts.Add("目标名单 ∩ 保护名单：" + string.Join(", ", tp));
            }

            List<string> tf = new List<string>();
            foreach (string t in targetSet)
            {
                if (forceSet.Contains(t)) tf.Add(t);
            }
            if (tf.Count > 0)
            {
                conflicts.Add("目标名单 ∩ 强制名单：" + string.Join(", ", tf));
            }

            List<string> pf = new List<string>();
            foreach (string p in protectedSet)
            {
                if (forceSet.Contains(p)) pf.Add(p);
            }
            if (pf.Count > 0)
            {
                conflicts.Add("保护名单 ∩ 强制名单：" + string.Join(", ", pf));
            }

            return conflicts;
        }

        private void LoadSuggestionsIntoTab()
        {
            LoadSuggestionsIntoContainer(suggestionsContainer);
            LoadSuggestionsIntoContainer(learningPageSuggestionsContainer);
        }

        private void LoadSuggestionsIntoContainer(Panel container)
        {
            if (container == null)
            {
                return;
            }

            container.Controls.Clear();
            AppConfig config = AppConfig.Load(configPath);
            List<UserPreferenceSuggestion> suggestions = preferences.BuildSuggestions(config);
            if (suggestions.Count == 0)
            {
                RoundedPanel emptyCard = MakeCard();
                emptyCard.Dock = DockStyle.Top;
                emptyCard.Height = 120;
                emptyCard.Margin = new Padding(8, 8, 12, 0);

                Label empty = new Label();
                empty.Text = "✨ 暂无学习建议\n使用一段时间后，会根据你的操作习惯生成智能建议。";
                empty.ForeColor = muted;
                empty.Font = new Font(Font.FontFamily, 10.5F);
                empty.Dock = DockStyle.Fill;
                empty.TextAlign = ContentAlignment.MiddleCenter;
                empty.BackColor = Color.Transparent;
                emptyCard.Controls.Add(empty);
                container.Controls.Add(emptyCard);
                return;
            }

            int y = 8;
            foreach (UserPreferenceSuggestion suggestion in suggestions)
            {
                RoundedPanel cardPanel = new RoundedPanel();
                cardPanel.Location = new Point(8, y);
                cardPanel.Size = new Size(container.Width > 40 ? container.Width - 24 : 680, 82);
                cardPanel.FillColor = card;
                cardPanel.FillColor2 = cardSoft;
                cardPanel.UseGradient = true;
                cardPanel.BorderColor = border;
                cardPanel.DrawHighlight = true;
                cardPanel.Radius = 10;
                cardPanel.Padding = new Padding(16, 12, 16, 12);
                cardPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                Label typeLabel = new Label();
                typeLabel.Text = suggestion.Type;
                typeLabel.ForeColor = purple;
                typeLabel.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
                typeLabel.Location = new Point(16, 10);
                typeLabel.AutoSize = true;
                typeLabel.BackColor = Color.Transparent;
                cardPanel.Controls.Add(typeLabel);

                Label nameLabel = new Label();
                nameLabel.Text = suggestion.ProcessName;
                nameLabel.ForeColor = titleText;
                nameLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
                nameLabel.Location = new Point(16, 30);
                nameLabel.AutoSize = true;
                nameLabel.BackColor = Color.Transparent;
                cardPanel.Controls.Add(nameLabel);

                Label reasonLabel = new Label();
                reasonLabel.Text = suggestion.Reason;
                reasonLabel.ForeColor = muted;
                reasonLabel.Font = new Font(Font.FontFamily, 9F);
                reasonLabel.Location = new Point(16, 55);
                reasonLabel.AutoSize = true;
                reasonLabel.BackColor = Color.Transparent;
                cardPanel.Controls.Add(reasonLabel);

                FlowLayoutPanel btnFlow = new FlowLayoutPanel();
                btnFlow.FlowDirection = FlowDirection.RightToLeft;
                btnFlow.Size = new Size(160, 36);
                btnFlow.BackColor = Color.Transparent;
                btnFlow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                btnFlow.Location = new Point(cardPanel.Width > 170 ? cardPanel.Width - 168 : 4, 24);

                ModernButton acceptBtn = MakeButton("接受", primary, primaryHover, primary, Color.White, 68, false);
                acceptBtn.Height = 30;
                acceptBtn.Radius = 5;
                acceptBtn.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
                UserPreferenceSuggestion capturedAccept = suggestion;
                acceptBtn.Click += delegate { AcceptSuggestionFromTab(capturedAccept); };

                ModernButton ignoreBtn = MakeButton("忽略", secondaryPanel, rowHover, secondaryPanel, muted, 68, true);
                ignoreBtn.Height = 30;
                ignoreBtn.Radius = 5;
                ignoreBtn.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold);
                UserPreferenceSuggestion capturedIgnore = suggestion;
                ignoreBtn.Click += delegate { IgnoreSuggestionFromTab(capturedIgnore); };

                btnFlow.Controls.Add(ignoreBtn);
                btnFlow.Controls.Add(acceptBtn);
                cardPanel.Controls.Add(btnFlow);

                cardPanel.Resize += delegate
                {
                    btnFlow.Left = cardPanel.Width - 168;
                };

                container.Controls.Add(cardPanel);
                y += 92;
            }
        }

        private void AcceptSuggestionFromTab(UserPreferenceSuggestion suggestion)
        {
            AppConfig config = AppConfig.Load(configPath);
            if (string.Equals(suggestion.Type, "保护名单", StringComparison.Ordinal))
            {
                config.protectedNames = AddUnique(config.protectedNames, suggestion.ProcessName);
                AppendLog("[OK] 已加入保护名单：" + suggestion.ProcessName);
            }
            else if (string.Equals(suggestion.Type, "强制清理名单", StringComparison.Ordinal))
            {
                config.forceAllowedNames = AddUnique(config.forceAllowedNames, suggestion.ProcessName);
                AppendLog("[OK] 已加入强制清理名单：" + suggestion.ProcessName);
            }
            AppConfig.Save(configPath, config);
            LoadConfigIntoUI();
        }

        private void IgnoreSuggestionFromTab(UserPreferenceSuggestion suggestion)
        {
            preferences.IgnoreSuggestion(suggestion);
            LoadSuggestionsIntoTab();
        }

        private RoundedPanel CardWithTitle(string title, string subtitle, Control child)
        {
            RoundedPanel outer = MakeCard();
            outer.Padding = new Padding(16, 14, 16, 16);
            outer.Margin = new Padding(0, 0, 0, 12);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 48;
            header.BackColor = Color.Transparent;

            Label label = new Label();
            label.Text = title;
            label.ForeColor = titleText;
            label.BackColor = Color.Transparent;
            label.Font = new Font(Font.FontFamily, 11.5F, FontStyle.Bold);
            label.Location = new Point(0, 0);
            label.AutoSize = true;
            header.Controls.Add(label);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.ForeColor = muted;
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.Font = new Font(Font.FontFamily, 8.5F);
            subtitleLabel.Location = new Point(1, 25);
            subtitleLabel.AutoSize = true;
            header.Controls.Add(subtitleLabel);
            outer.Controls.Add(header);

            child.Dock = DockStyle.Fill;
            outer.Controls.Add(child);
            child.BringToFront();
            return outer;
        }

        private RoundedPanel MakeCard()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.FillColor = card;
            panel.FillColor2 = card;
            panel.UseGradient = false;
            panel.BorderColor = border;
            panel.DrawHighlight = true;
            panel.Radius = 10;
            panel.Padding = new Padding(12);
            return panel;
        }

        private RoundedPanel WrapInput(TextBox box)
        {
            RoundedPanel wrap = new RoundedPanel();
            wrap.FillColor = secondaryPanel;
            wrap.BorderColor = border;
            wrap.Radius = 6;
            wrap.Dock = DockStyle.Fill;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = secondaryPanel;
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(8, 6, 8, 6);
            wrap.Controls.Add(box);
            return wrap;
        }

        private ProcessGridView MakeGrid()
        {
            ProcessGridView grid = new ProcessGridView();
            grid.BackColor = card;
            grid.BackgroundColor = card;
            grid.HeaderBackColor = card;
            grid.RowBackColor = cardSoft;
            grid.AlternateRowBackColor = rowAlt;
            grid.HotRowBackColor = rowHover;
            grid.DividerColor = border;
            grid.BodyTextColor = text;
            grid.MutedTextColor = muted;
            grid.HighRiskTextColor = muted;
            grid.AccentBlue = primary;
            grid.AccentOrange = force;
            grid.AccentPurple = purple;
            grid.DefaultCellStyle.BackColor = cardSoft;
            grid.DefaultCellStyle.ForeColor = text;
            grid.DefaultCellStyle.SelectionBackColor = rowHover;
            grid.DefaultCellStyle.SelectionForeColor = titleText;
            grid.AlternatingRowsDefaultCellStyle.BackColor = rowAlt;
            grid.ColumnHeadersDefaultCellStyle.BackColor = card;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = titleText;
            grid.DataError += delegate { };
            return grid;
        }

        private void AddCandidateColumns(ProcessGridView grid)
        {
            DataGridViewImageColumn icon = new DataGridViewImageColumn();
            icon.Name = "IconColumn";
            icon.HeaderText = "";
            icon.Width = 48;
            icon.ImageLayout = DataGridViewImageCellLayout.Zoom;
            grid.Columns.Add(icon);

            grid.Columns.Add(MakeTextColumn("ProcessColumn", "进程", 180));
            grid.Columns.Add(MakeTextColumn("CountColumn", "数量", 62));
            grid.Columns.Add(MakeTextColumn("ActionColumn", "动作", 100));
            grid.Columns.Add(MakeTextColumn("RiskColumn", "风险", 70));
            grid.Columns.Add(MakeTextColumn("MemoryColumn", "内存", 88));
            DataGridViewTextBoxColumn note = MakeTextColumn("NoteColumn", "说明", 260);
            note.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(note);

            grid.Columns["ProcessColumn"].SortMode = DataGridViewColumnSortMode.Automatic;
            grid.Columns["RiskColumn"].SortMode = DataGridViewColumnSortMode.Automatic;
            grid.Columns["MemoryColumn"].SortMode = DataGridViewColumnSortMode.Automatic;
        }

        private void AddOverviewCandidateColumns(ProcessGridView grid)
        {
            DataGridViewImageColumn icon = new DataGridViewImageColumn();
            icon.Name = "IconColumn";
            icon.HeaderText = "";
            icon.Width = 42;
            icon.ImageLayout = DataGridViewImageCellLayout.Zoom;
            grid.Columns.Add(icon);

            grid.Columns.Add(MakeTextColumn("ProcessColumn", "进程", 170));
            grid.Columns.Add(MakeTextColumn("ActionColumn", "动作", 96));
            grid.Columns.Add(MakeTextColumn("RiskColumn", "风险", 62));
            DataGridViewTextBoxColumn note = MakeTextColumn("NoteColumn", "说明", 280);
            note.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(note);

            grid.Columns["ProcessColumn"].SortMode = DataGridViewColumnSortMode.Automatic;
            grid.Columns["RiskColumn"].SortMode = DataGridViewColumnSortMode.Automatic;
        }

        private void AddProtectedColumns(ProcessGridView grid)
        {
            DataGridViewImageColumn icon = new DataGridViewImageColumn();
            icon.Name = "IconColumn";
            icon.HeaderText = "";
            icon.Width = 42;
            icon.ImageLayout = DataGridViewImageCellLayout.Zoom;
            grid.Columns.Add(icon);

            grid.Columns.Add(MakeTextColumn("ProcessColumn", "进程", 118));
            grid.Columns.Add(MakeTextColumn("CountColumn", "数", 44));
            grid.Columns.Add(MakeTextColumn("RiskColumn", "风险", 46));
            DataGridViewTextBoxColumn note = MakeTextColumn("NoteColumn", "说明", 80);
            note.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(note);
        }

        private DataGridViewTextBoxColumn MakeTextColumn(string name, string header, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            return column;
        }

        private ModernButton MakeButton(string caption, Color baseColor, Color hoverColor, Color pressedColor, Color foreColor, int width, bool drawBorder)
        {
            ModernButton button = new ModernButton();
            button.Text = caption;
            button.Width = width;
            button.Height = 40;
            button.Radius = 6;
            button.BaseColor = baseColor;
            button.HoverColor = hoverColor;
            button.PressedColor = pressedColor;
            button.TextColor = foreColor;
            button.BorderColor = buttonBorder;
            button.DrawBorder = drawBorder;
            button.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }

        private PillLabel MakePill(string caption, Color fill, Color fore, Color outline, int width)
        {
            PillLabel label = new PillLabel();
            label.Text = caption;
            label.FillColor = fill;
            label.TextColor = fore;
            label.BorderColor = outline;
            label.Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold);
            label.Size = new Size(width, 26);
            label.Margin = new Padding(0, 0, 8, 0);
            return label;
        }

        private void AttachCandidateMenu()
        {
            candidateMenu = new ContextMenuStrip();
            candidateMenu.Renderer = new DarkMenuRenderer();
            candidateMenu.Items.Add("从本次清理列表移除", null, delegate { RemoveSelectedCandidateFromPlan(); });
            candidateMenu.Items.Add("加入保护名单", null, delegate { AddSelectedCandidateToConfig("protect"); });
            candidateMenu.Items.Add("加入强制清理名单", null, delegate { AddSelectedCandidateToConfig("force"); });
            candidateGrid.ContextMenuStrip = candidateMenu;
            candidateGrid.MouseDown += CandidateGridMouseDown;
        }

        private async void MainFormLoad(object sender, EventArgs e)
        {
            if (SuppressAutoScan)
            {
                return;
            }

            await ScanProcessesAsync("启动自动扫描");
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await ScanProcessesAsync("手动重新扫描");
        }

        private void PreviewButtonClick(object sender, EventArgs e)
        {
            if (currentPlan == null)
            {
                AppendLog("[INFO] 暂无扫描结果，请先点击“重新扫描”。");
                return;
            }

            AppendLog(ProcessPlanner.FormatPlan(currentPlan));
        }

        private async Task ScanProcessesAsync(string reason)
        {
            if (scanInProgress || cleanupInProgress)
            {
                return;
            }

            scanInProgress = true;
            SetOperationState("scanning");
            lastRefreshLabel.Text = "扫描中";
            AppendLog("[INFO] " + reason + "：" + DateTime.Now.ToString("HH:mm:ss"));

            try
            {
                ClosePlan plan = await Task.Run(delegate
                {
                    return ProcessPlanner.GetClosePlan(configPath);
                });

                LoadPlan(plan);
                lastRefreshLabel.Text = "已刷新 " + DateTime.Now.ToString("HH:mm:ss");
                AppendLog("[OK] 扫描完成：" + ProcessPlanner.Summary(currentPlan));
            }
            catch (Exception ex)
            {
                lastRefreshLabel.Text = "扫描失败";
                AppendLog("[ERROR] 扫描失败：" + ex.Message);
                ShowDarkDialog("扫描失败", TranslateErrorMessage(ex.Message), true);
            }
            finally
            {
                scanInProgress = false;
                SetOperationState("idle");
            }
        }

        public void LoadPlanForSnapshot(ClosePlan plan)
        {
            LoadPlan(plan);
        }

        private void LoadPlan(ClosePlan plan)
        {
            currentPlan = plan;
            FillCandidateList();
            FillProtectedList();
            UpdateMetrics();
        }

        private void FillCandidateList()
        {
            allCandidateRows.Clear();
            if (currentPlan != null)
            {
                allCandidateRows.AddRange(ProcessPlanner.GroupRows(currentPlan.Candidates));
            }
            ApplyCandidateFilter();
        }

        private void FillProtectedList()
        {
            allProtectedRows.Clear();
            if (currentPlan != null)
            {
                allProtectedRows.AddRange(ProcessPlanner.GroupRows(currentPlan.Protected).Take(90));
            }
            ApplyProtectedFilter();
        }

        private void ApplyRowTone(DataGridViewRow row, bool mutedRow)
        {
            row.Height = 36;
            row.DefaultCellStyle.ForeColor = mutedRow ? muted : text;
            row.DefaultCellStyle.SelectionForeColor = titleText;
            row.DefaultCellStyle.BackColor = row.Index % 2 == 0 ? cardSoft : rowAlt;
            row.DefaultCellStyle.SelectionBackColor = rowHover;
        }

        private void UpdateMetrics()
        {
            if (currentPlan == null)
            {
                candidateCount.Text = "0";
                gracefulCount.Text = "0";
                forceCount.Text = "0";
                protectedCount.Text = "0";
                return;
            }

            candidateCount.Text = currentPlan.Candidates.Count.ToString();
            gracefulCount.Text = currentPlan.Candidates.Count(r => r.Action == ProcessPlanner.ActionGraceful).ToString();
            forceCount.Text = currentPlan.Candidates.Count(r => r.Action == ProcessPlanner.ActionForce).ToString();
            protectedCount.Text = currentPlan.Protected.Count.ToString();
        }

        private async void CloseButtonClick(object sender, EventArgs e)
        {
            if (cleanupInProgress && cleanupCts != null && !cleanupCts.IsCancellationRequested)
            {
                cleanupCts.Cancel();
                SetOperationState("cancelling");
                AppendLog("[INFO] 正在取消清理...");
                return;
            }

            if (currentPlan == null)
            {
                await ScanProcessesAsync("清理前扫描");
                if (currentPlan == null)
                {
                    return;
                }
            }

            string message = string.Format("将处理 {0} 个进程。\n\n确认后会先发送关闭请求，再尝试关机会话提示；\n只有强制白名单内的进程才会强制终止。", currentPlan.Candidates.Count);
            if (!ShowDarkConfirm("确认一键清理", message))
            {
                AppendLog("[INFO] 已取消。");
                return;
            }

            await ExecuteCleanupAsync();
        }

        private async Task ExecuteCleanupAsync()
        {
            if (cleanupInProgress || scanInProgress || currentPlan == null)
            {
                return;
            }

            cleanupInProgress = true;
            cleanupCts = new CancellationTokenSource();
            CancellationToken token = cleanupCts.Token;
            SetOperationState("cleaning");
            progressBar.Visible = true;
            progressBar.Value = 0;
            ClosePlan planSnapshot = currentPlan;
            CloseResult result = null;
            bool wasCancelled = false;
            int totalTargets = planSnapshot.Candidates.Count(r => r.Action != ProcessPlanner.ActionReport);
            int processedCount = 0;

            IProgress<string> progress = new Progress<string>(delegate(string message)
            {
                AppendLog(message);
                if (message.Contains("[CLEAN EXIT]") || message.Contains("[OK]") || message.Contains("[FORCE]"))
                {
                    processedCount++;
                    if (totalTargets > 0)
                    {
                        progressBar.Value = Math.Min(90, (int)((double)processedCount / totalTargets * 90));
                    }
                }
            });

            try
            {
                result = await Task.Run(delegate
                {
                    return CloseExecutor.Execute(planSnapshot, delegate(string message)
                    {
                        progress.Report(message);
                    }, token);
                });

                progressBar.Value = 92;
                preferences.RecordCleanup(result);
                PromptLearningSuggestions();
                AppendLog("[OK] 清理流程完成，正在重新扫描。");
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                int closed = (result != null) ? result.GracefulClosed.Count + result.Forced.Count : 0;
                AppendLog(string.Format("[SKIP] 用户取消了清理。已处理 {0} 个进程，剩余进程已跳过。", closed));
            }
            catch (Exception ex)
            {
                AppendLog("[ERROR] 清理流程异常：" + ex.Message);
                ShowDarkDialog("清理失败", TranslateErrorMessage(ex.Message), true);
            }
            finally
            {
                progressBar.Value = 100;
                cleanupInProgress = false;
                if (cleanupCts != null)
                {
                    cleanupCts.Dispose();
                    cleanupCts = null;
                }
                SetOperationState("idle");
            }

            await Task.Delay(250);
            progressBar.Visible = false;
            await ScanProcessesAsync(wasCancelled ? "取消后重新扫描" : "清理后自动扫描");
        }

        private void SetOperationState(string state)
        {
            bool scanning = state == "scanning";
            bool cleaning = state == "cleaning";
            bool cancelling = state == "cancelling";
            bool busy = scanning || cleaning || cancelling;

            refreshButton.Enabled = !busy;
            previewButton.Enabled = !busy && currentPlan != null;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (scanning)
            {
                refreshButton.Text = "扫描中...";
                closeButton.Text = "⚡ 一键清理";
                closeButton.Enabled = false;
            }
            else if (cleaning)
            {
                refreshButton.Text = "重新扫描";
                closeButton.Text = "✕ 取消清理";
                closeButton.Enabled = true;
            }
            else if (cancelling)
            {
                refreshButton.Text = "重新扫描";
                closeButton.Text = "取消中...";
                closeButton.Enabled = false;
            }
            else
            {
                refreshButton.Text = "重新扫描";
                closeButton.Text = "⚡ 一键清理";
                closeButton.Enabled = currentPlan != null;
            }
        }

        private void CandidateGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            DataGridView.HitTestInfo hit = candidateGrid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                DataGridViewRow clickedRow = candidateGrid.Rows[hit.RowIndex];
                if (!clickedRow.Selected)
                {
                    candidateGrid.ClearSelection();
                    clickedRow.Selected = true;
                }
                candidateGrid.CurrentCell = clickedRow.Cells[Math.Min(1, candidateGrid.Columns.Count - 1)];
            }
        }

        private ProcessGroupRow SelectedCandidateGroup()
        {
            if (candidateGrid.CurrentRow == null)
            {
                return null;
            }

            return candidateGrid.CurrentRow.Tag as ProcessGroupRow;
        }

        private List<ProcessGroupRow> SelectedCandidateGroups()
        {
            List<ProcessGroupRow> result = new List<ProcessGroupRow>();
            foreach (DataGridViewRow gridRow in candidateGrid.SelectedRows)
            {
                ProcessGroupRow row = gridRow.Tag as ProcessGroupRow;
                if (row != null)
                {
                    result.Add(row);
                }
            }
            if (result.Count == 0)
            {
                ProcessGroupRow single = SelectedCandidateGroup();
                if (single != null)
                {
                    result.Add(single);
                }
            }
            return result;
        }

        private void RemoveSelectedCandidateFromPlan()
        {
            List<ProcessGroupRow> rows = SelectedCandidateGroups();
            if (rows.Count == 0 || currentPlan == null)
            {
                return;
            }

            int removedCount = 0;
            foreach (ProcessGroupRow row in rows)
            {
                currentPlan.Candidates.RemoveAll(r => string.Equals(r.ProcessName, row.Process, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Action, row.Action, StringComparison.Ordinal));
                int count = preferences.IncrementManualRemove(row.Process);
                AppendLog("[SKIP] 已从本次清理列表移除：" + row.Process + "；累计 " + count + " 次。");

                if (count >= 3)
                {
                    PromptProtectionSuggestion(row.Process, count);
                }
                removedCount++;
            }

            if (removedCount > 1)
            {
                AppendLog("[INFO] 共移除 " + removedCount + " 个进程。");
            }

            FillCandidateList();
            UpdateMetrics();
        }

        private void AddSelectedCandidateToConfig(string type)
        {
            List<ProcessGroupRow> rows = SelectedCandidateGroups();
            if (rows.Count == 0)
            {
                return;
            }

            foreach (ProcessGroupRow row in rows)
            {
                if (string.Equals(type, "protect", StringComparison.Ordinal))
                {
                    AddNameToConfig("protect", row.Process);
                    AppendLog("[OK] 已加入保护名单：" + row.Process);
                }
                else if (string.Equals(type, "force", StringComparison.Ordinal))
                {
                    AddNameToConfig("force", row.Process);
                    AppendLog("[OK] 已加入强制清理名单：" + row.Process);
                }
            }
        }

        private void PromptProtectionSuggestion(string processName, int count)
        {
            AppConfig config = AppConfig.Load(configPath);
            if (config.ProtectedSet().Contains(processName))
            {
                return;
            }

            string message = "你已经多次从本次清理列表移除 " + processName + "。\n\n是否将它加入保护名单？";
            DialogResult choice = MessageBox.Show(message, "学习建议", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                AddNameToConfig("protect", processName);
                AppendLog("[OK] 已根据学习建议加入保护名单：" + processName);
            }
            else
            {
                preferences.IgnoreSuggestion(new UserPreferenceSuggestion { Type = "保护名单", ProcessName = processName, Count = count });
            }
        }

        private void PromptLearningSuggestions()
        {
            AppConfig config = AppConfig.Load(configPath);
            UserPreferenceSuggestion suggestion = preferences.BuildSuggestions(config)
                .FirstOrDefault(s => string.Equals(s.Type, "强制清理名单", StringComparison.Ordinal));
            if (suggestion == null)
            {
                return;
            }

            string message = suggestion.ProcessName + " " + suggestion.Reason + "。\n\n是否将它加入强制清理名单？";
            DialogResult choice = MessageBox.Show(message, "学习建议", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                AddNameToConfig("force", suggestion.ProcessName);
                AppendLog("[OK] 已根据学习建议加入强制清理名单：" + suggestion.ProcessName);
            }
            else
            {
                preferences.IgnoreSuggestion(suggestion);
            }
        }

        private void AddNameToConfig(string type, string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return;
            }

            AppConfig config = AppConfig.Load(configPath);
            if (string.Equals(type, "protect", StringComparison.Ordinal))
            {
                config.protectedNames = AddUnique(config.protectedNames, processName);
            }
            else if (string.Equals(type, "force", StringComparison.Ordinal))
            {
                config.forceAllowedNames = AddUnique(config.forceAllowedNames, processName);
            }
            AppConfig.Save(configPath, config);
        }

        private string[] AddUnique(string[] names, string processName)
        {
            HashSet<string> set = new HashSet<string>(names ?? new string[0], StringComparer.OrdinalIgnoreCase);
            set.Add(processName.Trim());
            return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private Image GetProcessIcon(string path)
        {
            string key = string.IsNullOrWhiteSpace(path) ? "__default" : path;
            if (iconCache.ContainsKey(key))
            {
                return iconCache[key];
            }

            Image image = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(path))
                    {
                        if (icon != null)
                        {
                            using (Bitmap source = icon.ToBitmap())
                            {
                                image = new Bitmap(source, new Size(20, 20));
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (image == null)
            {
                using (Bitmap source = SystemIcons.Application.ToBitmap())
                {
                    image = new Bitmap(source, new Size(20, 20));
                }
            }

            iconCache[key] = image;
            return image;
        }

        private void AppendLog(string message)
        {
            if (logBox == null && fullLogBox == null)
            {
                return;
            }

            Control invokeTarget = (Control)logBox ?? (Control)fullLogBox;
            if (invokeTarget.InvokeRequired)
            {
                invokeTarget.BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            string normalized = (message ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            foreach (string line in lines)
            {
                logHistory.Add(line ?? "");
                AppendLogLine(logBox, line);
                AppendLogLine(fullLogBox, line);
            }
        }

        private void AppendLogLine(RichTextBox target, string line)
        {
            if (target == null)
            {
                return;
            }

            if (target == logBox && logEmptyLabel != null)
            {
                logEmptyLabel.Visible = false;
                logBox.Visible = true;
            }

            if (target.TextLength > 0)
            {
                target.AppendText(Environment.NewLine);
            }

            target.SelectionStart = target.TextLength;
            target.SelectionLength = 0;
            target.SelectionColor = ResolveLogColor(line ?? "");
            target.AppendText(line ?? "");
            target.SelectionStart = target.TextLength;
            target.ScrollToCaret();
        }

        private Color ResolveLogColor(string line)
        {
            if (line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return danger;
            }

            if (line.IndexOf("[FORCE]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return force;
            }

            if (line.IndexOf("[SKIP]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("保护", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return purple;
            }

            if (line.IndexOf("[OK]", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("[CLEAN EXIT]", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return protect;
            }

            if (line.IndexOf("[INFO]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return muted;
            }

            return text;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                if (!scanInProgress && !cleanupInProgress)
                {
                    Task task = ScanProcessesAsync("快捷键扫描");
                }
                return true;
            }

            if (keyData == (Keys.Control | Keys.Enter))
            {
                if (!cleanupInProgress && !scanInProgress && currentPlan != null)
                {
                    CloseButtonClick(this, EventArgs.Empty);
                }
                return true;
            }

            if (keyData == Keys.Escape)
            {
                if (cleanupInProgress && cleanupCts != null && !cleanupCts.IsCancellationRequested)
                {
                    cleanupCts.Cancel();
                    SetOperationState("cancelling");
                    AppendLog("[INFO] 正在取消清理...");
                }
                return true;
            }

            if (keyData == (Keys.Control | Keys.F))
            {
                if (currentPage == "candidate" && candidateSearchBox != null)
                {
                    candidateSearchBox.Focus();
                    if (candidateSearchBox.Text == (candidateSearchBox.Tag as string ?? ""))
                    {
                        candidateSearchBox.Text = "";
                        candidateSearchBox.ForeColor = text;
                    }
                }
                else if (currentPage == "protected" && protectedSearchBox != null)
                {
                    protectedSearchBox.Focus();
                    if (protectedSearchBox.Text == (protectedSearchBox.Tag as string ?? ""))
                    {
                        protectedSearchBox.Text = "";
                        protectedSearchBox.ForeColor = text;
                    }
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private string TranslateErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "发生了未知错误，请稍后重试。";
            if (message.IndexOf("Access", StringComparison.OrdinalIgnoreCase) >= 0 && message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0)
                return "权限不足，无法操作该进程。请尝试以管理员身份运行。";
            if (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("找不到", StringComparison.OrdinalIgnoreCase) >= 0)
                return "所需的文件或进程未找到，可能已被其他程序关闭。";
            if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("超时", StringComparison.OrdinalIgnoreCase) >= 0)
                return "操作超时，进程可能已无响应。请检查系统状态后重试。";
            return message;
        }

        private void ShowDarkDialog(string title, string message, bool isError)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.Size = new Size(420, 200);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = background;
                dialog.ForeColor = text;
                dialog.ShowInTaskbar = false;

                Label icon = new Label();
                icon.Text = isError ? "\uEA39" : "\uE946";
                icon.Font = new Font("Segoe MDL2 Assets", 24F);
                icon.ForeColor = isError ? danger : primary;
                icon.BackColor = Color.Transparent;
                icon.Location = new Point(20, 24);
                icon.AutoSize = true;
                dialog.Controls.Add(icon);

                Label titleLabel = new Label();
                titleLabel.Text = title;
                titleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
                titleLabel.ForeColor = titleText;
                titleLabel.BackColor = Color.Transparent;
                titleLabel.Location = new Point(64, 24);
                titleLabel.AutoSize = true;
                dialog.Controls.Add(titleLabel);

                Label msgLabel = new Label();
                msgLabel.Text = message;
                msgLabel.Font = new Font(Font.FontFamily, 9.5F);
                msgLabel.ForeColor = text;
                msgLabel.BackColor = Color.Transparent;
                msgLabel.Location = new Point(64, 56);
                msgLabel.MaximumSize = new Size(320, 0);
                msgLabel.AutoSize = true;
                dialog.Controls.Add(msgLabel);

                ModernButton okButton = new ModernButton();
                okButton.Text = "确定";
                okButton.Width = 90;
                okButton.Height = 34;
                okButton.Radius = 6;
                okButton.BaseColor = primary;
                okButton.HoverColor = primaryHover;
                okButton.PressedColor = primary;
                okButton.TextColor = Color.White;
                okButton.BorderColor = buttonBorder;
                okButton.DrawBorder = false;
                okButton.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
                okButton.Location = new Point(300, 120);
                okButton.Click += delegate { dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                dialog.Controls.Add(okButton);

                dialog.AcceptButton = okButton;
                dialog.ShowDialog(this);
            }
        }

        private bool ShowDarkConfirm(string title, string message)
        {
            bool confirmed = false;
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.Size = new Size(440, 220);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = background;
                dialog.ForeColor = text;
                dialog.ShowInTaskbar = false;

                Label icon = new Label();
                icon.Text = "\uE7BA";
                icon.Font = new Font("Segoe MDL2 Assets", 24F);
                icon.ForeColor = force;
                icon.BackColor = Color.Transparent;
                icon.Location = new Point(20, 24);
                icon.AutoSize = true;
                dialog.Controls.Add(icon);

                Label titleLabel = new Label();
                titleLabel.Text = title;
                titleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
                titleLabel.ForeColor = titleText;
                titleLabel.BackColor = Color.Transparent;
                titleLabel.Location = new Point(64, 24);
                titleLabel.AutoSize = true;
                dialog.Controls.Add(titleLabel);

                Label msgLabel = new Label();
                msgLabel.Text = message;
                msgLabel.Font = new Font(Font.FontFamily, 9.5F);
                msgLabel.ForeColor = text;
                msgLabel.BackColor = Color.Transparent;
                msgLabel.Location = new Point(64, 56);
                msgLabel.MaximumSize = new Size(340, 0);
                msgLabel.AutoSize = true;
                dialog.Controls.Add(msgLabel);

                ModernButton yesBtn = new ModernButton();
                yesBtn.Text = "确认清理";
                yesBtn.Width = 100;
                yesBtn.Height = 34;
                yesBtn.Radius = 6;
                yesBtn.BaseColor = danger;
                yesBtn.HoverColor = dangerHover;
                yesBtn.PressedColor = danger;
                yesBtn.TextColor = Color.White;
                yesBtn.DrawBorder = false;
                yesBtn.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
                yesBtn.Location = new Point(220, 148);
                yesBtn.Click += delegate { confirmed = true; dialog.Close(); };
                dialog.Controls.Add(yesBtn);

                ModernButton noBtn = new ModernButton();
                noBtn.Text = "取消";
                noBtn.Width = 80;
                noBtn.Height = 34;
                noBtn.Radius = 6;
                noBtn.BaseColor = card;
                noBtn.HoverColor = rowHover;
                noBtn.PressedColor = secondaryPanel;
                noBtn.TextColor = text;
                noBtn.DrawBorder = true;
                noBtn.BorderColor = buttonBorder;
                noBtn.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
                noBtn.Location = new Point(330, 148);
                noBtn.Click += delegate { dialog.Close(); };
                dialog.Controls.Add(noBtn);

                dialog.ShowDialog(this);
            }
            return confirmed;
        }

        private void MainFormShown(object sender, EventArgs e)
        {
            if (preferences.History.records == null || preferences.History.records.Count == 0)
            {
                ShowOnboarding();
            }
        }

        private void ShowOnboarding()
        {
            onboardingOverlay = new Panel();
            onboardingOverlay.Dock = DockStyle.Fill;
            onboardingOverlay.BackColor = Color.FromArgb(200, 0, 0, 0);
            onboardingOverlay.BringToFront();

            RoundedPanel cardPanel = new RoundedPanel();
            cardPanel.Size = new Size(520, 380);
            cardPanel.FillColor = card;
            cardPanel.BorderColor = border;
            cardPanel.Radius = 12;
            cardPanel.Padding = new Padding(32);
            cardPanel.Anchor = AnchorStyles.None;
            onboardingOverlay.Controls.Add(cardPanel);

            onboardingOverlay.Resize += delegate
            {
                cardPanel.Left = (onboardingOverlay.Width - cardPanel.Width) / 2;
                cardPanel.Top = (onboardingOverlay.Height - cardPanel.Height) / 2;
            };
            cardPanel.Left = (onboardingOverlay.Width > 0 ? onboardingOverlay.Width : Width) / 2 - cardPanel.Width / 2;
            cardPanel.Top = (onboardingOverlay.Height > 0 ? onboardingOverlay.Height : Height) / 2 - cardPanel.Height / 2;

            Label welcomeTitle = new Label();
            welcomeTitle.Text = "欢迎使用一键关闭后台软件";
            welcomeTitle.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            welcomeTitle.ForeColor = titleText;
            welcomeTitle.BackColor = Color.Transparent;
            welcomeTitle.Location = new Point(32, 24);
            welcomeTitle.AutoSize = true;
            cardPanel.Controls.Add(welcomeTitle);

            Label welcomeDesc = new Label();
            welcomeDesc.Text = "快速了解三步工作流，关机前轻松清理后台。";
            welcomeDesc.Font = new Font(Font.FontFamily, 10F);
            welcomeDesc.ForeColor = muted;
            welcomeDesc.BackColor = Color.Transparent;
            welcomeDesc.Location = new Point(32, 56);
            welcomeDesc.AutoSize = true;
            cardPanel.Controls.Add(welcomeDesc);

            string[] steps = new string[]
            {
                "① 扫描 — 点击「重新扫描」或按 F5，自动发现后台进程",
                "② 确认 — 查看候选进程列表，右键可移除或加入保护名单",
                "③ 清理 — 点击「一键清理」或按 Ctrl+Enter，三阶段温和关闭"
            };

            int y = 100;
            foreach (string step in steps)
            {
                RoundedPanel stepCard = new RoundedPanel();
                stepCard.Location = new Point(32, y);
                stepCard.Size = new Size(440, 52);
                stepCard.FillColor = secondaryPanel;
                stepCard.BorderColor = border;
                stepCard.Radius = 6;
                stepCard.BackColor = Color.Transparent;

                Label stepLabel = new Label();
                stepLabel.Text = step;
                stepLabel.Font = new Font(Font.FontFamily, 10.5F);
                stepLabel.ForeColor = text;
                stepLabel.BackColor = Color.Transparent;
                stepLabel.Location = new Point(16, 14);
                stepLabel.AutoSize = true;
                stepCard.Controls.Add(stepLabel);

                cardPanel.Controls.Add(stepCard);
                y += 62;
            }

            ModernButton startBtn = new ModernButton();
            startBtn.Text = "开始使用";
            startBtn.Width = 120;
            startBtn.Height = 38;
            startBtn.Radius = 6;
            startBtn.BaseColor = primary;
            startBtn.HoverColor = primaryHover;
            startBtn.PressedColor = primary;
            startBtn.TextColor = Color.White;
            startBtn.BorderColor = buttonBorder;
            startBtn.DrawBorder = false;
            startBtn.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            startBtn.Location = new Point(360, 310);
            startBtn.Click += delegate
            {
                Controls.Remove(onboardingOverlay);
                onboardingOverlay.Dispose();
                onboardingOverlay = null;
            };
            cardPanel.Controls.Add(startBtn);

            Label shortcutHint = new Label();
            shortcutHint.Text = "快捷键：F5 扫描 · Ctrl+Enter 清理 · Esc 取消 · Ctrl+F 搜索";
            shortcutHint.Font = new Font(Font.FontFamily, 8.5F);
            shortcutHint.ForeColor = muted;
            shortcutHint.BackColor = Color.Transparent;
            shortcutHint.Location = new Point(32, 320);
            shortcutHint.AutoSize = true;
            cardPanel.Controls.Add(shortcutHint);

            Controls.Add(onboardingOverlay);
        }
    }
}
