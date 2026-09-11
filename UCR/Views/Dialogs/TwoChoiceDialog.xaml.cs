using System.Windows.Controls;

namespace HidWizards.UCR.Views.Dialogs
{
    public class TwoChoiceDialogViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Choice1 { get; set; }
        public string Choice2 { get; set; }
    }

    public partial class TwoChoiceDialog : UserControl
    {
        public TwoChoiceDialog(string title, string description, string choice1, string choice2)
        {
            DataContext = new TwoChoiceDialogViewModel
            {
                Title = title,
                Description = description,
                Choice1 = choice1,
                Choice2 = choice2
            };
            InitializeComponent();
        }
    }
}
