using System.Windows;
using System.Windows.Controls;
using HidWizards.UCR.ViewModels.Dialogs;

namespace HidWizards.UCR.Views.Dialogs
{
    public partial class ProfileEditDialog : UserControl
    {
        public ProfileEditDialogViewModel ViewModel { get; }

        public ProfileEditDialog(ProfileEditDialogViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }

        // See ProfileEditDialog.xaml's ListBox.ItemContainerStyle comment: this has to be a real
        // one-time assignment (not a Style.Trigger) so the row stays selected once focus moves to the
        // BROWSE/REMOVE buttons, otherwise their commands see SelectedApplicationRule go back to null
        // right as the click is processed.
        private void ApplicationRuleItem_OnGotKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ListBoxItem item) item.IsSelected = true;
        }
    }
}
