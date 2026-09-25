using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;
using Microsoft.Win32;

namespace HidWizards.UCR.ViewModels.Dialogs
{
    public enum ProfileEditResult
    {
        Clone,
        Remove,
        AddChild,
        ImportChild,
        Export,
        Save
    }

    public class ProfileEditDialogViewModel : INotifyPropertyChanged, IDisposable
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

        private ProfileApplicationRule _selectedApplicationRule;
        public ProfileApplicationRule SelectedApplicationRule
        {
            get => _selectedApplicationRule;
            set
            {
                if (_selectedApplicationRule == value) return;
                _selectedApplicationRule = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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

        // Only reachable "add an Output Device to a profile" path in the app right now — the old
        // full ProfilePage/ProfileWindow editor that used to do this relies on a navigation-hosting
        // mechanism (MainWindow.xaml.cs's ShowNavigationPage) that turned out to be dead stub code
        // left over from an earlier refactor, and the toolbar's old Output Device dropdown only ever
        // let you pick among already-added devices, never add one. Reuses the same
        // ProfileDeviceListControlViewModel the (unreachable) old editor used, and the same
        // AddDevicesDialog/ManageDeviceConfigurationDialog it already opens via DialogHost.Show —
        // no new device-management logic, just a working place to reach it from.
        public ProfileDeviceListControlViewModel OutputDeviceControlViewModel { get; }
        public ICommand AddOutputDeviceCommand { get; }
        public ICommand RemoveOutputDeviceCommand { get; }
        public ICommand ConfigureOutputDeviceCommand { get; }

        public Action<ProfileEditResult> CloseDialogAction { get; set; }

        public ProfileEditDialogViewModel(Profile profile)
        {
            _profile = profile;
            _profileName = profile.Title;
            AutoActivateApplications = new ObservableCollection<ProfileApplicationRule>(profile.AutoActivateApplications);
            // "ProfileEditNestedDialogHost" (not the default "RootDialog"): this whole dialog is
            // itself shown on "RootDialog", which can only have one dialog open at a time — see
            // ProfileEditDialog.xaml's nested DialogHost for why it has to be a sibling, not this
            // control's ancestor.
            OutputDeviceControlViewModel = new ProfileDeviceListControlViewModel(
                profile, profile.OutputDeviceConfigurations, DeviceIoType.Output, () => IsDirty = true,
                "ProfileEditNestedDialogHost");

            CloneCommand = new RelayCommand(ExecuteClone);
            RemoveCommand = new RelayCommand(ExecuteRemove);
            AddChildCommand = new RelayCommand(ExecuteAddChild);
            ImportChildCommand = new RelayCommand(ExecuteImportChild);
            ExportCommand = new RelayCommand(ExecuteExport);

            AddApplicationRuleCommand = new RelayCommand(ExecuteAddApplicationRule);
            RemoveApplicationRuleCommand = new RelayCommand(ExecuteRemoveApplicationRule, _ => SelectedApplicationRule != null);
            BrowseApplicationRuleCommand = new RelayCommand(ExecuteBrowseApplicationRule, _ => SelectedApplicationRule != null);

            AddOutputDeviceCommand = new RelayCommand(_ => OutputDeviceControlViewModel.AddDevices());
            RemoveOutputDeviceCommand = new RelayCommand(
                _ => OutputDeviceControlViewModel.RemoveDevice(OutputDeviceControlViewModel.SelectedDeviceConfiguration),
                _ => OutputDeviceControlViewModel.IsRemoveEnabled);
            ConfigureOutputDeviceCommand = new RelayCommand(
                _ => OutputDeviceControlViewModel.ManageDeviceConfiguration(),
                _ => OutputDeviceControlViewModel.IsConfigurationEnabled);
        }

        public void Dispose()
        {
            OutputDeviceControlViewModel.Dispose();
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
            CloseDialogAction?.Invoke(ProfileEditResult.Clone);
        }

        private void ExecuteRemove(object parameter)
        {
            CloseDialogAction?.Invoke(ProfileEditResult.Remove);
        }

        private void ExecuteAddChild(object parameter)
        {
            CloseDialogAction?.Invoke(ProfileEditResult.AddChild);
        }

        private void ExecuteImportChild(object parameter)
        {
            CloseDialogAction?.Invoke(ProfileEditResult.ImportChild);
        }

        private void ExecuteExport(object parameter)
        {
            CloseDialogAction?.Invoke(ProfileEditResult.Export);
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
