using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kingdom_Hearts_Launcher
{
    public partial class MainWindow : Window
    {
        private static readonly Dictionary<string, string> LaunchFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "-kh1", "kh1" },
            { "-recom", "recom" },
            { "-days", "days" },
            { "-kh2", "kh2" },
            { "-bbs", "bbs" },
            { "-recoded", "recoded" },
            { "-ddd", "ddd" },
            { "-bbs02", "bbs02" },
            { "-backcover", "backcover" },
            { "-kh3", "kh3" },
            { "-mom", "mom" },
        };

        private class GameItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Detail { get; set; }
            public bool Installed { get; set; }
        }

        private LauncherSettings settings;
        private readonly GamepadInput gamepad = new GamepadInput();

        public bool LaunchedFromArgs { get; private set; }

        public MainWindow() : this(new string[0])
        {
        }

        public MainWindow(string[] args)
        {
            InitializeComponent();
            MaxHeight = SystemParameters.WorkArea.Height;

            bool readFailed;
            settings = LauncherSettings.Load(out readFailed);
            if (readFailed)
            {
                MessageBox.Show("Unable to read config file, settings will be reset.");
            }
            if (string.IsNullOrEmpty(settings.ParentDirectory))
            {
                settings.ParentDirectory = SteamLocator.GuessGamesDirectory();
            }

            RefreshList(settings.SelectedGame);

            // Shared with the config window; each window ignores input unless it is the active one.
            gamepad.CanRaise = () => Application.Current != null && Application.Current.Windows.OfType<Window>().Any(w => w.IsActive);
            gamepad.ActionRaised += OnGamepadAction;
            ApplyControllerSetting();
            Closed += (s, e) => gamepad.Dispose();
            Loaded += MainWindow_Loaded;

            LaunchedFromArgs = TryLaunchFromArgs(args);
        }

        private bool TryLaunchFromArgs(string[] args)
        {
            if (args == null)
            {
                return false;
            }

            foreach (string arg in args)
            {
                string id;
                if (LaunchFlags.TryGetValue(arg, out id))
                {
                    return Launch(GameCatalog.Find(id));
                }
                if (arg.Equals("-skipLauncher", StringComparison.OrdinalIgnoreCase))
                {
                    return Launch(GameCatalog.Find(settings.SelectedGame));
                }
            }
            return false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GameList.Focus();
            if (string.IsNullOrEmpty(settings.ParentDirectory) || !Directory.Exists(settings.ParentDirectory))
            {
                MessageBox.Show(this, "Choose the folder that contains your KINGDOM HEARTS games (for example Steam's steamapps\\common folder).", "Games folder not set");
                OpenConfig();
            }

            if (OfferMove())
            {
                return;
            }
            OfferSteamShortcut();
        }

        // Asked once, on first run, unless the launcher already sits in a steamapps\common folder.
        private bool OfferMove()
        {
            if (settings.MovePromptShown)
            {
                return false;
            }

            settings.MovePromptShown = true;
            SaveSettings();
            string suggested = LauncherMover.SuggestedFolder();
            if (LauncherMover.IsInSteamLibrary || suggested == null)
            {
                return false;
            }

            MovePrompt prompt = new MovePrompt(gamepad, settings, suggested) { Owner = this };
            prompt.ShowDialog();
            return RelaunchIfMoved(prompt.MovedExe);
        }

        private bool RelaunchIfMoved(string movedExe)
        {
            if (movedExe == null)
            {
                return false;
            }

            try
            {
                LauncherMover.Relaunch(movedExe);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(this, "The launcher was copied to " + movedExe + " but could not be started: " + ex.Message);
                return false;
            }
            Close();
            return true;
        }

        // Asked once, on first run, so regular users don't have to find the option in the config.
        private void OfferSteamShortcut()
        {
            if (settings.SteamPromptShown)
            {
                return;
            }

            settings.SteamPromptShown = true;
            SaveSettings();
            if (SteamShortcuts.IsSteamInstalled && !SteamShortcuts.IsLauncherAdded())
            {
                new SteamPrompt(gamepad) { Owner = this }.ShowDialog();
            }
            GameList.Focus();
        }

        private void RefreshList(string selectedId)
        {
            GameList.ItemsSource = settings.Order.Select(id =>
            {
                GameDef game = GameCatalog.Find(id);
                LaunchPlan plan = GameLauncher.Plan(game, settings);
                return new GameItem
                {
                    Id = id,
                    Title = GameLauncher.GetTitle(game, settings),
                    Installed = plan.CanLaunch,
                    Detail = plan.CanLaunch ? plan.Mode : plan.Error
                };
            }).ToList();

            GameList.SelectedItem = GameList.Items.Cast<GameItem>().FirstOrDefault(i => i.Id == selectedId)
                ?? GameList.Items.Cast<GameItem>().FirstOrDefault();
        }

        private GameDef SelectedGame
        {
            get { return GameCatalog.Find((GameList.SelectedItem as GameItem)?.Id); }
        }

        private void ApplyControllerSetting()
        {
            gamepad.Enabled = settings.ControllerEnabled;
            ControllerHint.Visibility = settings.ControllerEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnGamepadAction(PadAction action)
        {
            int count = GameList.Items.Count;
            if (!IsActive || count == 0 || action == PadAction.Cancel)
            {
                return;
            }

            if (action == PadAction.Confirm)
            {
                LaunchSelected();
                return;
            }
            if (action == PadAction.Config)
            {
                OpenConfig();
                return;
            }

            int step = action == PadAction.Down ? 1 : -1;
            GameList.SelectedIndex = (GameList.SelectedIndex + step + count) % count;
            GameList.ScrollIntoView(GameList.SelectedItem);
            (GameList.ItemContainerGenerator.ContainerFromIndex(GameList.SelectedIndex) as ListBoxItem)?.Focus();
        }

        private void GameList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is GameItem)
            {
                LaunchSelected();
            }
        }

        private void LauncherConfigButton_Click(object sender, RoutedEventArgs e)
        {
            OpenConfig();
        }

        private void OpenConfig()
        {
            LauncherConfig configWindow = new LauncherConfig(settings, gamepad) { Owner = this };
            bool saved = configWindow.ShowDialog() == true;
            if (RelaunchIfMoved(configWindow.MovedExe))
            {
                return;
            }
            if (saved)
            {
                string selected = (GameList.SelectedItem as GameItem)?.Id;
                settings = configWindow.Result;
                settings.SelectedGame = selected;
                SaveSettings();
                RefreshList(selected);
                ApplyControllerSetting();
            }
            GameList.Focus();
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            LaunchSelected();
        }

        private void LaunchSelected()
        {
            if (Launch(SelectedGame))
            {
                Close();
            }
        }

        private bool Launch(GameDef game)
        {
            LaunchPlan plan = GameLauncher.Plan(game, settings);
            if (!plan.CanLaunch)
            {
                MessageBox.Show(this, plan.Error, GameLauncher.GetTitle(game, settings));
                return false;
            }

            try
            {
                GameLauncher.Start(plan);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Could not start the game: " + ex.Message, GameLauncher.GetTitle(game, settings));
                return false;
            }

            settings.SelectedGame = game.Id;
            SaveSettings();
            return true;
        }

        private void SaveSettings()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Could not save settings: " + ex.Message);
            }
        }
    }
}
