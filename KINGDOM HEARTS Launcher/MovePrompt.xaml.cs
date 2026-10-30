using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Kingdom_Hearts_Launcher
{
    // Offers to move the launcher into a Steam library's steamapps\common folder; used on first run and from Launcher Config.
    public partial class MovePrompt : Window
    {
        private readonly GamepadInput gamepad;
        private readonly LauncherSettings settings;

        // The new exe once the launcher has been copied; null if the user declined.
        public string MovedExe { get; private set; }

        public MovePrompt(GamepadInput gamepad, LauncherSettings settings, string suggestedFolder)
        {
            InitializeComponent();
            this.gamepad = gamepad;
            this.settings = settings;
            gamepad.ActionRaised += OnGamepadAction;
            Closed += (s, e) => gamepad.ActionRaised -= OnGamepadAction;

            FolderBox.Text = suggestedFolder ?? "";
            UpdatePreview();
        }

        private void OnGamepadAction(PadAction action)
        {
            if (!IsActive)
            {
                return;
            }

            if (action == PadAction.Confirm)
            {
                Move_Click(this, null);
            }
            else if (action == PadAction.Cancel)
            {
                Close();
            }
        }

        private void UpdatePreview()
        {
            string folder = FolderBox.Text.Trim();
            PreviewText.Text = folder.Length == 0
                ? "Choose the folder to move it to."
                : "It will be placed in: " + Path.Combine(folder, LauncherMover.FolderName);
            MoveButton.IsEnabled = Directory.Exists(folder);
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void FolderBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the steamapps\\common folder of the Steam library that holds your games";
                if (Directory.Exists(FolderBox.Text))
                {
                    dialog.SelectedPath = FolderBox.Text;
                }
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (!MoveButton.IsEnabled)
            {
                return;
            }

            string error;
            string newExe = LauncherMover.CopyTo(FolderBox.Text.Trim(), settings, out error);
            if (newExe == null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            MovedExe = newExe;
            DialogResult = true;
        }

        private void NotNow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
