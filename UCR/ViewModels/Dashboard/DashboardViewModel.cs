using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Annotations;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Dashboard
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Title => "Universal Control Remapper";
        public Visibility ProfileDetailsActive => SelectedProfileItem != null ? Visibility.Visible : Visibility.Hidden;
        public bool CanActivateProfile => SelectedProfileItem != null;
        public bool CanDeactivateProfile => Context?.ActiveProfile != null;
        public ProfileDeviceListControlViewModel InputDeviceControlViewModel { get; set; }
        public ProfileDeviceListControlViewModel OutputDeviceControlViewModel { get; set; }

        private ProfileItem _selectedProfileItem;
        public ProfileItem SelectedProfileItem
        {
            get => _selectedProfileItem;
            set
            {
                _selectedProfileItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfileDetailsActive));
                OnPropertyChanged(nameof(CanActivateProfile));
                if (_selectedProfileItem == null)
                {
                    DisposeDeviceLists();
                    OnPropertyChanged(nameof(InputDeviceControlViewModel));
                    OnPropertyChanged(nameof(OutputDeviceControlViewModel));
                }
            }
        }

        public ObservableCollection<ProfileItem> ProfileList { get; private set; }
        public ICollectionView ProfileListView { get; private set; }

        public ObservableCollection<InputScopeItem> InputSources { get; } = new ObservableCollection<InputScopeItem>();
        
        private InputScopeItem _selectedInputScope;
        public InputScopeItem SelectedInputScope
        {
            get => _selectedInputScope;
            set
            {
                if (_selectedInputScope == value) return;
                _selectedInputScope = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();

                // Once Mapping is already unlocked, a scope change (e.g. from the selector on the
                // Mapping tab itself, not just the Devices tab) should immediately re-point at that
                // scope's own profile — mirrors ExecuteBeginMapping's own resolution, see below.
                if (IsMappingUnlocked) ApplyAssociatedProfileForCurrentScope();
            }
        }

        // The Mapping tab stays hidden until the user has picked an Input Scope (a single device or
        // a group) and explicitly asked to begin mapping. Once unlocked it stays unlocked for the
        // rest of the session; the Devices tab remains reachable to go back and adjust things.
        private bool _isMappingUnlocked;
        public bool IsMappingUnlocked
        {
            get => _isMappingUnlocked;
            private set
            {
                if (_isMappingUnlocked == value) return;
                _isMappingUnlocked = value;
                OnPropertyChanged();
            }
        }

        public ICommand BeginMappingCommand { get; }

        private bool CanBeginMapping(object parameter)
        {
            return SelectedInputScope != null
                && Context.ScopeProfileAssociationService.GetAssociatedProfile(SelectedInputScope.Id) != null;
        }

        // Fired every time Begin Mapping is pressed, not just on the first IsMappingUnlocked
        // transition -- IsMappingUnlocked's setter no-ops once already true, so relying on its
        // PropertyChanged to drive tab navigation left the button permanently unable to bring the
        // user back to the Mapping tab after they'd navigated away from it once.
        public Action RequestShowMapping { get; set; }

        private void ExecuteBeginMapping(object parameter)
        {
            ApplyAssociatedProfileForCurrentScope();
            IsMappingUnlocked = true;
            RequestShowMapping?.Invoke();
        }

        // The Devices tab (or the scope selector on the Mapping tab itself) is where a scope gets its
        // Game Profile association — by the time this is called from ExecuteBeginMapping,
        // CanBeginMapping has already confirmed one exists; called from the scope selector it's a
        // best-effort re-point that's a no-op if the newly selected scope has no association yet.
        // Whether the resolved profile itself has an Output Device configured is the Patch Bay's own
        // concern (see MappingView's empty state), not a precondition for getting there.
        private void ApplyAssociatedProfileForCurrentScope()
        {
            if (SelectedInputScope == null) return;

            var profileGuid = Context.ScopeProfileAssociationService.GetAssociatedProfile(SelectedInputScope.Id);
            if (profileGuid == null) return;

            var profileItem = FindProfileItem(ProfileList, profileGuid.Value);
            if (profileItem == null)
            {
                // ProfileList is only a display-friendly wrapper around Context.Profiles; anything
                // that creates a Profile without going through this ViewModel (e.g. the Devices tab's
                // SelectProfileDialog) can leave it stale. Self-heal here rather than depend on every
                // future caller remembering to refresh it.
                ReplaceProfileList(ProfileItem.GetProfileTree(Context.Profiles));
                profileItem = FindProfileItem(ProfileList, profileGuid.Value);
            }
            if (profileItem == null) return;

            SelectedProfileItem = profileItem;

            // Selecting a device/group as the Mapping scope never actually added it to the profile's
            // own InputDeviceConfigurations -- and that list is what BindingManager.BeginBindMode
            // (Listen mode) and MappingRowViewModel.BuildManualPickerMenu (the manual picker) both
            // iterate to decide which physical devices to listen to at all. Without this, Listen mode
            // arms with zero devices subscribed and just waits forever no matter what gets pressed,
            // and the manual picker renders empty, regardless of whether the profile itself is active.
            EnsureScopeDevicesAreInProfileInputs(profileItem.Profile, SelectedInputScope);

            // Without this, nothing about "Begin Mapping" actually starts routing real hardware
            // input: Listen mode's EnterBindMode(), the Patch Bay's live value/pressed indicators,
            // and the manual picker's device list (MappingRowViewModel.BuildManualPickerMenu) all
            // depend on Context.ActiveProfile being set, which previously only happened via a
            // separate, easy-to-miss toolbar "Activate profile" button nothing in this flow ever
            // pointed the user at. Idempotent for an already-active profile (SubscriptionsManager.
            // ActivateProfile early-returns true), so safe to call on every scope/profile change.
            if (!Context.SubscriptionsManager.ActivateProfile(profileItem.Profile))
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show(
                    "The Profile could not be activated, see the log for more details",
                    "Profile failed to activate!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        // Adds a DeviceConfiguration for each of the scope's member devices that isn't already in the
        // profile's InputDeviceConfigurations. Deliberately additive/idempotent, never removes -- a
        // device dropped from a group later is a separate concern, not something to silently unwire
        // here. A member device that isn't currently connected/detected is skipped rather than failing
        // the whole operation; it'll be picked up the next time this runs after it reconnects.
        private void EnsureScopeDevicesAreInProfileInputs(Profile profile, InputScopeItem scope)
        {
            if (profile == null || scope == null || scope.MemberDeviceIds == null) return;

            var existingKeys = new HashSet<string>(profile.InputDeviceConfigurations
                .Select(configuration => DeviceIdentity.BuildLogicalKey(configuration.Device)));

            var availableDevices = Context.DevicesManager.GetAvailableDeviceList(DeviceIoType.Input, false);
            var toAdd = new List<DeviceConfiguration>();

            foreach (var memberDeviceId in scope.MemberDeviceIds)
            {
                if (!existingKeys.Add(memberDeviceId)) continue;

                var device = availableDevices.FirstOrDefault(d => DeviceIdentity.BuildLogicalKey(d) == memberDeviceId);
                if (device == null) continue;

                toAdd.Add(new DeviceConfiguration(device));
            }

            if (toAdd.Count > 0)
            {
                profile.AddDeviceConfigurations(toAdd, DeviceIoType.Input);
            }
        }

        private string _profileGroupingMode = "Tree";
        public string ProfileGroupingMode
        {
            get => _profileGroupingMode;
            set
            {
                if (string.Equals(_profileGroupingMode, value, StringComparison.Ordinal)) return;
                _profileGroupingMode = value ?? "Tree";
                RebuildProfileView();
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupProfilesByInput));
            }
        }

        public bool GroupProfilesByInput
        {
            get => string.Equals(ProfileGroupingMode, "Input", StringComparison.Ordinal);
            set => ProfileGroupingMode = value ? "Input" : "Tree";
        }

        public string ActiveProfileBreadCrumbs => Context?.ActiveProfile != null ? Context.ActiveProfile.ProfileBreadCrumbs() : "None";

        private Context Context { get; set; }

        public DashboardViewModel(Context context)
        {
            Context = context;
            BeginMappingCommand = new RelayCommand(ExecuteBeginMapping, CanBeginMapping);
            ProfileList = ProfileItem.GetProfileTree(context.Profiles);
            RebuildProfileView();
            PropertyChanged += OnPropertyChanged;
            context.ActiveProfileChangedEvent += OnActiveProfileChangedEvent;
            context.DeviceAliasesChangedEvent += OnDeviceAliasesChangedEvent;
            
            context.DevicesManager.DeviceListChanged += OnDeviceListChanged;
            context.DeviceGroupService.DeviceGroupsChanged += OnDeviceGroupsChanged;
            PopulateInputSources();
        }

        private void OnDeviceListChanged()
        {
            Application.Current.Dispatcher.Invoke(PopulateInputSources);
        }

        private void OnDeviceGroupsChanged()
        {
            Application.Current.Dispatcher.Invoke(PopulateInputSources);
        }

        private void PopulateInputSources()
        {
            var oldSelectedId = SelectedInputScope?.Id;
            InputSources.Clear();

            // 1. Add Ungrouped Devices
            var inputDevices = Context.DevicesManager.GetAvailableDeviceList(DeviceIoType.Input, false);
            foreach (var device in inputDevices)
            {
                var logicalKey = DeviceIdentity.BuildLogicalKey(device);
                // Check if this device is part of any group
                bool isGrouped = false;
                foreach (var group in Context.DeviceGroups)
                {
                    if (group.MemberDeviceIdentities.Contains(logicalKey))
                    {
                        isGrouped = true;
                        break;
                    }
                }
                
                if (!isGrouped)
                {
                    InputSources.Add(new InputScopeItem(logicalKey, device.Title, false, new List<string> { logicalKey }));
                }
            }

            // 2. Add Named Groups
            foreach (var group in Context.DeviceGroups)
            {
                InputSources.Add(new InputScopeItem(group.Guid.ToString(), group.Title, true, group.MemberDeviceIdentities));
            }

            // Restore selection if possible
            if (oldSelectedId != null)
            {
                foreach (var item in InputSources)
                {
                    if (item.Id == oldSelectedId)
                    {
                        SelectedInputScope = item;
                        break;
                    }
                }
            }
        }

        public void ReplaceProfileList(ObservableCollection<ProfileItem> profileList)
        {
            var selectedId = SelectedProfileItem?.Id ?? Guid.Empty;
            ProfileList = profileList ?? new ObservableCollection<ProfileItem>();
            RebuildProfileView();
            OnPropertyChanged(nameof(ProfileList));

            if (selectedId != Guid.Empty)
            {
                SelectedProfileItem = FindProfileItem(ProfileList, selectedId);
            }
        }

        private void RebuildProfileView()
        {
            var view = CollectionViewSource.GetDefaultView(ProfileList);
            if (view != null && view.CanGroup)
            {
                view.GroupDescriptions.Clear();
                if (string.Equals(ProfileGroupingMode, "Input", StringComparison.Ordinal))
                {
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProfileItem.InputGroup)));
                }
            }

            ProfileListView = view;
            OnPropertyChanged(nameof(ProfileListView));
        }

        private static ProfileItem FindProfileItem(IEnumerable<ProfileItem> items, Guid id)
        {
            if (items == null) return null;
            foreach (var item in items)
            {
                if (item.Id == id) return item;
                var child = FindProfileItem(item.Items, id);
                if (child != null) return child;
            }
            return null;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (nameof(SelectedProfileItem).Equals(e.PropertyName) && SelectedProfileItem != null)
            {
                BuildDeviceLists();
            }
        }

        private void BuildDeviceLists()
        {
            DisposeDeviceLists();
            InputDeviceControlViewModel = new ProfileDeviceListControlViewModel(SelectedProfileItem.Profile,
                GetDeviceConfigurations(SelectedProfileItem.Profile, DeviceIoType.Input), DeviceIoType.Input, RefreshProfilePresentation);
            OutputDeviceControlViewModel = new ProfileDeviceListControlViewModel(SelectedProfileItem.Profile,
                GetDeviceConfigurations(SelectedProfileItem.Profile, DeviceIoType.Output), DeviceIoType.Output, RefreshProfilePresentation);

            OnPropertyChanged(nameof(InputDeviceControlViewModel));
            OnPropertyChanged(nameof(OutputDeviceControlViewModel));
        }


        private void DisposeDeviceLists()
        {
            InputDeviceControlViewModel?.Dispose();
            OutputDeviceControlViewModel?.Dispose();
            InputDeviceControlViewModel = null;
            OutputDeviceControlViewModel = null;
        }

        private List<DeviceConfiguration> GetDeviceConfigurations(Profile profile, DeviceIoType deviceIoType)
        {
            return SelectedProfileItem.Profile.GetDeviceConfigurationList(deviceIoType);
        }


        private void RefreshProfilePresentation()
        {
            RefreshProfilePresentation(SelectedProfileItem);
        }

        private static void RefreshProfilePresentation(ProfileItem item)
        {
            if (item == null) return;
            item.RefreshPresentation();
            foreach (var child in item.Items) RefreshProfilePresentation(child);
        }

        private void OnDeviceAliasesChangedEvent()
        {
            // Profile presentation is cached in ProfileItem. Rebuild it when aliases change so
            // the profile tree and input-group headings immediately use the friendly names too.
            ReplaceProfileList(ProfileItem.GetProfileTree(Context.Profiles));
        }

        private void OnActiveProfileChangedEvent(Profile profile)
        {
            OnPropertyChanged(nameof(ActiveProfileBreadCrumbs));
            OnPropertyChanged(nameof(CanDeactivateProfile));
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
