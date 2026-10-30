using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Kingdom_Hearts_Launcher
{
    // Offers to add the launcher to Steam; used on first run and from Launcher Config.
    public partial class SteamPrompt : Window
    {
        private readonly GamepadInput gamepad;
        private bool busy;
        private bool done;

        // Null until the user chose to add; then the outcome.
        public SteamShortcuts.Result Result { get; private set; }

        public SteamPrompt(GamepadInput gamepad)
        {
            InitializeComponent();
            this.gamepad = gamepad;
            gamepad.ActionRaised += OnGamepadAction;
            Closed += (s, e) => gamepad.ActionRaised -= OnGamepadAction;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Steam is being closed and edited; don't leave halfway.
            e.Cancel = busy;
            base.OnClosing(e);
        }

        private void OnGamepadAction(PadAction action)
        {
            if (!IsActive || busy)
            {
                return;
            }

            if (action == PadAction.Confirm)
            {
                Primary_Click(this, null);
            }
            else if (action == PadAction.Cancel)
            {
                Close();
            }
        }

        private async void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
            {
                return;
            }
            if (done)
            {
                Close();
                return;
            }

            busy = true;
            PrimaryButton.IsEnabled = false;
            SecondaryButton.IsEnabled = false;
            HintText.Visibility = Visibility.Collapsed;
            ShowStatus("Adding to Steam. This can take a few seconds if Steam has to be closed first...", Brushes.Gray);

            Result = await Task.Run(() => SteamShortcuts.AddLauncher(true));

            busy = false;
            done = true;
            ShowStatus(Result.Message, Result.Success ? Brushes.ForestGreen : Brushes.Firebrick);
            PrimaryButton.Content = "Close";
            PrimaryButton.IsEnabled = true;
            SecondaryButton.Visibility = Visibility.Collapsed;
            HintText.Text = "Controller: A or B to close";
            HintText.Visibility = Visibility.Visible;
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowStatus(string text, Brush color)
        {
            StatusText.Text = text;
            StatusText.Foreground = color;
            StatusText.Visibility = Visibility.Visible;
        }
    }
}
