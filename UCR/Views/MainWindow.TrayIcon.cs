using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HidWizards.UCR.Utilities;
using Forms = System.Windows.Forms;

namespace HidWizards.UCR.Views
{
    public partial class MainWindow
    {
        private void InitializeTrayIcon()
        {
            var contextMenu = new Forms.ContextMenuStrip();
            _stopCurrentProfileMenuItem = new Forms.ToolStripMenuItem("Stop current profile");
            var exitMenuItem = new Forms.ToolStripMenuItem("Exit UCR");

            _stopCurrentProfileMenuItem.Click += (sender, args) => Dispatcher.BeginInvoke((Action)(() => _mainWindowViewModel.DeactivateProfileCommand.Execute(null)));
            exitMenuItem.Click += (sender, args) => Dispatcher.BeginInvoke((Action)ExitFromTray);
            contextMenu.Opening += (sender, args) =>
            {
                _stopCurrentProfileMenuItem.Enabled = Context.ActiveProfile != null;
            };
            contextMenu.Items.Add(_stopCurrentProfileMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Universal Control Remapper",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                ContextMenuStrip = contextMenu,
                Visible = true
            };
            _trayIcon.MouseDoubleClick += (sender, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left) Dispatcher.BeginInvoke((Action)RestoreFromTray);
            };
        }

        private void HideToTray()
        {
            _trayIcon.Visible = true;
            Hide();
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            BringToForeground();
            _trayIcon.Visible = true;
        }

        /// <summary>
        /// Gives UCR normal foreground activation behaviour after startup, tray restoration,
        /// or launching UCR again while an instance is already running. The Topmost pulse is
        /// deliberately temporary; UCR must not remain above other applications afterwards.
        /// </summary>
        public void BringToForeground()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.BringWindowToTop(handle);
                    NativeMethods.SetForegroundWindow(handle);
                }

                Topmost = true;
                try
                {
                    Activate();
                    Focus();
                }
                finally
                {
                    Topmost = false;
                }
            }));
        }

        private void ExitFromTray()
        {
            RestoreFromTray();
            _exitRequested = true;
            WindowCloseState = CloseState.None;
            Close();
        }
    }
}
