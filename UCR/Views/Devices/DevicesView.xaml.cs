using System;
using System.Windows.Controls;
using HidWizards.UCR.ViewModels.Devices;
using HidWizards.UCR.Views.Dialogs;
using MaterialDesignThemes.Wpf;

namespace HidWizards.UCR.Views.Devices
{
    public partial class DevicesView : UserControl
    {
        public DevicesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is DevicesViewModel vm)
            {
                vm.RequestGroupName = RequestGroupNameDialog;
                vm.RequestRemovalOption = RequestRemovalOptionDialog;
            }
        }

        private async void RequestGroupNameDialog(Action<string> callback)
        {
            var dialog = new StringDialog("New Device Group", "Enter group name", "New Group");
            var result = await DialogHost.Show(dialog, "RootDialog");
            if (result is bool success && success)
            {
                callback(dialog.Value);
            }
        }

        private async void RequestRemovalOptionDialog(Action<bool> callback)
        {
            var dialog = new DecisionDialog("Remove Device from Group", "Removing a device will affect existing mappings.\nDo you want to permanently DELETE the mappings, or RETAIN them in the profile?\n\n(Yes = Retain, No = Delete)");
            var result = await DialogHost.Show(dialog, "RootDialog");
            
            if (result is System.Windows.MessageBoxResult mbr)
            {
                if (mbr == System.Windows.MessageBoxResult.Yes)
                {
                    callback(false); // Retain -> DeleteMappings = false
                }
                else if (mbr == System.Windows.MessageBoxResult.No)
                {
                    callback(true); // Delete -> DeleteMappings = true
                }
            }
        }

        private void AddToGroupButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu == null) return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        // DeviceManagerPage (rename via alias, hide, remove from UCR/Windows, reorder) was fully built
        // and correctly wired internally, but had no reachable entry point anywhere in the app -- see
        // vault/ui-wiring-audit-2026-09-22.md. It's self-contained (only needs a DevicesManager, no
        // dependency on the removed legacy ProfileWindow/ProfilePage), so it hosts cleanly in the same
        // DialogHost.Show pattern every other dialog in this app already uses.
        private async void ManageDevices_OnClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!(DataContext is DevicesViewModel vm)) return;

            var page = new DeviceManagerPage(vm.DevicesManager);
            page.BackRequested += (s, args) => DialogHost.CloseDialogCommand.Execute(null, page);
            try
            {
                await DialogHost.Show(page, "RootDialog");
            }
            finally
            {
                page.Dispose();
            }
        }
    }
}
