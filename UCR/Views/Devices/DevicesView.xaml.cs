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
                DeviceGroupMemberViewModel.RequestRemovalOption = RequestRemovalOptionDialog;
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

        public event EventHandler BackRequested;

        private void Back_OnClick(object sender, System.Windows.RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
