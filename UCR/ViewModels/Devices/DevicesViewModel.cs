using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.ViewModels.Devices
{
    public class DevicesViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Context _context;
        private readonly DevicesManager _devicesManager;

        // Exposed so the View can hand it to DeviceManagerPage (rename/hide/remove/reorder devices) --
        // that page needs the same DevicesManager this ViewModel already holds, not a second instance.
        public DevicesManager DevicesManager => _devicesManager;

        public ObservableCollection<SelectableDeviceViewModel> DetectedDevices { get; } = new ObservableCollection<SelectableDeviceViewModel>();
        public ObservableCollection<DeviceGroupViewModel> DeviceGroups { get; } = new ObservableCollection<DeviceGroupViewModel>();
        
        public ICommand GroupSelectedCommand { get; }
        public ICommand AddToGroupCommand { get; }
        public ICommand ChooseProfileCommand { get; }
        public ICommand EditProfileCommand { get; }

        // The one device or group currently picked as the Mapping target. Selecting a group's
        // checkmark or exactly one device's checkbox sets this; anything else clears it (see
        // RecomputeScopeFromDevices / DeviceGroupViewModel.IsSelectedForMapping).
        private InputScopeItem _currentScope;
        public InputScopeItem CurrentScope
        {
            get => _currentScope;
            private set
            {
                if (_currentScope == value) return;
                _currentScope = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedScope));
                OnPropertyChanged(nameof(SelectedScopeDisplayName));
                RefreshAssociatedProfile();
                ScopeSelectionChanged?.Invoke(_currentScope);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasSelectedScope => CurrentScope != null;
        public string SelectedScopeDisplayName => CurrentScope?.DisplayName;

        private Guid? _associatedProfileGuid;
        public bool HasAssociatedProfile => _associatedProfileGuid != null;
        public string AssociatedProfileName { get; private set; }

        public Action<InputScopeItem> ScopeSelectionChanged { get; set; }
        public Action<Action<Guid>> RequestChooseProfile { get; set; }
        public Action<Profile> RequestEditProfile { get; set; }
        public Action<Action<bool>> RequestRemovalOption { get; set; }

        public int SelectedCount => DetectedDevices.Count(d => d.IsSelected);
        
        public string GroupingMessage
        {
            get
            {
                var count = SelectedCount;
                if (count == 0) return "";
                if (count == 1) return "Single device selected — ready to use as-is in Mapping. No group needed.";
                return $"{count} devices selected — group them to use as one combined source in Mapping.";
            }
        }
        
        public bool CanGroup => SelectedCount > 1;
        // "Add to group" should only appear once you've actually selected a device to add — not
        // merely because a group already exists.
        public bool CanAddToExistingGroup => SelectedCount > 0 && DeviceGroups.Count > 0;

        public DevicesViewModel(Context context)
        {
            _context = context;
            _devicesManager = context.DevicesManager;
            
            GroupSelectedCommand = new RelayCommand(ExecuteGroupSelected, _ => CanGroup);
            AddToGroupCommand = new RelayCommand(ExecuteAddToGroup, _ => SelectedCount > 0);
            ChooseProfileCommand = new RelayCommand(ExecuteChooseProfile, _ => HasSelectedScope);
            EditProfileCommand = new RelayCommand(ExecuteEditProfile, _ => HasAssociatedProfile);

            _devicesManager.DeviceListChanged += OnDeviceListChanged;
            _context.DeviceGroupService.DeviceGroupsChanged += OnDeviceGroupsChanged;
            _context.ScopeProfileAssociationService.ScopeProfileAssociationsChanged += OnScopeProfileAssociationsChanged;

            Populate();
        }

        private void ExecuteChooseProfile(object parameter)
        {
            if (CurrentScope == null) return;
            RequestChooseProfile?.Invoke(profileGuid =>
            {
                _context.ScopeProfileAssociationService.SetAssociatedProfile(CurrentScope.Id, profileGuid);
            });
        }

        private void ExecuteEditProfile(object parameter)
        {
            if (!_associatedProfileGuid.HasValue) return;
            var profile = _context.ProfilesManager.FindProfileByGuid(_associatedProfileGuid.Value);
            if (profile != null) RequestEditProfile?.Invoke(profile);
        }

        private void OnScopeProfileAssociationsChanged()
        {
            App.Current.Dispatcher.Invoke(RefreshAssociatedProfile);
        }

        private void RefreshAssociatedProfile()
        {
            _associatedProfileGuid = CurrentScope == null
                ? null
                : _context.ScopeProfileAssociationService.GetAssociatedProfile(CurrentScope.Id);
            AssociatedProfileName = _associatedProfileGuid.HasValue ? _context.ProfilesManager.FindProfileByGuid(_associatedProfileGuid.Value)?.Title : null;
            OnPropertyChanged(nameof(HasAssociatedProfile));
            OnPropertyChanged(nameof(AssociatedProfileName));
            CommandManager.InvalidateRequerySuggested();
        }

        // Enforces "exactly one device, or one group, is ever the current scope": checking a group
        // clears every device checkbox and every other group's checkmark; checking a second device
        // clears the scope back to null (it's now just a pending multi-select for grouping).
        private void RecomputeScopeFromDevices()
        {
            var selected = DetectedDevices.Where(d => d.IsSelected).ToList();
            if (selected.Count != 1)
            {
                if (CurrentScope != null && CurrentScope.MemberDeviceIds.Count == 1) CurrentScope = null;
                return;
            }

            foreach (var group in DeviceGroups) group.SetSelectedForMappingInternal(false);
            var logicalKey = DeviceIdentity.BuildLogicalKey(selected[0].Device);
            CurrentScope = new InputScopeItem(logicalKey, selected[0].DisplayName, false, new List<string> { logicalKey });
        }

        internal void OnGroupSelectedForMapping(DeviceGroupViewModel group)
        {
            foreach (var d in DetectedDevices) d.IsSelected = false;
            foreach (var other in DeviceGroups)
            {
                if (other != group) other.SetSelectedForMappingInternal(false);
            }
            CurrentScope = new InputScopeItem(group.Guid.ToString(), group.Name, true, group.Members.Select(m => m.DeviceId).ToList());
        }

        internal void OnGroupDeselectedForMapping(DeviceGroupViewModel group)
        {
            if (CurrentScope != null && CurrentScope.Id == group.Guid.ToString()) CurrentScope = null;
        }

        private void ExecuteGroupSelected(object parameter)
        {
            RequestGroupName?.Invoke(name => 
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var selectedDevices = DetectedDevices.Where(d => d.IsSelected).Select(d => DeviceIdentity.BuildLogicalKey(d.Device)).ToList();
                var group = _context.DeviceGroupService.CreateGroup(name, selectedDevices);
                if (group == null)
                {
                    HidWizards.UCR.Utilities.DarkMessageBox.Show($"A group named '{name}' already exists. Choose a different name.",
                        "Duplicate group name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                foreach(var d in DetectedDevices) d.IsSelected = false;
            });
        }
        
        public Action<Action<string>> RequestGroupName { get; set; }

        private void ExecuteAddToGroup(object parameter)
        {
            if (!(parameter is Guid groupGuid)) return;
            var selectedDevices = DetectedDevices.Where(d => d.IsSelected).Select(d => DeviceIdentity.BuildLogicalKey(d.Device)).ToList();
            foreach (var deviceId in selectedDevices)
            {
                _context.DeviceGroupService.AddDeviceToGroup(groupGuid, deviceId);
            }
            foreach (var d in DetectedDevices) d.IsSelected = false;
        }

        private void OnDeviceListChanged()
        {
            App.Current.Dispatcher.Invoke(() => Populate());
        }
        
        private void OnDeviceGroupsChanged()
        {
            App.Current.Dispatcher.Invoke(() => PopulateGroups());
        }

        private void Populate()
        {
            var liveDevices = _devicesManager.GetAvailableDeviceList(DeviceIoType.Input, false);
            
            var selectedIds = DetectedDevices.Where(d => d.IsSelected).Select(d => DeviceIdentity.BuildLogicalKey(d.Device)).ToHashSet();
            
            foreach (var d in DetectedDevices) d.PropertyChanged -= DeviceSelectionChanged;
            DetectedDevices.Clear();

            foreach (var device in liveDevices)
            {
                var vm = new SelectableDeviceViewModel(device);
                if (selectedIds.Contains(DeviceIdentity.BuildLogicalKey(device))) vm.IsSelected = true;
                vm.PropertyChanged += DeviceSelectionChanged;
                DetectedDevices.Add(vm);
            }
            PopulateGroups();
        }
        
        private void PopulateGroups()
        {
            var previouslySelectedGuid = DeviceGroups.FirstOrDefault(g => g.IsSelectedForMapping)?.Guid;
            DeviceGroups.Clear();
            var previouslySelectedGroupStillExists = false;
            foreach (var group in _context.DeviceGroups)
            {
                var vm = new DeviceGroupViewModel(group, _context, this);
                if (previouslySelectedGuid.HasValue && group.Guid == previouslySelectedGuid.Value)
                {
                    vm.SetSelectedForMappingInternal(true);
                    previouslySelectedGroupStillExists = true;
                }
                DeviceGroups.Add(vm);
            }
            // The selected group can vanish out from under CurrentScope (deleted from another view,
            // or by this same instance's own RemoveGroupCommand) -- without this, CurrentScope keeps
            // pointing at a group Guid/Name that no longer exists in DeviceGroups at all, and anything
            // downstream (ScopeSelectionChanged -> Dashboard.SelectedInputScope, Begin Mapping) keeps
            // treating the deleted group as the live mapping target.
            if (previouslySelectedGuid.HasValue && !previouslySelectedGroupStillExists) CurrentScope = null;
            OnPropertyChanged(nameof(CanAddToExistingGroup));
        }

        private void DeviceSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableDeviceViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(GroupingMessage));
                OnPropertyChanged(nameof(CanGroup));
                OnPropertyChanged(nameof(CanAddToExistingGroup));
                RecomputeScopeFromDevices();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void Dispose()
        {
            _devicesManager.DeviceListChanged -= OnDeviceListChanged;
            _context.DeviceGroupService.DeviceGroupsChanged -= OnDeviceGroupsChanged;
            _context.ScopeProfileAssociationService.ScopeProfileAssociationsChanged -= OnScopeProfileAssociationsChanged;
            foreach (var d in DetectedDevices) d.PropertyChanged -= DeviceSelectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectableDeviceViewModel : INotifyPropertyChanged
    {
        public Device Device { get; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName => Device.Title;
        public string ConnectionType => Device.ProviderName; 
        
        public SelectableDeviceViewModel(Device device)
        {
            Device = device;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DeviceGroupViewModel : INotifyPropertyChanged
    {
        private readonly DeviceGroup _group;
        private readonly Context _context;
        private readonly DevicesViewModel _parent;

        public Guid Guid => _group.Guid;

        private bool _isSelectedForMapping;
        public bool IsSelectedForMapping
        {
            get => _isSelectedForMapping;
            set
            {
                if (_isSelectedForMapping == value) return;
                _isSelectedForMapping = value;
                OnPropertyChanged();
                if (value) _parent?.OnGroupSelectedForMapping(this);
                else _parent?.OnGroupDeselectedForMapping(this);
            }
        }

        // Set by DevicesViewModel when enforcing selection exclusivity; does not re-notify the parent.
        internal void SetSelectedForMappingInternal(bool value)
        {
            if (_isSelectedForMapping == value) return;
            _isSelectedForMapping = value;
            OnPropertyChanged(nameof(IsSelectedForMapping));
        }

        public string Name
        {
            get => _group.Title;
            set
            {
                if (_group.Title == value) return;
                if (!_context.DeviceGroupService.RenameGroup(_group.Guid, value))
                {
                    HidWizards.UCR.Utilities.DarkMessageBox.Show($"A group named '{value}' already exists. Choose a different name.",
                        "Duplicate group name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OnPropertyChanged(); // revert bound control back to the stored title
                    return;
                }
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<DeviceGroupMemberViewModel> Members { get; } = new ObservableCollection<DeviceGroupMemberViewModel>();
        
        public ICommand RemoveGroupCommand { get; }

        public DeviceGroupViewModel(DeviceGroup group, Context context, DevicesViewModel parent = null)
        {
            _group = group;
            _context = context;
            _parent = parent;

            RemoveGroupCommand = new RelayCommand(ExecuteRemoveGroup);

            foreach (var devId in group.MemberDeviceIdentities)
            {
                Members.Add(new DeviceGroupMemberViewModel(devId, this, context, parent));
            }
        }
        
        private void ExecuteRemoveGroup(object parameter)
        {
            _context.DeviceGroupService.RemoveGroup(_group.Guid);
        }

        public void RemoveMember(string deviceId, bool deleteMappings)
        {
            _context.DeviceGroupService.RemoveDeviceFromGroup(_group.Guid, deviceId);
            if (deleteMappings)
            {
                foreach (var profile in _context.Profiles)
                {
                    ClearMappingsForDevice(profile, deviceId);
                }
            }
        }

        private void ClearMappingsForDevice(Profile profile, string deviceId)
        {
            foreach (var mapping in profile.Mappings)
            {
                foreach (var binding in mapping.DeviceBindings)
                {
                    if (!binding.IsBound) continue;
                    
                    var config = profile.GetDeviceConfiguration(binding.DeviceIoType, binding.DeviceConfigurationGuid);
                    if (config?.Device != null && HidWizards.UCR.Core.Managers.DeviceIdentity.BuildLogicalKey(config.Device) == deviceId)
                    {
                        binding.ClearBinding();
                    }
                }
            }
            
            foreach (var childProfile in profile.ChildProfiles)
            {
                ClearMappingsForDevice(childProfile, deviceId);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DeviceGroupMemberViewModel : INotifyPropertyChanged
    {
        private readonly string _deviceId;
        private readonly DeviceGroupViewModel _parent;
        private readonly Context _context;
        // Held as a live reference, not a snapshotted delegate: DevicesViewModel.RequestRemovalOption
        // is only assigned by DevicesView.xaml.cs's DataContextChanged handler *after* the
        // DevicesViewModel constructor (and its first PopulateGroups/member-construction pass)
        // already ran, so capturing the delegate itself at construction time would read null.
        // Reading it lazily off the live DevicesViewModel at click time is always correct.
        private readonly DevicesViewModel _devicesViewModel;

        public string DeviceId => _deviceId;

        public string DisplayName
        {
            get
            {
                var dev = _context.DevicesManager.GetAvailableDeviceList(DeviceIoType.Input, false)
                    .FirstOrDefault(d => DeviceIdentity.BuildLogicalKey(d) == _deviceId);
                return dev?.Title ?? _deviceId;
            }
        }

        public ICommand RemoveMemberCommand { get; }

        public DeviceGroupMemberViewModel(string deviceId, DeviceGroupViewModel parent, Context context, DevicesViewModel devicesViewModel)
        {
            _deviceId = deviceId;
            _parent = parent;
            _context = context;
            _devicesViewModel = devicesViewModel;

            RemoveMemberCommand = new RelayCommand(ExecuteRemoveMember);
        }

        private void ExecuteRemoveMember(object parameter)
        {
            _devicesViewModel?.RequestRemovalOption?.Invoke(deleteMappings =>
            {
                _parent.RemoveMember(_deviceId, deleteMappings);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
