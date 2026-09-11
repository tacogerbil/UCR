using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;
using Microsoft.Win32;

namespace HidWizards.UCR.ViewModels.Dialogs
{
    public class ProfileEditDialogViewModel : INotifyPropertyChanged
    {
        private readonly Profile _profile;
        private string _profileName;

        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (_profileName != value)
                {
                    _profileName = value;
                    IsDirty = true;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ProfileApplicationRule> AutoActivateApplications { get; }
        public ProfileApplicationRule SelectedApplicationRule { get; set; }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CloneCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand AddChildCommand { get; }
        public ICommand ImportChildCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand AddApplicationRuleCommand { get; }
        public ICommand RemoveApplicationRuleCommand { get; }
        public ICommand BrowseApplicationRuleCommand { get; }

        public Action<string> CloseDialogAction { get; set; }

        public ProfileEditDialogViewModel(Profile profile)
        {
            _profile = profile;
            _profileName = profile.Title;
            AutoActivateApplications = new ObservableCollection<ProfileApplicationRule>(profile.AutoActivateApplications);

            CloneCommand = new RelayCommand(ExecuteClone);
            RemoveCommand = new RelayCommand(ExecuteRemove);
            AddChildCommand = new RelayCommand(ExecuteAddChild);
            ImportChildCommand = new RelayCommand(ExecuteImportChild);
            ExportCommand = new RelayCommand(ExecuteExport);
            
            AddApplicationRuleCommand = new RelayCommand(ExecuteAddApplicationRule);
            RemoveApplicationRuleCommand = new RelayCommand(ExecuteRemoveApplicationRule, _ => SelectedApplicationRule != null);
            BrowseApplicationRuleCommand = new RelayCommand(ExecuteBrowseApplicationRule, _ => SelectedApplicationRule != null);
        }

        public void SaveToProfile()
        {
            if (_profileName != _profile.Title)
            {
                _profile.Rename(_profileName);
            }
            
            // Sync auto-activate apps
            _profile.AutoActivateApplications.Clear();
            foreach (var rule in AutoActivateApplications)
            {
                _profile.AutoActivateApplications.Add(rule);
            }
        }

        private void ExecuteClone(object parameter)
        {
            CloseDialogAction?.Invoke("Clone");
        }

        private void ExecuteRemove(object parameter)
        {
            CloseDialogAction?.Invoke("Remove");
        }

        private void ExecuteAddChild(object parameter)
        {
            CloseDialogAction?.Invoke("AddChild");
        }

        private void ExecuteImportChild(object parameter)
        {
            CloseDialogAction?.Invoke("ImportChild");
        }

        private void ExecuteExport(object parameter)
        {
            CloseDialogAction?.Invoke("Export");
        }

        private void ExecuteAddApplicationRule(object parameter)
        {
            AutoActivateApplications.Add(new ProfileApplicationRule(""));
            IsDirty = true;
        }

        private void ExecuteRemoveApplicationRule(object parameter)
        {
            if (SelectedApplicationRule != null)
            {
                AutoActivateApplications.Remove(SelectedApplicationRule);
                IsDirty = true;
            }
        }

        private void ExecuteBrowseApplicationRule(object parameter)
        {
            if (SelectedApplicationRule != null)
            {
                var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select Game Executable" };
                if (dialog.ShowDialog() == true)
                {
                    SelectedApplicationRule.Executable = dialog.FileName;
                    IsDirty = true;
                    // Trigger property change for UI update
                    var index = AutoActivateApplications.IndexOf(SelectedApplicationRule);
                    if (index >= 0)
                    {
                        AutoActivateApplications[index] = new ProfileApplicationRule(dialog.FileName);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
