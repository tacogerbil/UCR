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
using HidWizards.UCR.Core.Services;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class MappingRowViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;
        private readonly Core.Models.Mapping _mapping;

        // Row identity, resolved once from the selected Output Device's own binding tree (see
        // OutputSlotResolver) rather than a hardcoded key table. The row is fully recreated by
        // PatchBayViewModel.PopulateRows() whenever the profile or output device changes, so these
        // don't need to be mutable/observable.
        private readonly Guid _outputDeviceConfigurationGuid;
        private readonly int _outputKeyType;
        private readonly int _outputKeyValue;
        private readonly int _outputKeySubValue;

        // Plugin template lookup by name, defaulting to the real MEF-discovered catalog in
        // production. Exposed as an injectable seam (internal ctor overload below) because
        // PluginManager.Plugins is only ever populated when a "Plugins" folder sits next to the
        // running executable (see UCR.Plugins.csproj's post-build copy step) — that folder is never
        // populated next to the test runner's output, so PluginManager.Plugins is always empty in
        // UCR.Tests. Without this seam, GetOrCreateNextBinding/SwapToMergerPlugin are untestable.
        private readonly Func<string, Plugin> _resolvePluginTemplate;

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

        private short _currentValue;
        // Matches DeviceBindingViewModel.GetPreviewValue's existing convention for the same kind of
        // bipolar-axis preview (centered at 50, moving left/right) rather than a magnitude-only 0-100,
        // so a Patch Bay row and the Advanced panel's per-binding preview agree with each other.
        public float InputValue => IsAxis ? 50f + (float)_currentValue / Core.Utilities.Constants.AxisMaxValue * 50f : 0;
        public bool IsPressed => !IsAxis && _currentValue > 0;
        public bool IsMerged => _mapping.Plugins.Count > 0 && (_mapping.Plugins[0].PluginName == "Axis Merger" || _mapping.Plugins[0].PluginName == "Button Merger");

        private void UpdateLiveValues()
        {
            OnPropertyChanged(nameof(InputValue));
            OnPropertyChanged(nameof(IsPressed));
        }

        public MappingRowViewModel(Context context, Core.Models.Mapping mapping, OutputSlot outputSlot, Guid outputDeviceConfigurationGuid)
            : this(context, mapping, outputSlot, outputDeviceConfigurationGuid,
                pluginName => context.PluginManager.Plugins.FirstOrDefault(p => p.PluginName == pluginName))
        {
        }

        internal MappingRowViewModel(Context context, Core.Models.Mapping mapping, OutputSlot outputSlot,
            Guid outputDeviceConfigurationGuid, Func<string, Plugin> resolvePluginTemplate)
        {
            _context = context;
            _mapping = mapping;
            _resolvePluginTemplate = resolvePluginTemplate;

            Title = outputSlot.Title;
            TargetOutputKey = outputSlot.SlotKey;
            // Range (analog) slots render as a slider row; everything else (Momentary/Event/Delta)
            // renders as a button row. See OutputSlotResolver's Delta caveat for the known gap there.
            IsAxis = outputSlot.Category == DeviceBindingCategory.Range;

            _outputDeviceConfigurationGuid = outputDeviceConfigurationGuid;
            _outputKeyType = outputSlot.KeyType;
            _outputKeyValue = outputSlot.KeyValue;
            _outputKeySubValue = outputSlot.KeySubValue;

            ListenCommand = new RelayCommand(ExecuteListen);
            ClearCommand = new RelayCommand(ExecuteClear);

            Plugins = new ObservableCollection<PluginSummaryViewModel>();
            foreach (var plugin in _mapping.Plugins)
            {
                Plugins.Add(new PluginSummaryViewModel(plugin, _mapping.DeviceBindings));
            }

            SubscribeToOutput();
        }

        private void SubscribeToOutput()
        {
            UnsubscribeFromOutput();
            if (_mapping != null && _mapping.Plugins.Count > 0 && _mapping.Plugins[0].Outputs.Count > 0)
            {
                _mapping.Plugins[0].Outputs[0].PropertyChanged += OutputBinding_PropertyChanged;
            }
        }

        private void UnsubscribeFromOutput()
        {
            if (_mapping != null && _mapping.Plugins.Count > 0 && _mapping.Plugins[0].Outputs.Count > 0)
            {
                _mapping.Plugins[0].Outputs[0].PropertyChanged -= OutputBinding_PropertyChanged;
            }
        }

        private void OutputBinding_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceBinding.CurrentValue))
            {
                var binding = sender as DeviceBinding;
                if (binding != null && _context.ActiveProfile != null && binding.Profile.IsActive())
                {
                    _currentValue = binding.CurrentValue;
                    UpdateLiveValues();
                }
                else if (_currentValue != 0)
                {
                    _currentValue = 0;
                    UpdateLiveValues();
                }
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

        private string _primarySourceDisplayNameOverride;

        public string PrimarySourceDisplayName
        {
            get
            {
                if (_mapping == null || _mapping.DeviceBindings.Count == 0) return string.Empty;
                var bind = _mapping.DeviceBindings[0];
                return bind.IsBound ? bind.BoundName() : string.Empty;
            }
            set
            {
                if (_primarySourceDisplayNameOverride != value)
                {
                    _primarySourceDisplayNameOverride = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ContextMenuItem> ManualPickerMenu => BuildManualPickerMenu();

        // internal (not private) so UCR.Tests can drive the plugin-creation/merge-swap logic
        // directly, bypassing the UI commands that wrap it (ListenCommand/the manual picker), which
        // also touch BindingManager/IOController hardware-detection paths this logic doesn't need.
        internal DeviceBinding GetOrCreateNextBinding()
        {
            if (_mapping.Plugins.Count == 0)
            {
                var pluginName = IsAxis ? "Axis to Axis" : "Button to Button";
                var templatePlugin = _resolvePluginTemplate(pluginName);
                if (templatePlugin != null)
                {
                    var newPlugin = _context.PluginManager.GetNewPlugin(templatePlugin);
                    _mapping.AddPlugin(newPlugin);
                    ResolveOutputBinding(newPlugin);
                    Plugins.Add(new PluginSummaryViewModel(newPlugin, _mapping.DeviceBindings));
                    SubscribeToOutput();
                }
                return _mapping.DeviceBindings.FirstOrDefault();
            }

            if (!IsMerged && _mapping.DeviceBindings.Count == 1 && _mapping.DeviceBindings[0].IsBound)
            {
                SwapToMergerPlugin();
                return _mapping.DeviceBindings.Count > 1 ? _mapping.DeviceBindings[1] : null;
            }

            return _mapping.DeviceBindings.FirstOrDefault(b => !b.IsBound) ?? _mapping.DeviceBindings.LastOrDefault();
        }

        private void SwapToMergerPlugin()
        {
            if (_mapping.Plugins.Count == 0) return;
            var oldPlugin = _mapping.Plugins[0];
            var oldBinding = _mapping.DeviceBindings[0];

            // Save old binding state
            var oldGuid = oldBinding.DeviceConfigurationGuid;
            var oldType = oldBinding.KeyType;
            var oldValue = oldBinding.KeyValue;
            var oldSubValue = oldBinding.KeySubValue;
            var oldIsBound = oldBinding.IsBound;

            var mergerPluginName = IsAxis ? "Axis Merger" : "Button Merger";
            var templatePlugin = _resolvePluginTemplate(mergerPluginName);
            
            if (templatePlugin != null)
            {
                var newPlugin = _context.PluginManager.GetNewPlugin(templatePlugin);
                
                _mapping.RemovePlugin(oldPlugin);
                _mapping.AddPlugin(newPlugin);
                
                ResolveOutputBinding(newPlugin);
                
                // Restore old binding
                if (oldIsBound)
                {
                    _mapping.DeviceBindings[0].SetDeviceConfigurationGuid(oldGuid);
                    _mapping.DeviceBindings[0].SetKeyTypeValue(oldType, oldValue, oldSubValue);
                }
                
                Plugins.Clear();
                Plugins.Add(new PluginSummaryViewModel(newPlugin, _mapping.DeviceBindings));

                OnPropertyChanged(nameof(IsMerged));
                SubscribeToOutput();
            }
        }

        // Points the plugin's single Output at this row's own slot on the selected Output Device,
        // using the same two calls the manual Input picker already makes (SetDeviceConfigurationGuid
        // + SetKeyTypeValue) — deliberately not a new binding-resolution path. Every plugin this row
        // can hold (1:1 remapper or, later, a merge plugin) has exactly one Output category, so
        // Outputs[0] is always the row's target regardless of which plugin is currently active.
        private void ResolveOutputBinding(Plugin plugin)
        {
            if (plugin == null || plugin.Outputs.Count == 0) return;
            if (_outputDeviceConfigurationGuid == Guid.Empty) return;

            var outputBinding = plugin.Outputs[0];
            outputBinding.SetDeviceConfigurationGuid(_outputDeviceConfigurationGuid);
            outputBinding.SetKeyTypeValue(_outputKeyType, _outputKeyValue, _outputKeySubValue);
            
            SubscribeToOutput();
        }

        private void ExecuteListen(object parameter)
        {
            if (IsMerged)
            {
                IsAdvancedExpanded = true;
                return;
            }

            var binding = GetOrCreateNextBinding();
            if (!IsListening)
            {
                // DeviceBinding.DeviceBindingCategory has no ctor default and Mapping.AddPlugin never
                // sets it, so it sits at the enum's default (Event) unless something assigns it here.
                // BindingManager only accepts a physical input whose category matches this exactly, and
                // no real device ever reports "Event" (only the Button-to-Event plugin's own *output*
                // does) -- leaving this unset means the Listen button can never capture anything.
                binding.DeviceBindingCategory = IsAxis ? DeviceBindingCategory.Range : DeviceBindingCategory.Momentary;
                binding.PropertyChanged += Binding_PropertyChanged;
                binding.EnterBindMode();
                PrimarySourceDisplayName = "Press Input...";
                IsListening = true;
            }
            else
            {
                EndListening(binding);
            }
        }

        private void EndListening(DeviceBinding binding)
        {
            binding.PropertyChanged -= Binding_PropertyChanged;
            IsListening = false;
        }

        private void ExecuteClear(object parameter)
        {
            if (IsMerged)
            {
                IsAdvancedExpanded = true;
                return;
            }

            if (_mapping.DeviceBindings.Count > 0)
            {
                var binding = _mapping.DeviceBindings[0];
                binding.ClearBinding();
                _primarySourceDisplayNameOverride = null;
                OnPropertyChanged(nameof(IsBound));
                OnPropertyChanged(nameof(PrimarySourceDisplayName));
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
                    _primarySourceDisplayNameOverride = null;
                    OnPropertyChanged(nameof(IsBound));
                    OnPropertyChanged(nameof(PrimarySourceDisplayName));
                    _context.ContextChanged();
                }
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
                        if (IsMerged)
                        {
                            IsAdvancedExpanded = true;
                            return;
                        }

                        var binding = GetOrCreateNextBinding();
                        if (binding != null)
                        {
                            binding.SetDeviceConfigurationGuid(deviceConfigurationGuid);
                            binding.SetKeyTypeValue(node.DeviceBindingInfo.KeyType, node.DeviceBindingInfo.KeyValue, node.DeviceBindingInfo.KeySubValue);
                            PrimarySourceDisplayName = binding.BoundName();
                        }
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
