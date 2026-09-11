using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;
using HidWizards.UCR.ViewModels.Dialogs;
using HidWizards.UCR.Views.Dialogs;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using HidWizards.UCR.Core.Persistence;

namespace HidWizards.UCR.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;

        public DashboardViewModel Dashboard { get; }

        public ICommand ActivateProfileCommand { get; }
        public ICommand DeactivateProfileCommand { get; }
        public ICommand AddProfileCommand { get; }
        public ICommand GameProfileCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand ImportExportCommand { get; }
        public ICommand SaveCommand { get; }

        public Action<Profile> OpenProfileWindowAction { get; set; }
        
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel(Context context)
        {
            _context = context;
            Dashboard = new DashboardViewModel(context);

            Dashboard.PropertyChanged += Dashboard_PropertyChanged;

            ActivateProfileCommand = new RelayCommand(ActivateProfile, _ => Dashboard.CanActivateProfile);
            DeactivateProfileCommand = new RelayCommand(DeactivateProfile, _ => Dashboard.CanDeactivateProfile);
            AddProfileCommand = new RelayCommand(AddProfile);
            GameProfileCommand = new RelayCommand(GameProfile);
            EditProfileCommand = new RelayCommand(EditProfile, _ => Dashboard.SelectedProfileItem != null);
            ImportExportCommand = new RelayCommand(ImportExport);
            SaveCommand = new RelayCommand(Save, _ => _context.IsNotSaved);

            _context.ActiveProfileChangedEvent += OnActiveProfileChangedEvent;
        }

        private void Dashboard_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Dashboard.SelectedProfileItem))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OnActiveProfileChangedEvent(Profile profile)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void RefreshCommands()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Save(object parameter)
        {
            _context.SaveContext();
        }

        private void ActivateProfile(object parameter)
        {
            if (Dashboard.SelectedProfileItem == null) return;
            if (!_context.SubscriptionsManager.ActivateProfile(Dashboard.SelectedProfileItem.Profile))
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show("The Profile could not be activated, see the log for more details", "Profile failed to activate!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void DeactivateProfile(object parameter)
        {
            if (_context.ActiveProfile == null) return;
            if (!_context.SubscriptionsManager.DeactivateCurrentProfile())
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show("The active Profile could not be deactivated, see the log for more details", "Profile failed to deactivate!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private Profile CreateAndRegisterProfile(string name, Action<Profile> beforeAdd = null)
        {
            var profile = _context.ProfilesManager.CreateProfile(name, new List<DeviceConfiguration>(), new List<DeviceConfiguration>());
            beforeAdd?.Invoke(profile);
            _context.ProfilesManager.AddProfile(profile);
            return profile;
        }

        private void AddProfile(object parameter)
        {
            var profile = CreateAndRegisterProfile("New profile");
            ReloadProfileTree();
            OpenProfileWindowAction?.Invoke(profile);
        }

        private async void GameProfile(object parameter)
        {
            if (Dashboard.SelectedProfileItem != null)
            {
                OpenProfileWindowAction?.Invoke(Dashboard.SelectedProfileItem.Profile);
                return;
            }

            var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select Game Executable" };
            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var stringDialog = new StringDialog("New Game Profile", "Enter profile name", fileName);
                var result = await DialogHost.Show(stringDialog, "RootDialog");
                if (result == null || string.IsNullOrWhiteSpace(result.ToString())) return;

                var profileName = result.ToString();
                var profile = CreateAndRegisterProfile(profileName, p =>
                {
                    p.AutoActivateApplications.Add(new ProfileApplicationRule(filePath));
                });
                ReloadProfileTree();
                
                var newProfileItem = FindProfileItemById(Dashboard.ProfileList, profile.Guid);
                if (newProfileItem != null)
                {
                    Dashboard.SelectedProfileItem = newProfileItem;
                    OpenProfileWindowAction?.Invoke(profile);
                }
            }
        }

        private ProfileItem FindProfileItemById(IEnumerable<ProfileItem> items, Guid id)
        {
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item.Id == id) return item;
                var childMatch = FindProfileItemById(item.Items, id);
                if (childMatch != null) return childMatch;
            }
            return null;
        }



        private async void EditProfile(object parameter)
        {
            if (Dashboard.SelectedProfileItem == null) return;
            var profile = Dashboard.SelectedProfileItem.Profile;
            var vm = new ProfileEditDialogViewModel(profile);
            var dialog = new ProfileEditDialog(vm);

            vm.CloseDialogAction = action => DialogHost.CloseDialogCommand.Execute(action, dialog);

            var result = await DialogHost.Show(dialog, "RootDialog");
            
            if (result is string resultStr)
            {
                if (resultStr != "Save" && resultStr != "Clone" && vm.IsDirty)
                {
                    var discardDialog = new BoolDialog("Unsaved Changes", "There are unsaved changes. Discard them and proceed?");
                    var discardResult = (bool?)await DialogHost.Show(discardDialog, "RootDialog");
                    if (discardResult != true) return;
                }

                if (resultStr == "Clone")
                {
                    var copyDialog = new StringDialog("Copy profile", "Profile name", vm.ProfileName + " Clone");
                    var cloneResult = (bool?)await DialogHost.Show(copyDialog, "RootDialog");
                    if (cloneResult == true && !string.IsNullOrWhiteSpace(copyDialog.Value))
                    {
                        var clonedProfile = _context.ProfilesManager.CopyProfile(profile, copyDialog.Value);
                        if (clonedProfile != null)
                        {
                            clonedProfile.AutoActivateApplications.Clear();
                            foreach (var rule in vm.AutoActivateApplications)
                            {
                                clonedProfile.AutoActivateApplications.Add(rule);
                            }
                        }
                        ReloadProfileTree();
                    }
                }
                else if (resultStr == "Remove")
                {
                    profile.Remove();
                    ReloadProfileTree();
                }
                else if (resultStr == "AddChild")
                {
                    var childProfile = _context.ProfilesManager.CreateProfile("New profile", new List<DeviceConfiguration>(), new List<DeviceConfiguration>());
                    _context.ProfilesManager.AddProfile(childProfile, profile);
                    ReloadProfileTree();
                    OpenProfileWindowAction?.Invoke(childProfile);
                }
                else if (resultStr == "ImportChild")
                {
                    var openDialog = new OpenFileDialog
                    {
                        Title = "Import child profile",
                        Filter = "UCR profile (*.ucrprofile)|*.ucrprofile",
                        CheckFileExists = true
                    };
                    if (openDialog.ShowDialog() == true)
                    {
                        ImportProfilePackageFromPath(openDialog.FileName, profile);
                    }
                }
                else if (resultStr == "Export")
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "Export profile",
                        Filter = "Selected profile (*.ucrprofile)|*.ucrprofile",
                        DefaultExt = ".ucrprofile",
                        FileName = SanitizeFileName(profile.Title) + ".ucrprofile",
                        AddExtension = true
                    };
                    if (saveDialog.ShowDialog() == true)
                    {
                        var exportPath = EnsureExtension(saveDialog.FileName, ".ucrprofile");
                        _context.ProfilesManager.ExportProfile(profile, exportPath);
                        HidWizards.UCR.Utilities.DarkMessageBox.Show("Profile exported successfully.", "Export profile", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else if (resultStr == "Save")
                {
                    if (vm.IsDirty)
                    {
                        var promptDialog = new TwoChoiceDialog("Unsaved Changes", "Do you want to overwrite the existing profile, or clone these changes to a new profile?", "Overwrite", "Clone");
                        var promptResult = await DialogHost.Show(promptDialog, "RootDialog");
                        if (promptResult is bool overwrite)
                        {
                            if (overwrite) // Overwrite
                            {
                                vm.SaveToProfile();
                                ReloadProfileTree();
                            }
                            else // Clone
                            {
                                var newProfileTitle = vm.ProfileName;
                                var clonedProfile = _context.ProfilesManager.CopyProfile(profile, newProfileTitle);
                                if (clonedProfile != null)
                                {
                                    clonedProfile.AutoActivateApplications.Clear();
                                    foreach (var rule in vm.AutoActivateApplications)
                                    {
                                        clonedProfile.AutoActivateApplications.Add(rule);
                                    }
                                }
                                ReloadProfileTree();
                            }
                        }
                    }
                }
            }
        }

        private async void ImportExport(object parameter)
        {
            var result = (string)await DialogHost.Show(new ImportExportDialog(), "RootDialog");
            if (string.Equals(result, "Import", StringComparison.OrdinalIgnoreCase))
            {
                ImportFromCombinedDialog();
            }
            else if (string.Equals(result, "Export", StringComparison.OrdinalIgnoreCase))
            {
                ExportFromCombinedDialog();
            }
        }

        private void ImportFromCombinedDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import UCR profile, profile list, or legacy context",
                Filter = "UCR import files (*.ucrprofile;*.ucrprofiles;context.xml)|*.ucrprofile;*.ucrprofiles;context.xml|UCR profile (*.ucrprofile)|*.ucrprofile|UCR profile list (*.ucrprofiles)|*.ucrprofiles|Legacy UCR context (context.xml)|context.xml",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;

            var extension = Path.GetExtension(dialog.FileName);
            if (string.Equals(extension, ".ucrprofiles", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                ImportProfileListFromPath(dialog.FileName);
            }
            else
            {
                ImportProfilePackageFromPath(dialog.FileName, null);
            }
        }

        private void ImportProfileListFromPath(string fileName)
        {
            var choice = HidWizards.UCR.Utilities.DarkMessageBox.Show(
                "How should the imported profile list be applied?\n\nYes = REPLACE the current profile list with the imported backup.\nNo = MERGE the imported profiles into the current list.\nCancel = Do nothing.",
                "Import profile list", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;

            var mode = choice == MessageBoxResult.Yes ? ProfileListImportMode.Replace : ProfileListImportMode.Merge;
            try
            {
                var importedCount = _context.ProfilesManager.ImportProfileList(fileName, mode);
                if (mode == ProfileListImportMode.Replace)
                {
                    Dashboard.SelectedProfileItem = null;
                }
                ReloadProfileTree();
                HidWizards.UCR.Utilities.DarkMessageBox.Show($"Imported {importedCount} top-level profile{(importedCount == 1 ? string.Empty : "s")} successfully.", "Import profiles", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show(exception.Message, "Profile-list import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportProfilePackageFromPath(string fileName, Profile parentProfile)
        {
            try
            {
                _context.ProfilesManager.ImportProfile(fileName, parentProfile);
                ReloadProfileTree();
                HidWizards.UCR.Utilities.DarkMessageBox.Show(parentProfile == null ? "Profile imported successfully." : "Child profile imported successfully.", "Import profile", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show(exception.Message, "Profile import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportFromCombinedDialog()
        {
            var selectedProfile = Dashboard.SelectedProfileItem?.Profile;
            var dialog = new SaveFileDialog { Title = "Export UCR", AddExtension = true };

            if (selectedProfile != null)
            {
                dialog.Filter = "Selected profile (*.ucrprofile)|*.ucrprofile|All profiles (*.ucrprofiles)|*.ucrprofiles";
                dialog.FilterIndex = 1;
                dialog.DefaultExt = ".ucrprofile";
                dialog.FileName = SanitizeFileName(selectedProfile.Title) + ".ucrprofile";
            }
            else
            {
                dialog.Filter = "All profiles (*.ucrprofiles)|*.ucrprofiles";
                dialog.FilterIndex = 1;
                dialog.DefaultExt = ".ucrprofiles";
                dialog.FileName = "UCR Profiles.ucrprofiles";
            }

            if (dialog.ShowDialog() != true) return;

            try
            {
                var exportAll = selectedProfile == null || dialog.FilterIndex == 2 || string.Equals(Path.GetExtension(dialog.FileName), ".ucrprofiles", StringComparison.OrdinalIgnoreCase);
                if (exportAll)
                {
                    var exportPath = EnsureExtension(dialog.FileName, ".ucrprofiles");
                    _context.ProfilesManager.ExportProfileList(exportPath);
                    HidWizards.UCR.Utilities.DarkMessageBox.Show("All profiles exported successfully.", "Export profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var exportPath = EnsureExtension(dialog.FileName, ".ucrprofile");
                    _context.ProfilesManager.ExportProfile(selectedProfile, exportPath);
                    HidWizards.UCR.Utilities.DarkMessageBox.Show("Profile exported successfully.", "Export profile", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception exception)
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show(exception.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UCR Profile";
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter, '_');
            }
            return value.Trim();
        }

        private static string EnsureExtension(string filePath, string extension)
        {
            if (string.Equals(Path.GetExtension(filePath), extension, StringComparison.OrdinalIgnoreCase)) return filePath;
            return Path.ChangeExtension(filePath, extension.TrimStart('.'));
        }

        private void ReloadProfileTree()
        {
            Dashboard.ReplaceProfileList(ProfileItem.GetProfileTree(_context.Profiles));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
