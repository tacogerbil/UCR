using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using HidWizards.UCR.Core.Annotations;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Utilities;
using HidWizards.UCR.ViewModels.Presentation;
using KeyInterop = System.Windows.Input.KeyInterop;

namespace HidWizards.UCR.ViewModels.ProfileViewModels
{
    public class DeviceBindingViewModel : INotifyPropertyChanged, IDisposable
    {
        // A pseudo-entry appended to Devices for Momentary input bindings, alongside real device
        // configurations -- selecting it never resolves to a real DeviceConfiguration (there isn't
        // one), it starts DeviceBinding.AuxiliaryKeyboardKeyCode capture instead. Fixed and distinct
        // from Guid.Empty (already used for "no device"/unavailable).
        public static readonly Guid VirtualKeyboardSentinelGuid = new Guid("6B195B0E-1B6B-4B6E-9B1A-56B1F5D0C6F1");

        public string DeviceBindingName { get; set; }
        public string IoTypeName => DeviceBinding.DeviceIoType.Equals(DeviceIoType.Input) ? "Input" : "Output";
        public DeviceBindingCategory DeviceBindingCategory { get; set; }
        public ObservableCollection<ComboBoxItemViewModel> Devices { get; set; }
        public ComboBoxItemViewModel SelectedDevice { get; set; }
        public Visibility ShowPreview => DeviceBinding.IsInBindMode ? Visibility.Hidden : Visibility.Visible;
        public Visibility ShowBindMode => ShowPreview.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
        public Visibility ShowPropertyList => PluginPropertyGroup == null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ShowBlock => DeviceBinding.DeviceIoType == DeviceIoType.Input && DeviceBinding.IsBlockable()
            ? Visibility.Visible
            : Visibility.Collapsed;
        public Visibility ShowInvertInput => DeviceBinding.DeviceIoType == DeviceIoType.Input &&
                                             DeviceBindingCategory == DeviceBindingCategory.Range
            ? Visibility.Visible
            : Visibility.Collapsed;
        public PluginPropertyGroupViewModel PluginPropertyGroup { get; set; }
        public double PreviewValue => GetPreviewValue();
        public bool ShowButtonPreview => DeviceBinding.IsInBindMode || DeviceBinding.Profile.IsActive();

        private bool GuiInvalidated { get; set; }
        private bool _disposed;

        private double GetPreviewValue()
        {
            if (DeviceBinding.IsInBindMode)
            {
                return BindModeProgress;
            } else if (DeviceBinding.Profile.IsActive())
            {
                switch (DeviceBindingCategory)
                {
                    case DeviceBindingCategory.Momentary:
                        return 100 * CurrentValue;
                    case DeviceBindingCategory.Range:
                        return (long) (50.0 + ((double) CurrentValue / Constants.AxisMaxValue) * 50);
                    case DeviceBindingCategory.Event:
                    case DeviceBindingCategory.Delta:
                    default:
                        return 0;
                }
            }

            return 0;
        }

