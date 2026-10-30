using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Kingdom_Hearts_Launcher
{
    public partial class LauncherConfig : Window
    {
        private readonly LauncherSettings settings;
        private readonly GamepadInput gamepad;

        public LauncherSettings Result { get; private set; }

        public LauncherConfig(LauncherSettings current, GamepadInput gamepad)
        {
            InitializeComponent();
            MaxHeight = SystemParameters.WorkArea.Height * 0.95;

            this.gamepad = gamepad;
            gamepad.ActionRaised += OnGamepadAction;
            Closed += (s, e) => gamepad.ActionRaised -= OnGamepadAction;

            settings = current.Clone();

            PresetsComboBox.ItemsSource = GameCatalog.Presets;
            PresetsComboBox.SelectedItem = settings.SelectedOrder;
            if (PresetsComboBox.SelectedItem == null)
            {
                PresetsComboBox.SelectedIndex = 0;
            }

            GamesDirBox.Text = settings.ParentDirectory;
            SkipMoviesCheckBox.IsChecked = settings.SkipCopyrightScreenOnMovies;
            SkipKH1CheckBox.IsChecked = settings.SkipCopyrightScreenOnKH1;
            ControllerCheckBox.IsChecked = settings.ControllerEnabled;
            DaysNativeBox.Text = settings.DaysNativePath;
            UseNativeDaysCheckBox.IsChecked = settings.UseNativeDays;
            MelonMixBox.Text = settings.MelonMixPath;
            UseMelonMixOnDaysCheckBox.IsChecked = settings.UseMelonMixOnDays;
            UseMelonMixOnRecodedCheckBox.IsChecked = settings.UseMelonMixOnRecoded;
            EmulatorBox.Text = settings.EmulatorPath;
            RomBox.Text = settings.RomPath;
            MelodyOfMemoryBox.Text = settings.MelodyOfMemoryPath;
            ComInsteadOfRecomCheckBox.IsChecked = settings.ComInsteadOfRecom;

            UpdateState();
        }

        private void UpdateState()
        {
            DetectedText.Text = string.Join("\n", GameCatalog.Collections.Select(c =>
                (GameCatalog.FindCollectionDir(GamesDirBox.Text.Trim(), c) != null ? "✓ " : "✗ ") + "KINGDOM HEARTS " + c.Name));

            string daysExe = GameLauncher.FindDaysNativeExe(DaysNativeBox.Text.Trim());
            DaysNativeStatus.Text = daysExe != null ? "Found: " + daysExe : "Not found";
            UseNativeDaysCheckBox.IsEnabled = daysExe != null;
            if (daysExe == null)
            {
                UseNativeDaysCheckBox.IsChecked = false;
            }

            bool melon = File.Exists(MelonMixBox.Text);
            UseMelonMixOnDaysCheckBox.IsEnabled = melon;
            UseMelonMixOnRecodedCheckBox.IsEnabled = melon;
            if (!melon)
            {
                UseMelonMixOnDaysCheckBox.IsChecked = false;
                UseMelonMixOnRecodedCheckBox.IsChecked = false;
            }

            bool gba = File.Exists(EmulatorBox.Text) && File.Exists(RomBox.Text);
            ComInsteadOfRecomCheckBox.IsEnabled = gba;
            if (!gba)
            {
                ComInsteadOfRecomCheckBox.IsChecked = false;
            }
        }

        private void Paths_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateState();
        }

        private void BrowseGamesDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder that contains your KINGDOM HEARTS game folders";
                if (Directory.Exists(GamesDirBox.Text))
                {
                    dialog.SelectedPath = GamesDirBox.Text;
                }
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    GamesDirBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseDaysNative_Click(object sender, RoutedEventArgs e)
        {
            string folder = Directory.Exists(DaysNativeBox.Text) ? DaysNativeBox.Text : LauncherSettings.DefaultDaysNativePath;
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select the 358/2 Days executable",
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(folder) ? folder : null
            };
            if (dialog.ShowDialog(this) == true)
            {
                DaysNativeBox.Text = dialog.FileName;
            }
        }

        private void BrowseMelonMix_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                OpenFileDialog dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
                if (dialog.ShowDialog(this) != true)
                {
                    break;
                }

                string name = Path.GetFileName(dialog.FileName);
                if (name.Contains("khDaysMM") || name.Contains("MelonMix"))
                {
                    MelonMixBox.Text = dialog.FileName;
                    break;
                }
                MessageBox.Show(this, "Please select the khDaysMM or MelonMix executable.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateState();
        }

        private void BrowseEmulator_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == true)
            {
                MessageBox.Show(this, "Keep in mind this application does not verify whether what you selected is a valid GBA emulator.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                EmulatorBox.Text = dialog.FileName;
                UpdateState();
            }
        }

        private void BrowseMelodyOfMemory_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select the Melody of Memory executable",
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                MelodyOfMemoryBox.Text = dialog.FileName;
            }
        }

        private void AddToSteam_Click(object sender, RoutedEventArgs e)
        {
            SteamPrompt prompt = new SteamPrompt(gamepad) { Owner = this };
            prompt.ShowDialog();
            if (prompt.Result != null)
            {
                SteamStatus.Text = prompt.Result.Message;
                SteamStatus.Foreground = prompt.Result.Success ? Brushes.ForestGreen : Brushes.Firebrick;
            }
        }

        // Set when the launcher was copied to a new folder; the main window then restarts from there.
        public string MovedExe { get; private set; }

        private void MoveLauncher_Click(object sender, RoutedEventArgs e)
        {
            MovePrompt prompt = new MovePrompt(gamepad, settings, LauncherMover.SuggestedFolder()) { Owner = this };
            if (prompt.ShowDialog() == true)
            {
                MovedExe = prompt.MovedExe;
                DialogResult = false;
            }
        }

        private FrameworkElement[] NavigableItems
        {
            get
            {
                return new FrameworkElement[]
                {
                    PresetsComboBox, SkipMoviesCheckBox, SkipKH1CheckBox, ControllerCheckBox,
                    UseNativeDaysCheckBox, UseMelonMixOnDaysCheckBox, UseMelonMixOnRecodedCheckBox,
                    ComInsteadOfRecomCheckBox, AddToSteamButton, MoveLauncherButton, SaveButton, CancelButton
                };
            }
        }

        // Controller navigation covers the options and buttons; picking paths still needs mouse and keyboard.
        private void OnGamepadAction(PadAction action)
        {
            if (!IsActive)
            {
                return;
            }

            switch (action)
            {
                case PadAction.Up:
                case PadAction.Down:
                    Move(action == PadAction.Down ? 1 : -1);
                    break;
                case PadAction.Confirm:
                    ActivateFocused();
                    break;
                case PadAction.Cancel:
                    if (PresetsComboBox.IsDropDownOpen)
                    {
                        PresetsComboBox.IsDropDownOpen = false;
                    }
                    else
                    {
                        DialogResult = false;
                    }
                    break;
            }
        }

        private void Move(int step)
        {
            if (PresetsComboBox.IsDropDownOpen)
            {
                PresetsComboBox.SelectedIndex = Math.Max(0, Math.Min(PresetsComboBox.Items.Count - 1, PresetsComboBox.SelectedIndex + step));
                return;
            }

            List<FrameworkElement> items = NavigableItems.Where(i => i.IsEnabled).ToList();
            int current = items.FindIndex(i => i.IsKeyboardFocusWithin);
            int next = current < 0 ? (step > 0 ? 0 : items.Count - 1) : (current + step + items.Count) % items.Count;
            items[next].Focus();
            items[next].BringIntoView();
        }

        private void ActivateFocused()
        {
            FrameworkElement focused = NavigableItems.FirstOrDefault(i => i.IsKeyboardFocusWithin);
            if (focused == PresetsComboBox)
            {
                PresetsComboBox.IsDropDownOpen = !PresetsComboBox.IsDropDownOpen;
            }
            else if (focused == CancelButton)
            {
                DialogResult = false;
            }
            else if (focused is CheckBox)
            {
                CheckBox box = (CheckBox)focused;
                box.IsChecked = box.IsChecked != true;
            }
            else if (focused is Button)
            {
                focused.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
        }

        private void BrowseRom_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == true)
            {
                MessageBox.Show(this, "Keep in mind this application does not verify whether what you selected is a valid Chain of Memories ROM.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                RomBox.Text = dialog.FileName;
                UpdateState();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            settings.ParentDirectory = GamesDirBox.Text.Trim();
            settings.SelectedOrder = (string)PresetsComboBox.SelectedItem;
            settings.Order = GameCatalog.BuildOrder(settings.SelectedOrder);
            settings.SkipCopyrightScreenOnMovies = SkipMoviesCheckBox.IsChecked ?? false;
            settings.SkipCopyrightScreenOnKH1 = SkipKH1CheckBox.IsChecked ?? false;
            settings.ControllerEnabled = ControllerCheckBox.IsChecked ?? false;
            settings.DaysNativePath = DaysNativeBox.Text.Trim();
            settings.UseNativeDays = UseNativeDaysCheckBox.IsChecked ?? false;
            settings.MelonMixPath = MelonMixBox.Text;
            settings.UseMelonMixOnDays = UseMelonMixOnDaysCheckBox.IsChecked ?? false;
            settings.UseMelonMixOnRecoded = UseMelonMixOnRecodedCheckBox.IsChecked ?? false;
            settings.EmulatorPath = EmulatorBox.Text;
            settings.RomPath = RomBox.Text;
            settings.MelodyOfMemoryPath = MelodyOfMemoryBox.Text.Trim();
            settings.ComInsteadOfRecom = ComInsteadOfRecomCheckBox.IsChecked ?? false;

            Result = settings.Clone();
            DialogResult = true;
        }
    }
}
