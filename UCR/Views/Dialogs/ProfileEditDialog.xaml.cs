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
    }
}
