using System.Windows.Controls;
using HidWizards.UCR.Core;
using HidWizards.UCR.ViewModels.Dialogs;

namespace HidWizards.UCR.Views.Dialogs
{
    public partial class SelectProfileDialog : UserControl
    {
        public SelectProfileDialogViewModel ViewModel { get; }

        public SelectProfileDialog(Context context)
        {
            ViewModel = new SelectProfileDialogViewModel(context);
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
