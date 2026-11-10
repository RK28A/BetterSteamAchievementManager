using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private JsonStore<OrderCart> _OrderStore;
        private JsonStore<OrderCommand> _Commands;
        private readonly OrderRunner _Runner = new();
        private readonly Stopwatch _OrderClock = Stopwatch.StartNew();
        private readonly Stopwatch _CommandClock = new();
        private Panel _OrderPanel;
        private OrderRows _OrderRows;
        private RangeEditor _SuccessRange, _GameRange;
        private CheckBox _BetweenGames;
        private Label _OrderStatus, _OrderCount, _OrderGameName;
        private Button _OrderButton, _RunOrder, _StopOrder;
        private FlowLayoutPanel _OrderActions;
        private bool _BindingOrder;
        private string _RowSignature = "", _RunId, _PendingCommand;
        private uint _TimingGameId;
        private DateTime _LastLease;
        private string _OrderMessage = "Ajoutez des succès depuis vos jeux, puis choisissez leurs délais.";
        private string _CartPath, _CommandPath;
        private System.Threading.Mutex _OrderRunGate;
        private bool _OwnOrderRunGate;

        private void InitializeOrderCart()
        {
            string session = Path.Combine(Path.GetTempPath(), "BSAM-Session-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));
            _CartPath = _Demo ? Path.Combine(session, "demo-cart.json") : OrderPaths.Cart;
            _CommandPath = Path.Combine(session, "command.json");
            _OrderStore = new JsonStore<OrderCart>(_CartPath);
            _Commands = new JsonStore<OrderCommand>(_CommandPath);
            _OrderButton = new Button { Text = "Achievement Order  ·  0", AutoSize = true, Dock = DockStyle.Right, Width = 225, FlatStyle = FlatStyle.Flat, BackColor = SteamTheme.Surface, ForeColor = SteamTheme.Accent, Padding = new Padding(12, 4, 12, 4) };
            _OrderButton.FlatAppearance.BorderSize = 0;
            _OrderButton.Click += (_, _) => { _OrderPanel.BringToFront(); RefreshOrderCart(); };
            // The button lives in the library navigation strip, below the custom window caption.
            var bar = new Panel { Dock = DockStyle.Top, BackColor = SteamTheme.Background, Height = 40, Padding = new Padding(16, 2, 20, 2) };
            bar.Controls.Add(_OrderButton);
            bar.Controls.Add(SteamTheme.Label("VOS JEUX  /  VOS OBJECTIFS", 10, SteamTheme.Muted));
            Controls.Add(bar); bar.SendToBack();
            // Preserve the caption as the uppermost reserved strip.
            var caption = Controls.Cast<Control>().FirstOrDefault(c => c.AccessibleName == "Barre de fenêtre"); caption?.SendToBack();
            _OrderPanel = new Panel { Dock = DockStyle.Fill, BackColor = SteamTheme.Background };
            var header = new HeroPanel { Dock = DockStyle.Top, Height = 105, Padding = new Padding(22, 10, 14, 8) };
            var title = SteamTheme.Label("Achievement Order", 24); title.Dock = DockStyle.Top;
            _OrderCount = SteamTheme.Label("Votre panier multi-jeux", 11, SteamTheme.Muted); _OrderCount.Dock = DockStyle.Top;
            header.Controls.Add(_OrderCount); header.Controls.Add(title);
            var body = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(840, 450), FixedPanel = FixedPanel.Panel2, SplitterWidth = 2, BackColor = SteamTheme.Background };
            body.SplitterDistance = 560; body.Panel1MinSize = 260; body.Panel2MinSize = 250;
            _OrderRows = new OrderRows(); body.Panel1.Controls.Add(_OrderRows);
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            _OrderGameName = SteamTheme.Label("Sélectionnez un jeu du panier", 11, SteamTheme.Accent); _OrderGameName.Dock = DockStyle.Top; _OrderGameName.MaximumSize = new Size(250, 60);
            _SuccessRange = new RangeEditor("Entre les succès");
            _BetweenGames = new CheckBox { Text = "Pause entre les jeux", Dock = DockStyle.Top, Height = 36, ForeColor = SteamTheme.Text };
            _GameRange = new RangeEditor("Entre les jeux");
            var hint = SteamTheme.Label("Chaque jeu conserve ses délais.\nLa pause entre jeux s’ajoute au délai\ndu prochain succès.\n\nUn envoi à la fois, avec\nconfirmation de Steam.", 9, SteamTheme.Muted); hint.Dock = DockStyle.Top;
            _OrderGameName.AutoSize = false; _OrderGameName.Height = 44;
            hint.AutoSize = false; hint.Height = 120;
            settings.Controls.Add(_OrderGameName); settings.Controls.Add(_SuccessRange); settings.Controls.Add(_BetweenGames); settings.Controls.Add(_GameRange); settings.Controls.Add(hint);
            void FitSettings()
            {
                int width = Math.Max(160, settings.ClientSize.Width - settings.Padding.Horizontal - 2);
                foreach (Control child in settings.Controls) { child.Dock = DockStyle.None; child.Margin = Padding.Empty; if (child.Width != width) child.Width = width; }
            }
            settings.SizeChanged += (_, _) => FitSettings();
            settings.Layout += (_, _) => FitSettings();
            FitSettings();
            body.Panel2.Controls.Add(settings);
            _OrderActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10, 4, 10, 4), WrapContents = true };
            void Action(string text, Action click)
            {
                var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3) }; button.Click += (_, _) => EditOrder(click); _OrderActions.Controls.Add(button);
            }
            Action("Retirer", RemoveOrderItem);
            Action("Monter", () => MoveOrderItem(-1));
            Action("Descendre", () => MoveOrderItem(1));
            Action("Vider", () => _OrderStore.Update(cart => { OrderPaths.EnsureEditable(cart); cart.Games.Clear(); }));
            _RunOrder = new Button { Text = "Démarrer", AutoSize = true, Margin = new Padding(3) };
            _RunOrder.Click += (_, _) => StartOrder();
            _StopOrder = new Button { Text = "Arrêter", AutoSize = true, Enabled = false, Margin = new Padding(3) };
            _StopOrder.Click += (_, _) => StopOrder();
            var run = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
            run.Controls.Add(_RunOrder); run.Controls.Add(_StopOrder);
            _OrderStatus = SteamTheme.Label(_OrderMessage, 10, SteamTheme.Accent); _OrderStatus.Dock = DockStyle.Bottom; _OrderStatus.MaximumSize = new Size(1100, 0); _OrderStatus.Padding = new Padding(12, 8, 12, 8);
            _OrderPanel.Controls.Add(body); _OrderPanel.Controls.Add(_OrderActions); _OrderPanel.Controls.Add(run); _OrderPanel.Controls.Add(_OrderStatus); _OrderPanel.Controls.Add(header);
            _GameHost.Controls.Add(_OrderPanel); _OrderPanel.SendToBack();
            SteamTheme.Apply(_OrderPanel);
            title.BackColor = _OrderCount.BackColor = Color.Transparent;
            _OrderRows.SelectedIndexChanged += (_, _) => BindTimingGame();
            _SuccessRange.Edited += (_, _) => { if (_BindingOrder) return; EditOrder(() => _OrderStore.Update(cart => { OrderPaths.EnsureEditable(cart); var g = cart.Games.FirstOrDefault(x => x.GameId == _TimingGameId); if (g != null) g.Delay = _SuccessRange.Range; })); };
            _BetweenGames.CheckedChanged += (_, _) => { if (_BindingOrder) return; EditOrder(() => _OrderStore.Update(cart => { OrderPaths.EnsureEditable(cart); cart.BetweenGames = _BetweenGames.Checked; })); };
            _GameRange.Edited += (_, _) => { if (_BindingOrder) return; EditOrder(() => _OrderStore.Update(cart => { OrderPaths.EnsureEditable(cart); cart.GameDelay = _GameRange.Range; })); };
            var timer = new Timer(components) { Interval = 500, Enabled = true };
            timer.Tick += (_, _) => TickOrder();
            FormClosed += (_, _) => { _Runner.Stop(); ReleaseOrderLease(); };
            RefreshOrderCart();
        }

        private void EditOrder(Action edit)
        {
            try { if (_Runner.Running) return; edit(); RefreshOrderCart(); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            { MessageBox.Show(this, e.Message, "Achievement Order"); }
        }
        private void BindTimingGame()
        {
            if (_OrderRows.SelectedItem is not OrderRows.Row row) return;
            _TimingGameId = row.Game.GameId;
            _BindingOrder = true;
            _OrderGameName.Text = row.Game.Name;
            _SuccessRange.Bind(_OrderStore.Read().Games.First(g => g.GameId == _TimingGameId).Delay);
            _BindingOrder = false;
        }
        private void RefreshOrderCart()
        {
            OrderCart cart;
            try { cart = _OrderStore.Read(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { _OrderStatus.Text = e.Message; _RunOrder.Enabled = _OrderActions.Enabled = false; return; }
            _OrderButton.Text = "Achievement Order  ·  " + cart.Count;
            _OrderCount.Text = $"{cart.Games.Count} jeux · {cart.Count} succès · un seul panier";
            string signature = string.Join("|", cart.Games.SelectMany(g => new[] { "game:" + g.GameId }.Concat(g.Achievements.Select(a => a.Id + ":" + a.Status))));
            if (signature != _RowSignature) { _RowSignature = signature; _OrderRows.Bind(cart); if (_OrderRows.SelectedIndex < 0 && _OrderRows.Items.Count > 0) _OrderRows.SelectedIndex = 0; }
            _BindingOrder = true;
            _BetweenGames.Checked = cart.BetweenGames;
            if (!_GameRange.ContainsFocus) _GameRange.Bind(cart.GameDelay);
            _BindingOrder = false;
            bool editable = !cart.IsRunning && !_Runner.Running;
            _OrderActions.Enabled = _SuccessRange.Enabled = _BetweenGames.Enabled = editable;
            _GameRange.Enabled = editable && cart.BetweenGames;
            _RunOrder.Enabled = editable && !_Demo && cart.Games.Any(g => g.Achievements.Any(a => a.Status != "done"));
            _StopOrder.Enabled = _Runner.Running && _Runner.Phase != OrderPhase.Stopping;
        }
        private void RemoveOrderItem()
        {
            if (_OrderRows.SelectedItem is not OrderRows.Row row) return;
            _OrderStore.Update(cart =>
            {
                OrderPaths.EnsureEditable(cart);
                if (row.Achievement == null) cart.Games.RemoveAll(g => g.GameId == row.Game.GameId);
                else cart.Games.First(g => g.GameId == row.Game.GameId).Achievements.RemoveAll(a => a.Id == row.Achievement.Id);
                cart.Games.RemoveAll(g => g.Achievements.Count == 0);
            });
        }
        private void MoveOrderItem(int direction)
        {
            if (_OrderRows.SelectedItem is not OrderRows.Row row) return;
            _OrderStore.Update(cart =>
            {
                OrderPaths.EnsureEditable(cart);
                if (row.Achievement == null)
                { int i = cart.Games.FindIndex(g => g.GameId == row.Game.GameId), to = i + direction; if (to >= 0 && to < cart.Games.Count) { var g = cart.Games[i]; cart.Games.RemoveAt(i); cart.Games.Insert(to, g); } }
                else
                { var list = cart.Games.First(g => g.GameId == row.Game.GameId).Achievements; int i = list.FindIndex(a => a.Id == row.Achievement.Id), to = i + direction; if (to >= 0 && to < list.Count) { var a = list[i]; list.RemoveAt(i); list.Insert(to, a); } }
            });
        }
        private void StartOrder()
        {
            if (_Demo || _Runner.Running) return;
            try
            {
                _OrderRunGate = new System.Threading.Mutex(false, "Local\\BSAM-AchievementOrder-Run");
                try { _OwnOrderRunGate = _OrderRunGate.WaitOne(0); }
                catch (System.Threading.AbandonedMutexException) { _OwnOrderRunGate = true; }
                if (!_OwnOrderRunGate) throw new InvalidOperationException("Une commande s’exécute déjà dans une autre fenêtre.");
                var cart = _OrderStore.Read(); OrderPaths.EnsureEditable(cart);
                foreach (var game in cart.Games) game.Achievements.RemoveAll(a => a.Status == "done");
                cart.Games.RemoveAll(g => g.Achievements.Count == 0);
                _Runner.Start(cart, _OrderClock.Elapsed);
                _RunId = Guid.NewGuid().ToString("N"); _PendingCommand = null;
                _OrderStore.Update(data => { OrderPaths.EnsureEditable(data); data.ActiveRun = _RunId; data.LeaseUntilUtc = DateTime.UtcNow.AddSeconds(15); foreach (var g in data.Games) foreach (var a in g.Achievements) if (a.Status != "done") a.Status = "En attente"; });
                _LastLease = DateTime.UtcNow;
                _OrderMessage = "Commande démarrée.";
                RefreshOrderCart();
            }
            catch (Exception e) { _Runner.Fail(); _OrderMessage = e.Message; ReleaseOrderLease(); RefreshOrderCart(); }
        }
        private void StopOrder()
        {
            _Runner.Stop();
            _OrderMessage = _Runner.Phase == OrderPhase.Stopping ? "Arrêt demandé · attente de l’envoi en cours, aucun succès suivant." : "Commande arrêtée. Le panier est conservé.";
            if (!_Runner.Running) ReleaseOrderLease();
            RefreshOrderCart();
        }
        private void ReleaseOrderLease()
        {
            try
            {
                if (_RunId != null) _OrderStore.Update(cart => { if (cart.ActiveRun == _RunId) { cart.ActiveRun = null; cart.LeaseUntilUtc = DateTime.MinValue; } });
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            finally
            {
                if (_OwnOrderRunGate) { _OrderRunGate.ReleaseMutex(); _OwnOrderRunGate = false; }
                _OrderRunGate?.Dispose(); _OrderRunGate = null; _RunId = null;
            }
        }
        private void TickOrder()
        {
            if (_ClosingLibrary || IsDisposed) return;
            try
            {
                if (_Runner.Running)
                {
                    if ((DateTime.UtcNow - _LastLease).TotalSeconds >= 1)
                    {
                        _OrderStore.Update(cart => { if (cart.ActiveRun != _RunId) throw new InvalidOperationException("Une autre commande a pris la main."); cart.LeaseUntilUtc = DateTime.UtcNow.AddSeconds(15); });
                        _LastLease = DateTime.UtcNow;
                    }
                    if (_PendingCommand != null)
                    {
                        var command = _Commands.Read();
                        if (command.Id == _PendingCommand && command.State != "requested")
                        {
                            bool success = command.State == "done";
                            if (_Runner.Confirm(command.GameId, command.AchievementId, success, _OrderClock.Elapsed))
                            {
                                _OrderStore.Update(cart => { var game = cart.Games.First(g => g.GameId == command.GameId); game.Achievements.First(a => a.Id == command.AchievementId).Status = success ? "done" : "failed"; });
                                _OrderMessage = success ? command.Message : "Commande arrêtée : " + command.Message;
                                _PendingCommand = null;
                            }
                        }
                        else if (_CommandClock.Elapsed > TimeSpan.FromSeconds(120))
                        { _Runner.Fail(); _OrderMessage = "Le jeu ne répond pas. Aucun autre succès ne sera envoyé. Vérifiez sa vue avant de réessayer."; _PendingCommand = null; }
                    }
                    var step = _Runner.Tick(_OrderClock.Elapsed);
                    if (step != null)
                    {
                        EnsureSession(new GameInfo(step.GameId, "normal") { Name = step.GameName }, false);
                        _PendingCommand = Guid.NewGuid().ToString("N");
                        _Commands.Update(command => { command.Id = _PendingCommand; command.RunId = _RunId; command.GameId = step.GameId; command.AchievementId = step.Achievement.Id; command.State = "requested"; command.Message = null; });
                        _CommandClock.Restart();
                        _OrderStore.Update(cart => cart.Games.First(g => g.GameId == step.GameId).Achievements.First(a => a.Id == step.Achievement.Id).Status = "sending");
                    }
                    if (!_Runner.Running) ReleaseOrderLease();
                }
                string timing = _Runner.Running ? _Runner.Phase == OrderPhase.Confirmation || _Runner.Phase == OrderPhase.Stopping ? "Confirmation Steam…" : (_Runner.Phase == OrderPhase.GameDelay ? "Pause entre jeux · " : "Prochain succès · ") + FormatRemaining(_Runner.Deadline - _OrderClock.Elapsed) : _OrderMessage;
                _OrderStatus.Text = $"{_Runner.Completed}/{_Runner.Total} · {timing}";
                RefreshOrderCart();
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            { _Runner.Fail(); _OrderMessage = e.Message; _OrderStatus.Text = e.Message; ReleaseOrderLease(); }
        }
        private static string FormatRemaining(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.Days > 0 ? $"{time.Days} j {time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }
    }
}
