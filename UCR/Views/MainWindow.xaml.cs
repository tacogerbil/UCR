using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Persistence;
using HidWizards.UCR.Core.Utilities;
using HidWizards.UCR.Utilities;
using HidWizards.UCR.ViewModels.Dashboard;
using HidWizards.UCR.Views.Dialogs;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace HidWizards.UCR.Views
{
    /// <summary>
    /// MainWindow's code-behind is split across partial-class files by responsibility:
    ///   - MainWindow.xaml.cs (this file): window lifecycle, message pump, appearance/menu chrome.
    ///   - MainWindow.TrayIcon.cs: system tray icon lifecycle.
    ///
    /// The old ProfileTree TreeView (and its drag-and-drop partial-class file) was removed when the
    /// Simplified UI's Game Profile toolbar chip replaced it; see vault/passdown.md, Phase 2.
    ///
    /// Note: All profile CRUD (add/rename/copy/remove) and Import/Export functionality
    /// has been modularized into MainWindowViewModel to adhere to MVVM and MCCC standards.
    /// </summary>
    public partial class MainWindow : Window
    {
        private Context Context { get; set; }
        private readonly HidWizards.UCR.ViewModels.MainWindowViewModel _mainWindowViewModel;
        private CloseState WindowCloseState { get; set; }
        private Forms.NotifyIcon _trayIcon;
        private Forms.ToolStripMenuItem _stopCurrentProfileMenuItem;
        private readonly AutoProfileMonitor _autoProfileMonitor;
        private bool _exitRequested;
        enum CloseState
        {
            None,
            Closing,
            ForceClose
        }

        public MainWindow(Context context)
        {
            _mainWindowViewModel = new HidWizards.UCR.ViewModels.MainWindowViewModel(context);
            DataContext = _mainWindowViewModel;
            Context = context;
            InitializeComponent();
            
            var devicesViewModel = new HidWizards.UCR.ViewModels.Devices.DevicesViewModel(context);
            DevicesViewElement.DataContext = devicesViewModel;

            // Selecting a device/group on the Devices tab is now what drives the Mapping scope
            // (replaces the old toolbar Input Scope dropdown).
            devicesViewModel.ScopeSelectionChanged = scope => _mainWindowViewModel.Dashboard.SelectedInputScope = scope;
            devicesViewModel.RequestChooseProfile = ShowSelectProfileDialog;
            devicesViewModel.RequestEditProfile = profile => _mainWindowViewModel.EditProfileByGuid(profile.Guid);

            var patchBayViewModel = new HidWizards.UCR.ViewModels.Mapping.PatchBayViewModel(context);
            MappingViewElement.DataContext = patchBayViewModel;

            // Sync Scope and Catalog to the Patch Bay
            patchBayViewModel.FullCatalog = _mainWindowViewModel.Dashboard.InputSources;
            patchBayViewModel.CurrentScope = _mainWindowViewModel.Dashboard.SelectedInputScope;

            // Patch Bay rows are generated from whichever Output Device the toolbar has selected;
            // this keeps them in sync for the lifetime of the window (see PatchBayViewModel.AttachToDashboard).
            patchBayViewModel.AttachToDashboard(_mainWindowViewModel.Dashboard);
            _mainWindowViewModel.Dashboard.RequestShowMapping = () => MappingViewRadio.IsChecked = true;

            _mainWindowViewModel.Dashboard.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DashboardViewModel.SelectedInputScope))
                {
                    patchBayViewModel.CurrentScope = _mainWindowViewModel.Dashboard.SelectedInputScope;
                }
                else if (args.PropertyName == nameof(DashboardViewModel.SelectedProfileItem))
                {
                    patchBayViewModel.SetProfile(_mainWindowViewModel.Dashboard.SelectedProfileItem?.Profile);
                }
            };
            
            InitializeTrayIcon();
            _autoProfileMonitor = new AutoProfileMonitor(context);
        }

        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!AppearancePopup.IsOpen || e.Key != Key.Escape) return;
            AppearancePopup.IsOpen = false;
            e.Handled = true;
        }

        private void MainWindow_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            var scale = AppearanceManager.AdjustUiScale(e.Delta);
            Logger.Info("UI scale changed to " + Math.Round(scale * 100) + "%");
            e.Handled = true;
        }

        /// <summary>
        /// AddHook Handle WndProc messages in WPF
        /// This cannot be done in a Window's constructor as a handle window handle won't at that point, so there won't be a HwndSource.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            EnableMessageHandling();
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        private void ContextMenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu == null) return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OpenLogs_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Logger.GetLogDirectory();
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
                Logger.Info("Opened diagnostic logs folder: " + path);
            }
            catch (Exception exception)
            {
                Logger.Error("Unable to open the diagnostic logs folder", exception);
                HidWizards.UCR.Utilities.DarkMessageBox.Show(
                    "UCR could not open the logs folder. The logs remain under the UCR 'logs' directory.",
                    "Unable to open logs", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ShowSelectProfileDialog(Action<Guid> callback)
        {
            var dialog = new HidWizards.UCR.Views.Dialogs.SelectProfileDialog(Context);
            dialog.ViewModel.CloseDialogAction = result => DialogHost.CloseDialogCommand.Execute(result, dialog);
            var result = await DialogHost.Show(dialog, "RootDialog");
            if (result is Guid guid)
            {
                // Covers both "selected an existing profile" (no-op refresh) and "created a new one"
                // (Dashboard.ProfileList would otherwise never learn about it — see ReloadProfileTree).
                _mainWindowViewModel.ReloadProfileTree();
                callback(guid);
            }
        }

        private void Appearance_OnClick(object sender, RoutedEventArgs e)
        {
            AppearancePopup.IsOpen = true;
        }

        private void AppearancePopup_OnOpened(object sender, EventArgs e)
        {
            AppearancePicker.Refresh();
            AppearancePicker.Focus();
            Keyboard.Focus(AppearancePicker);
        }

        private void AppearancePopup_OnClosed(object sender, EventArgs e)
        {
            // Closing by Escape or click-away is a pure cancel: no colour is applied unless a swatch
            // was explicitly clicked. Return keyboard focus to the control that opened the picker.
            AppearanceButton.Focus();
        }

        private void AppearancePicker_OnAccentSelected(object sender, EventArgs e)
        {
            AppearancePopup.IsOpen = false;
        }

        private void AppearancePicker_OnCancelRequested(object sender, EventArgs e)
        {
            AppearancePopup.IsOpen = false;
        }

        internal void PrepareForShutdown()
        {
            _autoProfileMonitor?.Dispose();
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoProfileMonitor?.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            base.OnClosed(e);
        }

        private async void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (!_exitRequested)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            if (CloseState.ForceClose.Equals(WindowCloseState)) return;
            if (CloseState.Closing.Equals(WindowCloseState))
            {
                if (WindowState.Equals(WindowState.Minimized)) WindowState = WindowState.Normal;

                e.Cancel = true;
                SystemSounds.Exclamation.Play();
                return;
            }

            e.Cancel = true;
            WindowCloseState = CloseState.Closing;
            var saveBeforeShutdown = false;

            if (Context.IsNotSaved)
            {
                if (WindowState.Equals(WindowState.Minimized))
                {
                    WindowState = WindowState.Normal;
                    SystemSounds.Exclamation.Play();
                    RootDialog.Focus();
                }

                if (RootDialog.IsOpen)
                {
                    DialogHost.CloseDialogCommand.Execute(null, RootDialog);
                }

                var dialog = new DecisionDialog("Configuration has changed", "Do you want to save before closing?");
                var result = (MessageBoxResult?)await DialogHost.Show(dialog, "RootDialog");
                if (result == null)
                {
                    WindowCloseState = CloseState.None;
                    _exitRequested = false;
                    return;
                }

                switch (result)
                {
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                        WindowCloseState = CloseState.None;
                        _exitRequested = false;
                        return;
                    case MessageBoxResult.OK:
                    case MessageBoxResult.Yes:
                        saveBeforeShutdown = true;
                        break;
                    case MessageBoxResult.No:
                        break;
                }
            }

            BeginFinalShutdown(saveBeforeShutdown);
        }

        private void BeginFinalShutdown(bool saveContext)
        {
            WindowCloseState = CloseState.ForceClose;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var app = Application.Current as App;
                if (app != null)
                {
                    app.ShutdownWithProgress(this, saveContext);
                    return;
                }

                Close();
            }));
        }

        private void Save_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            _mainWindowViewModel.SaveCommand.Execute(null);
        }

        private void Save_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _mainWindowViewModel.SaveCommand.CanExecute(null);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_DEVICECHANGE)
            {
                Context?.InvokeDeviceListChanged();
            }

            if (msg != NativeMethods.WM_COPYDATA) return IntPtr.Zero;
            
            var data = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.COPYDATASTRUCT));
            var argsString = Marshal.PtrToStringAnsi(data.lpData);
            if (!string.IsNullOrEmpty(argsString)) Context.ParseCommandLineArguments(argsString.Split(';'));
            RestoreFromTray();
            return IntPtr.Zero;
        }

        private void EnableMessageHandling()
        {
            var changeFilter = new NativeMethods.CHANGEFILTERSTRUCT();
            changeFilter.size = (uint)Marshal.SizeOf(changeFilter);
            changeFilter.info = 0;
            if
            (
                NativeMethods.ChangeWindowMessageFilterEx(
                    new WindowInteropHelper(this).EnsureHandle(),
                    NativeMethods.WM_COPYDATA,
                    NativeMethods.ChangeWindowMessageFilterExAction.Allow,
                    ref changeFilter)
            ) return;

            var error = Marshal.GetLastWin32Error();
            HidWizards.UCR.Utilities.DarkMessageBox.Show($"Enabling message handling failed with the error: {error}");
        }

        private async void About_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            await DialogHost.Show(dialog, "RootDialog");
        }

        private async void Help_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new HelpDialog();
            await DialogHost.Show(dialog, "RootDialog");
        }
    }
}
