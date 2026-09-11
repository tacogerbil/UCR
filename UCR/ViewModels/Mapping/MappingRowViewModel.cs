using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core;
using System.Linq;
using System.Collections.ObjectModel;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class MappingRowViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;
        private readonly Core.Models.Mapping _mapping;
        
        public string Title { get; }
        public string TargetOutputKey { get; }
        public bool IsAxis { get; }

        public ObservableCollection<PluginSummaryViewModel> Plugins { get; }

        private bool _isAdvancedExpanded;
        public bool IsAdvancedExpanded
        {
            get => _isAdvancedExpanded;
            set
            {
                if (_isAdvancedExpanded != value)
                {
                    _isAdvancedExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public InputScopeItem CurrentScope { get; set; }
        public ObservableCollection<InputScopeItem> FullCatalog { get; set; }

        public ICommand ListenCommand { get; }
        public ICommand ClearCommand { get; }

        public MappingRowViewModel(Context context, Core.Models.Mapping mapping, string title, string targetOutputKey, bool isAxis)
        {
            _context = context;
            _mapping = mapping;
            Title = title;
            TargetOutputKey = targetOutputKey;
            IsAxis = isAxis;
            
            ListenCommand = new RelayCommand(ExecuteListen);
            ClearCommand = new RelayCommand(ExecuteClear);

            Plugins = new ObservableCollection<PluginSummaryViewModel>();
            foreach (var plugin in _mapping.Plugins)
            {
                Plugins.Add(new PluginSummaryViewModel(plugin));
            }
        }

        private bool _isListening;
        public bool IsListening
        {
            get => _isListening;
            set
            {
                if (_isListening != value)
                {
                    _isListening = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBound));
                }
            }
        }

        public bool IsBound => _mapping != null && _mapping.DeviceBindings.Count > 0 && _mapping.DeviceBindings[0].IsBound;
        
        public string PrimarySourceDisplayName
        {
            get
            {
                if (_mapping == null || _mapping.DeviceBindings.Count == 0) return string.Empty;
                var bind = _mapping.DeviceBindings[0];
                return bind.IsBound ? bind.BoundName() : string.Empty;
            }
        }
        
        public ObservableCollection<ContextMenuItem> ManualPickerMenu => BuildManualPickerMenu();
        
        private DeviceBinding GetOrAddBinding()
        {
            if (_mapping.DeviceBindings.Count == 0)
            {
                _mapping.DeviceBindings.Add(new DeviceBinding(null, _context.ActiveProfile, DeviceIoType.Input));
            }
            if (_mapping.Plugins.Count == 0)
            {
                var pluginName = IsAxis ? "Axis to Axis" : "Button to Button";
                var templatePlugin = _context.PluginManager.Plugins.FirstOrDefault(p => p.PluginName == pluginName);
                if (templatePlugin != null)
                {
                    var newPlugin = _context.PluginManager.GetNewPlugin(templatePlugin);
                    _mapping.AddPlugin(newPlugin);
                }
            }
            
            var binding = _mapping.DeviceBindings[0];
            return binding;
        }

        private void ExecuteListen(object parameter)
        {
            var binding = GetOrAddBinding();
            
            if (!IsListening)
            {
                binding.PropertyChanged += Binding_PropertyChanged;
                binding.EnterBindMode();
                IsListening = true;
            }
            else
            {
                // Can't trivially cancel bind mode, but we can reset our local state if needed.
                // In UCR, bind mode ends automatically when an input is detected or timeout.
            }
        }

        private void Binding_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceBinding.IsInBindMode))
            {
                var binding = sender as DeviceBinding;
                if (binding != null && !binding.IsInBindMode)
                {
                    binding.PropertyChanged -= Binding_PropertyChanged;
                    IsListening = false;
                    OnPropertyChanged(nameof(IsBound));
                    OnPropertyChanged(nameof(PrimarySourceDisplayName));
                    _context.ContextChanged();
                }
            }
        }

        private void ExecuteClear(object parameter)
        {
            if (_mapping != null && _mapping.DeviceBindings.Count > 0)
            {
                var binding = _mapping.DeviceBindings[0];
                binding.ClearBinding();
                OnPropertyChanged(nameof(IsBound));
                OnPropertyChanged(nameof(PrimarySourceDisplayName));
            }
        }

        private ObservableCollection<ContextMenuItem> BuildManualPickerMenu()
        {
            var menuList = new ObservableCollection<ContextMenuItem>();
            if (_context.ActiveProfile == null) return menuList;
            
            var devicesManager = _context.DevicesManager;
            var deviceConfigurationList = _context.ActiveProfile.GetDeviceConfigurationList(DeviceIoType.Input)
                .OrderBy(c => devicesManager.GetDeviceSortOrder(c.Device))
                .ToList();

            // Filter by CurrentScope if provided and it has MemberDeviceIds
            if (CurrentScope != null && CurrentScope.MemberDeviceIds != null && CurrentScope.MemberDeviceIds.Any())
            {
                deviceConfigurationList = deviceConfigurationList
                    .Where(c => CurrentScope.MemberDeviceIds.Contains(DeviceIdentity.BuildLogicalKey(c.Device)))
                    .ToList();
            }

            foreach (var deviceConfig in deviceConfigurationList)
            {
                var title = deviceConfig.GetFullTitleForProfile(_context.ActiveProfile);
                var deviceNodes = deviceConfig.Device.GetDeviceBindingMenu(_context, DeviceIoType.Input);
                var subMenu = BuildSubMenu(deviceNodes, deviceConfig.Guid);
                if (subMenu.Count > 0)
                {
                    menuList.Add(new ContextMenuItem(title, subMenu));
                }
            }

            return menuList;
        }

        private ObservableCollection<ContextMenuItem> BuildSubMenu(List<DeviceBindingNode> deviceBindingNodes, Guid deviceConfigurationGuid)
        {
            var menuList = new ObservableCollection<ContextMenuItem>();
            if (deviceBindingNodes == null) return menuList;

            foreach (var node in deviceBindingNodes)
            {
                RelayCommand cmd = null;
                if (node.IsBinding)
                {
                    cmd = new RelayCommand(c =>
                    {
                        var binding = GetOrAddBinding();
                        binding.SetDeviceConfigurationGuid(deviceConfigurationGuid);
                        binding.SetKeyTypeValue(node.DeviceBindingInfo.KeyType, node.DeviceBindingInfo.KeyValue, node.DeviceBindingInfo.KeySubValue);
                        OnPropertyChanged(nameof(IsBound));
                        OnPropertyChanged(nameof(PrimarySourceDisplayName));
                        _context.ContextChanged();
                    });
                }

                var menu = new ContextMenuItem(node.Title, BuildSubMenu(node.ChildrenNodes, deviceConfigurationGuid), cmd);
                if (node.IsBinding || (!node.IsBinding && menu.Children.Count > 0))
                {
                    menuList.Add(menu);
                }
            }

            return menuList;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
