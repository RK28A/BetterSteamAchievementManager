// Altered source: Steam-inspired library shell for BetterSteamAchievementManager.
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SAM.UI;

namespace SAM.Picker
{
    partial class GamePicker
    {
        private IContainer components;
        private MyListView _GameListView;
        private ImageList _LogoImageList;
        private Timer _CallbackTimer;
        private ToolStrip _PickerToolStrip;
        private ToolStripButton _RefreshGamesButton, _AddGameButton;
        private ToolStripTextBox _AddGameTextBox, _SearchGameTextBox;
        private ToolStripDropDownButton _FilterDropDownButton;
        private ToolStripMenuItem _FilterGamesMenuItem, _FilterJunkMenuItem, _FilterDemosMenuItem, _FilterModsMenuItem;
        private StatusStrip _PickerStatusStrip;
        private ToolStripStatusLabel _DownloadStatusLabel, _PickerStatusLabel;
        private BackgroundWorker _ListWorker;
        private Panel _GameHost;
        private Label _LibraryCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();
            SuspendLayout();
            Text = "BetterSteamAchievementManager";
            Icon = (Icon)new ComponentResourceManager(typeof(GamePicker)).GetObject("$this.Icon");
            ClientSize = new Size(1400, 860);
            MinimumSize = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10);
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 2, FixedPanel = FixedPanel.Panel1, BackColor = SteamTheme.Surface };
            split.Size = ClientSize;
            split.SplitterDistance = 260;
            split.Panel1MinSize = 245;
            split.Panel2MinSize = 680;
            var libraryTop = new Panel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(16, 10, 12, 8) };
            _LibraryCount = SteamTheme.Label("VOTRE BIBLIOTHÈQUE", 10, SteamTheme.Muted);
            _LibraryCount.Dock = DockStyle.Top;
            var search = new ToolStrip { Dock = DockStyle.Bottom, AutoSize = false, Height = 40 };
            _SearchGameTextBox = new ToolStripTextBox { AutoSize = false, Width = 212, ToolTipText = "Rechercher dans la bibliothèque", AccessibleName = "Rechercher un jeu" };
            _SearchGameTextBox.TextChanged += OnFilterUpdate;
            search.Items.Add(_SearchGameTextBox);
            _LibraryFilters = new ToolStrip { Dock = DockStyle.Bottom, Height = 34 };
            libraryTop.Controls.Add(_LibraryFilters);
            libraryTop.Controls.Add(search);
            libraryTop.Controls.Add(_LibraryCount);
            _LogoImageList = new ImageList(components) { ImageSize = new Size(40, 58), ColorDepth = ColorDepth.Depth32Bit };
            _GameListView = new MyListView { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None,
                BorderStyle = BorderStyle.None, FullRowSelect = true, MultiSelect = false, HideSelection = false,
                VirtualMode = true, OwnerDraw = true, SmallImageList = _LogoImageList, AccessibleName = "Bibliothèque de jeux" };
            _GameListView.Columns.Add("Jeux", 265);
            _GameListView.RetrieveVirtualItem += OnGameListViewRetrieveVirtualItem;
            _GameListView.SearchForVirtualItem += OnGameListViewSearchForVirtualItem;
            _GameListView.DrawItem += OnGameListViewDrawItem;
            _GameListView.DrawSubItem += (_, e) => { };
            _GameListView.ItemSelectionChanged += (_, e) => { if (e.IsSelected) OpenGame(e.ItemIndex); };
            _GameListView.ItemActivate += OnActivateGame;
            _GameListView.MouseClick += (_, e) => { var item = _GameListView.HitTest(e.Location).Item; if (item != null) OpenGame(item.Index); };
            _GameListView.SizeChanged += (_, _) => _GameListView.Columns[0].Width = Math.Max(100, _GameListView.ClientSize.Width - 2);
            split.Panel1.Controls.Add(_GameListView);
            split.Panel1.Controls.Add(libraryTop);
            _GameHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
            var empty = new HeroPanel { Dock = DockStyle.Fill, Padding = new Padding(50, 75, 30, 30) };
            var welcome = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            welcome.Controls.Add(SteamTheme.Label("BETTER ACHIEVEMENTS", 11, SteamTheme.Accent));
            welcome.Controls.Add(SteamTheme.Label("Votre bibliothèque.\nÀ votre rythme.", 28));
            welcome.Controls.Add(SteamTheme.Label("Choisissez un jeu dans votre bibliothèque pour afficher ses succès.", 11, SteamTheme.Muted));
            welcome.Controls.Add(SteamTheme.Label("Succès   /   Panier   /   GameTime", 12, SteamTheme.Accent));
            empty.Controls.Add(welcome);
            _GameHost.Controls.Add(empty);
            split.Panel2.Controls.Add(_GameHost);
            _PickerToolStrip = new ToolStrip { Dock = DockStyle.Bottom };
            _RefreshGamesButton = new ToolStripButton("Actualiser la bibliothèque", null, OnRefresh);
            _AddGameTextBox = new ToolStripTextBox { AutoSize = false, Width = 95, ToolTipText = "App ID Steam" };
            _AddGameButton = new ToolStripButton("Ajouter un jeu", null, OnAddGame);
            _FilterDropDownButton = new ToolStripDropDownButton("Paramètres / Filtres");
            ToolStripMenuItem Filter(string title, bool selected)
            {
                var item = new ToolStripMenuItem(title) { CheckOnClick = true, Checked = selected };
                item.CheckedChanged += OnFilterUpdate;
                return item;
            }
            _FilterGamesMenuItem = Filter("Jeux", true);
            _FilterDemosMenuItem = Filter("Démos", false);
            _FilterModsMenuItem = Filter("Mods", false);
            _FilterJunkMenuItem = Filter("Autres", false);
            _FilterDropDownButton.DropDownItems.AddRange(new ToolStripItem[] { _FilterGamesMenuItem, _FilterDemosMenuItem, _FilterModsMenuItem, _FilterJunkMenuItem });
            var about = new ToolStripMenuItem("À propos");
            about.Click += (_, _) => MessageBox.Show(this, "BetterSteamAchievementManager\nInterface bibliothèque · Version 8.3\n\nBasé sur Steam Achievement Manager de gibbed.\nLicence zlib et attributions dans la distribution.", "À propos");
            _FilterDropDownButton.DropDownItems.Add(new ToolStripSeparator());
            _FilterDropDownButton.DropDownItems.Add(about);
            _PickerToolStrip.Items.AddRange(new ToolStripItem[] { _RefreshGamesButton, new ToolStripSeparator(), new ToolStripLabel("App ID"), _AddGameTextBox, _AddGameButton, new ToolStripSeparator(), _FilterDropDownButton });
            _PickerStatusStrip = new StatusStrip();
            _PickerStatusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _DownloadStatusLabel = new ToolStripStatusLabel { Visible = false };
            _PickerStatusStrip.Items.AddRange(new ToolStripItem[] { _PickerStatusLabel, _DownloadStatusLabel });
            _ListWorker = new BackgroundWorker();
            _ListWorker.DoWork += DoDownloadList;
            _ListWorker.RunWorkerCompleted += OnDownloadList;
            _CallbackTimer = new Timer(components) { Interval = 100, Enabled = true };
            _CallbackTimer.Tick += OnTimer;
            Controls.Add(split);
            Controls.Add(_PickerToolStrip);
            Controls.Add(_PickerStatusStrip);
            SteamTheme.Apply(this);
            welcome.BackColor = Color.Transparent;
            foreach (Control child in welcome.Controls) child.BackColor = Color.Transparent;
            _GameListView.BackColor = SteamTheme.Sidebar;
            ResumeLayout(true);
        }
    }
}
