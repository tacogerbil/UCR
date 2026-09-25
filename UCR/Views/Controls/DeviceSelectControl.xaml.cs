using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HidWizards.UCR.ViewModels.Controls;
using HidWizards.UCR.ViewModels.DeviceViewModels;

namespace HidWizards.UCR.Views.Controls
{
    public partial class DeviceSelectControl : UserControl
    {
        
        public DeviceSelectControl()
        {
            InitializeComponent();
        }

        private void Device_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // The row's CheckBox already toggles itself natively. Without this guard, clicking the
            // CheckBox also fires this row-wide "click anywhere to toggle" convenience handler,
            // toggling Checked a second time — net effect: checking a device never actually stuck,
            // which is why AddDevicesDialog's ADD button never added anything.
            if (e.OriginalSource is DependencyObject source && FindAncestorOrSelf<CheckBox>(source) != null) return;

            var device = (sender as Grid)?.DataContext as DeviceViewModel;
            device?.ToggleSelection();
        }

        private static T FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T match) return match;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        public void BringDeviceIntoView(DeviceViewModel device)
        {
            if (device == null) return;
            DeviceItems.UpdateLayout();
            var container = DeviceItems.ItemContainerGenerator.ContainerFromItem(device) as FrameworkElement;
            container?.BringIntoView();
        }
    }
}
