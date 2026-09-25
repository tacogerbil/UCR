using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Annotations;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;
using Microsoft.Win32;

namespace HidWizards.UCR.ViewModels.Dialogs
{
    public class SelectableProfileItem
    {
        public Guid Guid { get; }
        public string Title { get; }

        public SelectableProfileItem(Profile profile)
        {
            Guid = profile.Guid;
            Title = profile.Title;
        }
    }

    /// <summary>
    /// Lets the user pick an existing Game Profile or create a new one. Closes via CloseDialogAction
    /// with the resulting Profile's Guid (or null on cancel) — same callback pattern as
    /// ProfileEditDialogViewModel.CloseDialogAction, wired by the caller after construction.
    /// </summary>
    public class SelectProfileDialogViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;

        public ObservableCollection<SelectableProfileItem> Profiles { get; } = new ObservableCollection<SelectableProfileItem>();

        private SelectableProfileItem _selectedProfile;
        public SelectableProfileItem SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (_selectedProfile == value) return;
                _selectedProfile = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _newProfileName = "";
        public string NewProfileName
        {
            get => _newProfileName;
            set
            {
                if (_newProfileName == value) return;
                _newProfileName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Optional: matches the app's other profile-creation flow (auto-activate on launching a
        // game executable). Left blank, the created profile just has no auto-activate rule yet —
        // one can still be added later via Edit Profile's Auto-Activate Applications section.
        private string _gameExecutablePath = "";
        public string GameExecutablePath
        {
            get => _gameExecutablePath;
            private set
            {
                if (_gameExecutablePath == value) return;
                _gameExecutablePath = value;
                OnPropertyChanged();
            }
        }

        public ICommand SelectCommand { get; }
        public ICommand BrowseExecutableCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<object> CloseDialogAction { get; set; }

        public SelectProfileDialogViewModel(Context context)
        {
            _context = context;
            foreach (var profile in Flatten(context.Profiles))
            {
                Profiles.Add(new SelectableProfileItem(profile));
            }

            SelectCommand = new RelayCommand(_ => CloseDialogAction?.Invoke(SelectedProfile.Guid), _ => SelectedProfile != null);
            BrowseExecutableCommand = new RelayCommand(_ =>
            {
                var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", Title = "Select Game Executable" };
                if (dialog.ShowDialog() != true) return;

                GameExecutablePath = dialog.FileName;
                if (string.IsNullOrWhiteSpace(NewProfileName))
                {
                    NewProfileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            });
            CreateCommand = new RelayCommand(async _ =>
            {
                var profile = _context.ProfilesManager.CreateProfile(NewProfileName, new List<DeviceConfiguration>(), new List<DeviceConfiguration>());
                if (!string.IsNullOrWhiteSpace(GameExecutablePath))
                {
                    profile.AutoActivateApplications.Add(new ProfileApplicationRule(GameExecutablePath));
                }
                _context.ProfilesManager.AddProfile(profile);

                // The whole point of a profile is binding physical input to a virtual output device —
                // ask for it immediately as part of creation rather than leaving it as a separate,
                // easy-to-miss step buried in Edit Profile. Not blocking: cancelling this picker still
                // leaves the profile created, just without an output device configured yet (can still
                // be added later via Edit Profile, same as before this existed). Logged/guarded rather
                // than left to fail silently — if the nested DialogHost lookup ever breaks, profile
                // creation must still complete instead of the user losing the profile they just named.
                try
                {
                    HidWizards.UCR.Core.Utilities.Logger.Info("SelectProfileDialog: opening output device picker for new profile " + profile.Guid);
                    var outputDeviceControlViewModel = new ProfileDeviceListControlViewModel(
                        profile, profile.OutputDeviceConfigurations, DeviceIoType.Output, null,
                        "SelectProfileNestedDialogHost");
                    await outputDeviceControlViewModel.AddDevices();
                    outputDeviceControlViewModel.Dispose();
                    HidWizards.UCR.Core.Utilities.Logger.Info("SelectProfileDialog: output device picker closed; profile now has " + profile.OutputDeviceConfigurations.Count + " output device(s)");
                }
                catch (Exception exception)
                {
                    HidWizards.UCR.Core.Utilities.Logger.Error("SelectProfileDialog: output device picker failed", exception);
                }

                CloseDialogAction?.Invoke(profile.Guid);
            }, _ => !string.IsNullOrWhiteSpace(NewProfileName));
            CancelCommand = new RelayCommand(_ => CloseDialogAction?.Invoke(null));
        }

        private static IEnumerable<Profile> Flatten(IEnumerable<Profile> profiles)
        {
            foreach (var profile in profiles)
            {
                yield return profile;
                foreach (var child in Flatten(profile.ChildProfiles)) yield return child;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
