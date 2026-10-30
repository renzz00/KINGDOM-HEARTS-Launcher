using System;
using System.Windows;

namespace Kingdom_Hearts_Launcher
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            App app = new App();
            app.InitializeComponent();
            app.DispatcherUnhandledException += (s, e) =>
            {
                e.Handled = true;
                ShowFatal(e.Exception);
                app.Shutdown(1);
            };

            MainWindow mainWindow;
            try
            {
                mainWindow = new MainWindow(args);
            }
            catch (Exception ex)
            {
                // Last resort at the top level: a plain message beats the raw .NET crash dialog.
                ShowFatal(ex);
                return;
            }

            if (mainWindow.LaunchedFromArgs)
            {
                return;
            }
            app.Run(mainWindow);
        }

        private static void ShowFatal(Exception ex)
        {
            MessageBox.Show(
                "KINGDOM HEARTS Launcher ran into a problem and has to close.\n\n" + ex.Message
                + "\n\nIf this keeps happening, delete launcher_config.json next to the launcher and start it again.",
                "KINGDOM HEARTS Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