        private bool _bindingEnabled;
        public bool BindingEnabled
        {
            get => _bindingEnabled;
            set
            {
                _bindingEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewValue));
                OnPropertyChanged(nameof(ShowButtonPreview));
            }
        }

        public bool Block
        {
            get => DeviceBinding.Block;
            set => DeviceBinding.SetBlock(value);
        }

        public bool InvertInput
        {
            get => DeviceBinding.InvertInput;
            set => DeviceBinding.SetInvertInput(value);
        }

        public string BindButtonText
        {
            get
            {
                if (IsCapturingAuxiliaryKey) return "Press a key to send…";
                if (IsAuxiliaryKeyboardArmed) return "Click to capture key";

                if (DeviceBinding.IsInBindMode) return "Press input device";
                if (DeviceBinding.IsBound)
                {
                    var boundName = DeviceBinding.BoundName();
                    return HasAuxiliaryKeyboardKey ? $"{boundName} + ⌨ {AuxiliaryKeyboardKeyName}" : boundName;
                }
                // Unbound: showing "Click to bind" alongside an already-captured key reads as if the
                // key itself weren't bound yet. It is -- just show it, the same way a bound physical
                // input's name replaces "Click to bind" entirely rather than appending to it.
                return HasAuxiliaryKeyboardKey ? $"⌨ {AuxiliaryKeyboardKeyName}" : "Click to bind";
            }
        }

        // Offered only for Momentary input bindings -- an axis crossing some threshold doesn't map
        // cleanly to a key down/up, and that's not what this feature was asked for.
        public bool ShowVirtualKeyboardOption => DeviceBinding.DeviceIoType == DeviceIoType.Input
            && DeviceBindingCategory == DeviceBindingCategory.Momentary;

        // Two steps, deliberately not one: picking "Virtual Keyboard" from the dropdown only arms the
        // button (IsAuxiliaryKeyboardArmed) -- it can't itself grab WPF keyboard focus reliably (a
        // ComboBox selection leaves focus on the ComboBox, or nowhere, once its dropdown Popup closes).
        // Actually starting capture (IsCapturingAuxiliaryKey) happens from BindButton's own Click,
        // which WPF focuses automatically -- the same reliable mechanism KeyCaptureControl's
        // click-then-press gesture already relies on.
        public bool IsAuxiliaryKeyboardArmed { get; private set; }
        public bool IsCapturingAuxiliaryKey { get; private set; }

        public ushort AuxiliaryKeyboardKeyCode => DeviceBinding.AuxiliaryKeyboardKeyCode;
        public bool HasAuxiliaryKeyboardKey => AuxiliaryKeyboardKeyCode != 0;
        public string AuxiliaryKeyboardKeyName => HasAuxiliaryKeyboardKey
            ? KeyInterop.KeyFromVirtualKey(AuxiliaryKeyboardKeyCode).ToString()
            : null;

        // Selecting the "Virtual Keyboard" pseudo-entry doesn't resolve to a real DeviceConfiguration
        // (ChangeDeviceConfiguration would just no-op), so the code-behind calls this instead of that.
        public void BeginCapturingAuxiliaryKey()
        {
            IsAuxiliaryKeyboardArmed = true;
            OnPropertyChanged(nameof(IsAuxiliaryKeyboardArmed));
            OnPropertyChanged(nameof(BindButtonText));
        }

        // Called from BindButton's Click while armed -- this is the moment that actually needs
        // keyboard focus, and a Button.Click reliably has it.
        public void StartCapturingAuxiliaryKey()
        {
            IsAuxiliaryKeyboardArmed = false;
            IsCapturingAuxiliaryKey = true;
            OnPropertyChanged(nameof(IsAuxiliaryKeyboardArmed));
            OnPropertyChanged(nameof(IsCapturingAuxiliaryKey));
            OnPropertyChanged(nameof(BindButtonText));
        }

        public void CancelCapturingAuxiliaryKey()
        {
            if (!IsAuxiliaryKeyboardArmed && !IsCapturingAuxiliaryKey) return;
            IsAuxiliaryKeyboardArmed = false;
            IsCapturingAuxiliaryKey = false;
            OnPropertyChanged(nameof(IsAuxiliaryKeyboardArmed));
            OnPropertyChanged(nameof(IsCapturingAuxiliaryKey));
            OnPropertyChanged(nameof(BindButtonText));
        }

        public void SetAuxiliaryKeyboardKeyCode(ushort keyCode)
        {
            DeviceBinding.AuxiliaryKeyboardKeyCode = keyCode;
            IsAuxiliaryKeyboardArmed = false;
            IsCapturingAuxiliaryKey = false;
            OnPropertyChanged(nameof(IsAuxiliaryKeyboardArmed));
            OnPropertyChanged(nameof(IsCapturingAuxiliaryKey));
            OnPropertyChanged(nameof(BindButtonText));
            OnPropertyChanged(nameof(AuxiliaryKeyboardKeyCode));
            OnPropertyChanged(nameof(HasAuxiliaryKeyboardKey));
            OnPropertyChanged(nameof(AuxiliaryKeyboardKeyName));
        }

        public void ClearAuxiliaryKeyboardKey()
        {
            SetAuxiliaryKeyboardKeyCode(0);
        }

        private DeviceBinding _deviceBinding;
        public DeviceBinding DeviceBinding
        {
            get => _deviceBinding;
            set
            {
                if (ReferenceEquals(_deviceBinding, value)) return;
                if (_deviceBinding != null) _deviceBinding.PropertyChanged -= DeviceBindingOnPropertyChanged;
                _deviceBinding = value;
                if (_deviceBinding == null) return;
                _deviceBinding.PropertyChanged += DeviceBindingOnPropertyChanged;
                CurrentValue = _deviceBinding.CurrentValue;
            }
        }

        private long _currentValue;
        public long CurrentValue
        {
            get => _currentValue;
            set
            {
                if (_currentValue == value) return;
                _currentValue = value;
                GuiInvalidated = true;
                OnPropertyChanged(nameof(ShowButtonPreview));
            }
        }

        private double _bindModeProgress;
        public double BindModeProgress
        {
            get => _bindModeProgress;
            set
            {
                if (Math.Abs(_bindModeProgress - value) < 0.001) return;
                _bindModeProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewValue));
            }
        }

        public DeviceBindingViewModel(DeviceBinding deviceBinding, string deviceBindingName = null,
            DeviceBindingCategory? deviceBindingCategory = null)
        {
            DeviceBinding = deviceBinding;
            // Must be set before LoadDeviceInputs() below, since ShowVirtualKeyboardOption reads
            // DeviceBindingCategory -- a caller-supplied object initializer (`new
            // DeviceBindingViewModel(x) { DeviceBindingCategory = ... }`) only assigns it *after* the
            // constructor returns, which is too late for that first LoadDeviceInputs() call.
            if (deviceBindingName != null) DeviceBindingName = deviceBindingName;
            if (deviceBindingCategory.HasValue) DeviceBindingCategory = deviceBindingCategory.Value;

            deviceBinding.Profile.Context.DeviceAliasesChangedEvent += ContextOnDeviceAliasesChanged;
            // Previously false whenever SubscriptionsManager.ProfileActive was true -- but Begin
            // Mapping always activates the profile just to reach this screen at all (see
            // DashboardViewModel.ApplyAssociatedProfileForCurrentScope), so that made this control
            // permanently disabled the entire time it's visible. Rebinding while active is already
            // safe: DeviceBinding.SetKeyTypeValue/ClearBinding already call
            // RefreshSubscriptionsIfActive (session 16), the same way the Patch Bay row's own Listen
            // button -- which was never gated this way -- has always worked.
            BindingEnabled = true;

            LoadDeviceInputs();
        }
        
        public void LoadDeviceInputs()
        {
            var devicesManager = DeviceBinding.Profile.Context.DevicesManager;
            var deviceConfigurationList = DeviceBinding.Profile.GetDeviceConfigurationList(DeviceBinding.DeviceIoType)
                .Select((configuration, index) => new
                {
                    Configuration = configuration,
                    OriginalIndex = index,
                    SortOrder = devicesManager.GetDeviceSortOrder(configuration.Device)
                })
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.OriginalIndex)
                .Select(item => item.Configuration)
                .ToList();

            Devices = new ObservableCollection<ComboBoxItemViewModel>();
            foreach (var deviceConfiguration in deviceConfigurationList)
            {
                Devices.Add(new ComboBoxItemViewModel(
                    deviceConfiguration.GetFullTitleForProfile(DeviceBinding.Profile),
                    deviceConfiguration.Guid,
                    DeviceVisualCatalog.Describe(deviceConfiguration, DeviceBinding.Profile, DeviceBinding.DeviceIoType)));
            }

            if (ShowVirtualKeyboardOption)
            {
                Devices.Add(new ComboBoxItemViewModel("Virtual Keyboard", VirtualKeyboardSentinelGuid,
                    new DeviceVisualDescriptor
                    {
                        Kind = DeviceVisualKind.Keyboard,
                        BadgeText = "VK",
                        AccentBrush = DeviceVisualCatalog.NeutralBrush,
                        OutlineBrush = DeviceVisualCatalog.NeutralBrush,
                        ToolTip = "Send an emulated key press while this input is held, in addition to its normal binding"
                    }));
            }

            SetSelectDevice();
        }

        public void RefreshDeviceList()
        {
            if (_disposed || DeviceBinding == null || DeviceBinding.Profile == null) return;
            LoadDeviceInputs();
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(SelectedDevice));
            OnPropertyChanged(nameof(BindButtonText));
            OnPropertyChanged(nameof(ShowBlock));
            OnPropertyChanged(nameof(ShowInvertInput));
        }

        private void ContextOnDeviceAliasesChanged()
        {
            RefreshDeviceList();
        }

        private void SetSelectDevice()
        {
            ComboBoxItemViewModel selectedDevice = null;

            foreach (var comboBoxItem in Devices)
            {
                if (comboBoxItem.Value == DeviceBinding.DeviceConfigurationGuid)
                {
                    selectedDevice = comboBoxItem;
                    break;
                }
            }

            if (Devices.Count == 0)
            {
                Devices.Add(new ComboBoxItemViewModel("No devices", Guid.Empty));
                selectedDevice = Devices[0];
            }
            else if (selectedDevice == null && DeviceBinding.DeviceConfigurationGuid != Guid.Empty)
            {
                selectedDevice = new ComboBoxItemViewModel("Unavailable device", DeviceBinding.DeviceConfigurationGuid);
                Devices.Insert(0, selectedDevice);
            }
            else if (selectedDevice == null)
            {
                selectedDevice = Devices[0];
            }

            SelectedDevice = selectedDevice;
        }

        public DeviceBindingTransferCompatibility ChangeDeviceConfiguration(Guid selectedDeviceConfigurationGuid)
        {
            var selectedDeviceConfiguration = DeviceBinding.Profile.GetDeviceConfiguration(
                DeviceBinding.DeviceIoType, selectedDeviceConfigurationGuid);
            if (selectedDeviceConfiguration == null) return DeviceBindingTransferCompatibility.Unknown;

            var previousDeviceConfiguration = DeviceBinding.Profile.GetDeviceConfiguration(
                DeviceBinding.DeviceIoType, DeviceBinding.DeviceConfigurationGuid);

            var transfer = DeviceBindingTransferResult.For(
                DeviceBindingTransferCompatibility.Unknown,
                DeviceBinding);

            if (DeviceBinding.IsBound && previousDeviceConfiguration != null &&
                previousDeviceConfiguration.Guid != selectedDeviceConfiguration.Guid)
            {
                transfer = DeviceBindingCompatibility.EvaluateTransfer(
                    previousDeviceConfiguration.Device,
                    selectedDeviceConfiguration.Device,
                    DeviceBinding.Profile.Context,
                    DeviceBinding.DeviceIoType,
                    DeviceBinding,
                    DeviceBindingCategory);
            }

            if (transfer.Compatibility == DeviceBindingTransferCompatibility.Incompatible)
            {
                DeviceBinding.SetDeviceConfigurationGuid(selectedDeviceConfiguration.Guid, false);
            }
            else if (transfer.Compatibility == DeviceBindingTransferCompatibility.Compatible)
            {
                DeviceBinding.SetDeviceConfigurationGuid(
                    selectedDeviceConfiguration.Guid,
                    true,
                    transfer.KeyType,
                    transfer.KeyValue,
                    transfer.KeySubValue);
            }
            else
            {
                // Unknown remains deliberately non-destructive. Preserve the existing semantic key
                // unless the compatibility layer can positively prove that it is incompatible.
                DeviceBinding.SetDeviceConfigurationGuid(selectedDeviceConfiguration.Guid, true);
            }

            SetSelectDevice();
            OnPropertyChanged(nameof(SelectedDevice));
            OnPropertyChanged(nameof(BindButtonText));
            OnPropertyChanged(nameof(ShowBlock));
            OnPropertyChanged(nameof(Block));
            OnPropertyChanged(nameof(ShowInvertInput));
            OnPropertyChanged(nameof(InvertInput));
            Logger.Info("Binding device changed. io=" + DeviceBinding.DeviceIoType +
                        "; category=" + DeviceBindingCategory +
                        "; from=" + (previousDeviceConfiguration?.GetFullTitleForProfile(DeviceBinding.Profile) ?? "unavailable") +
                        "; to=" + selectedDeviceConfiguration.GetFullTitleForProfile(DeviceBinding.Profile) +
                        "; compatibility=" + transfer.Compatibility +
                        "; preserved=" + DeviceBinding.IsBound);
            return transfer.Compatibility;
        }

        public void CurrentValueChanged()
        {
            if (!GuiInvalidated) return;
            GuiInvalidated = false;
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(PreviewValue));
        }

        private void DeviceBindingOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            var deviceBinding = (DeviceBinding) sender;
            if (!deviceBinding.Guid.Equals(DeviceBinding.Guid)) return;

            CurrentValue = deviceBinding.CurrentValue;

            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.IsInBindMode)))
            {
                var bindingManager = deviceBinding.Profile?.Context?.BindingManager;
                if (bindingManager != null)
                {
                    // Only the binding currently waiting for input needs the high-frequency countdown.
                    // Keeping every mapping subscribed caused thousands of needless WPF updates per second.
                    bindingManager.PropertyChanged -= BindingManagerOnPropertyChanged;
                    if (deviceBinding.IsInBindMode) bindingManager.PropertyChanged += BindingManagerOnPropertyChanged;
                }
            }

            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.IsBound))
                || propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.IsInBindMode)))
            {
                BindModeProgress = 0;
                OnPropertyChanged(nameof(BindButtonText));
                OnPropertyChanged(nameof(ShowPreview));
                OnPropertyChanged(nameof(ShowBindMode));
            }

            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.IsBound)))
            {
                SetSelectDevice();
                OnPropertyChanged(nameof(SelectedDevice));
                OnPropertyChanged(nameof(ShowBlock));
                OnPropertyChanged(nameof(Block));
                OnPropertyChanged(nameof(ShowInvertInput));
                OnPropertyChanged(nameof(InvertInput));
            }
            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.DeviceConfigurationGuid)))
            {
                OnPropertyChanged(nameof(BindButtonText));
                OnPropertyChanged(nameof(ShowBlock));
                OnPropertyChanged(nameof(Block));
                OnPropertyChanged(nameof(ShowInvertInput));
                OnPropertyChanged(nameof(InvertInput));
            }
            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.Block)))
            {
                OnPropertyChanged(nameof(Block));
            }
            if (propertyChangedEventArgs.PropertyName.Equals(nameof(DeviceBinding.InvertInput)))
            {
                OnPropertyChanged(nameof(InvertInput));
            }
        }
        
        private void BindingManagerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(BindingManager.BindModeProgress), StringComparison.Ordinal)) return;
            if (!DeviceBinding.IsInBindMode) return;

            var bindingManager = sender as BindingManager;
            if (bindingManager == null) return;
            BindModeProgress = bindingManager.BindModeProgress;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var binding = _deviceBinding;
            if (binding != null)
            {
                binding.PropertyChanged -= DeviceBindingOnPropertyChanged;
                var context = binding.Profile?.Context;
                if (context != null)
                {
                    context.BindingManager.PropertyChanged -= BindingManagerOnPropertyChanged;
                    context.DeviceAliasesChangedEvent -= ContextOnDeviceAliasesChanged;
                }
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
