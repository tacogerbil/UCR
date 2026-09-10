using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Devices
{
    public class DevicesViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Context _context;
        private readonly DevicesManager _devicesManager;
        
        public ObservableCollection<SelectableDeviceViewModel> DetectedDevices { get; } = new ObservableCollection<SelectableDeviceViewModel>();
        public ObservableCollection<DeviceGroupViewModel> DeviceGroups { get; } = new ObservableCollection<DeviceGroupViewModel>();
        
        public ICommand GroupSelectedCommand { get; }
        
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

        public DevicesViewModel(Context context)
        {
            _context = context;
            _devicesManager = context.DevicesManager;
            
            GroupSelectedCommand = new RelayCommand(ExecuteGroupSelected, _ => CanGroup);
            
            _devicesManager.DeviceListChanged += OnDeviceListChanged;
            _context.DeviceGroupService.DeviceGroupsChanged += OnDeviceGroupsChanged;
            
            Populate();
        }

        private void ExecuteGroupSelected(object parameter)
        {
            RequestGroupName?.Invoke(name => 
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var selectedDevices = DetectedDevices.Where(d => d.IsSelected).Select(d => DeviceIdentity.BuildLogicalKey(d.Device)).ToList();
                _context.DeviceGroupService.CreateGroup(name, selectedDevices);
                foreach(var d in DetectedDevices) d.IsSelected = false;
            });
        }
        
        public Action<Action<string>> RequestGroupName { get; set; }

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
            DeviceGroups.Clear();
            foreach (var group in _context.DeviceGroups)
            {
                DeviceGroups.Add(new DeviceGroupViewModel(group, _context));
            }
        }

        private void DeviceSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableDeviceViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(GroupingMessage));
                OnPropertyChanged(nameof(CanGroup));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void Dispose()
        {
            _devicesManager.DeviceListChanged -= OnDeviceListChanged;
            _context.DeviceGroupService.DeviceGroupsChanged -= OnDeviceGroupsChanged;
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

        public Guid Guid => _group.Guid;
        
        public string Name
        {
            get => _group.Title;
            set
            {
                if (_group.Title == value) return;
                _context.DeviceGroupService.RenameGroup(_group.Guid, value);
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<DeviceGroupMemberViewModel> Members { get; } = new ObservableCollection<DeviceGroupMemberViewModel>();
        
        public ICommand RemoveGroupCommand { get; }

        public DeviceGroupViewModel(DeviceGroup group, Context context)
        {
            _group = group;
            _context = context;
            
            RemoveGroupCommand = new RelayCommand(ExecuteRemoveGroup);
            
            foreach (var devId in group.MemberDeviceIdentities)
            {
                Members.Add(new DeviceGroupMemberViewModel(devId, this, context));
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
                // To be handled in Phase 3
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

        public DeviceGroupMemberViewModel(string deviceId, DeviceGroupViewModel parent, Context context)
        {
            _deviceId = deviceId;
            _parent = parent;
            _context = context;
            
            RemoveMemberCommand = new RelayCommand(ExecuteRemoveMember);
        }
        
        private void ExecuteRemoveMember(object parameter)
        {
            RequestRemovalOption?.Invoke(deleteMappings => 
            {
                _parent.RemoveMember(_deviceId, deleteMappings);
            });
        }
        
        public static Action<Action<bool>> RequestRemovalOption { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
