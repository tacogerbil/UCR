using System.Windows.Controls;
using HidWizards.UCR.ViewModels.Dialogs;

namespace HidWizards.UCR.Views.Dialogs
{
    public partial class ProfileManagerDialog : UserControl
    {
        public ProfileManagerDialogViewModel ViewModel { get; }

        public ProfileManagerDialog(ProfileManagerDialogViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
