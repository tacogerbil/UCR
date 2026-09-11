using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Annotations;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;

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
