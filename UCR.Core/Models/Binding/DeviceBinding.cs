using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using HidWizards.IOWrapper.DataTransferObjects;
using HidWizards.UCR.Core.Annotations;
using HidWizards.UCR.Core.Utilities;

namespace HidWizards.UCR.Core.Models.Binding
{
    public enum DeviceBindingCategory
    {
        Event,
        Momentary,
        Range,
        Delta
    }

    public class DeviceBinding : INotifyPropertyChanged
    {
        /* Persistence */
        private bool _isBound;
        [XmlAttribute]
        public bool IsBound
        {
            get => _isBound;
            set
            {
                _isBound = value;
                OnPropertyChanged();
            }
        }
        // Index in its device list
        [XmlAttribute]
        public Guid DeviceConfigurationGuid { get; set; }
        // Subscription key
        [XmlAttribute]
        public int KeyType { get; set; }
        [XmlAttribute]
        public int KeyValue { get; set; }
        [XmlAttribute]
        public int KeySubValue { get; set; }
        [XmlAttribute]
        [DefaultValue(false)]
        public bool Block { get; set; }
        [XmlAttribute]
        [DefaultValue(false)]
        public bool InvertInput { get; set; }
        // Optional, independent of whichever remapper plugin this row uses (Button to Button, Axis to
        // Axis, ...): while this input is held, also send this key via SendInput, in addition to
        // whatever the row's own plugin does with the same press. Lives here rather than on any plugin
        // because it's a property of the physical binding itself, not of a specific remapper -- the
        // Advanced panel's "Virtual Keyboard" device-picker entry sets this instead of a real
        // DeviceConfigurationGuid. 0 = not configured.
        [XmlAttribute]
        [DefaultValue((ushort)0)]
        public ushort AuxiliaryKeyboardKeyCode { get; set; }

        /* Runtime */
        [XmlIgnore]
        public Guid Guid { get; }
        [XmlIgnore]
        public Profile Profile { get; set; }
        [XmlIgnore]
        public DeviceIoType DeviceIoType { get; set; }
        [XmlIgnore]
        public DeviceBindingCategory DeviceBindingCategory { get; set; }

        private bool _isInBindMode = false;
        [XmlIgnore]
        public bool IsInBindMode
        {
            get => _isInBindMode;
            private set
            {
                _isInBindMode = value;
                OnPropertyChanged();
            }
        }


        public delegate void ValueChanged(short value);
        
        private Action<short> _callback;

        [XmlIgnore]
        public Action<short> Callback
        {
            get => InputChanged;
            set
            {
                _callback = value;
                OnPropertyChanged();
            }
        }
        [XmlIgnore]
        public ValueChanged OutputSink { get; set; }

        private short _currentValue;
        [XmlIgnore]
        public short CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                OnPropertyChanged();
            }
        }

        public DeviceBinding()
        {
            Guid = Guid.NewGuid();
        }

        public DeviceBinding(Action<short> callback, Profile profile, DeviceIoType deviceIoType)
        {
            Callback = callback;
            Profile = profile;
            DeviceIoType = deviceIoType;
            Guid = Guid.NewGuid();
            IsBound = false;
        }

        public void SetDeviceConfigurationGuid(Guid deviceConfigurationGuid)
        {
            SetDeviceConfigurationGuid(deviceConfigurationGuid, true);
        }

        public void SetDeviceConfigurationGuid(Guid deviceConfigurationGuid, bool preserveBinding)
        {
            SetDeviceConfigurationGuid(deviceConfigurationGuid, preserveBinding, KeyType, KeyValue, KeySubValue);
        }

        public void SetDeviceConfigurationGuid(Guid deviceConfigurationGuid, bool preserveBinding,
            int keyType, int keyValue, int keySubValue)
        {
            DeviceConfigurationGuid = deviceConfigurationGuid;

            if (!preserveBinding)
            {
                KeyType = 0;
                KeyValue = 0;
                KeySubValue = 0;
                Block = false;
                IsBound = false;
            }
            else
            {
                // Apply any semantic translation before checking whether the new device can block
                // this binding. This avoids testing the destination against a stale source key.
                KeyType = keyType;
                KeyValue = keyValue;
                KeySubValue = keySubValue;

                if (Block && !IsBlockable())
                {
                    Block = false;
                }
            }

            Profile.Context.ContextChanged();
            OnPropertyChanged(nameof(DeviceConfigurationGuid));
            OnPropertyChanged(nameof(IsBound));
        }

        public void SetBlock(bool block)
        {
            if (Block == block) return;
            Block = block;
            Profile.Context.ContextChanged();
            OnPropertyChanged(nameof(Block));
        }

        public void SetInvertInput(bool invert)
        {
            InvertInput = invert;
            Profile.Context.ContextChanged();
            OnPropertyChanged(nameof(InvertInput));
        }

        public void SetKeyTypeValue(int type, int value, int subValue)
        {
            KeyType = type;
            KeyValue = value;
            KeySubValue = subValue;
            IsBound = true;
            Profile.Context.ContextChanged();
            Profile.Context.SubscriptionsManager.RefreshSubscriptionsIfActive(Profile);
        }
        
        public string BoundName()
        {
            return Profile.GetDeviceConfiguration(DeviceIoType, DeviceConfigurationGuid)?.Device.GetBindingName(this) ?? "Device unavailable";
        }

        public bool IsBlockable()
        {
            var device = Profile.GetDeviceConfiguration(DeviceIoType, DeviceConfigurationGuid)?.Device;
            if (device == null) return false;

            var deviceBindingNodes = Profile.Context.DevicesManager.GetDeviceBindingMenu(device, DeviceIoType);
            return IsBlockableInMenu(deviceBindingNodes, KeyType, KeyValue, KeySubValue);
        }

        internal static bool IsBlockableInMenu(List<DeviceBindingNode> deviceBindingNodes,
            int keyType, int keyValue, int keySubValue)
        {
            // Never destructively traverse the provider/cache binding menu. These lists are shared by
            // the UI and device cache; removing nodes here can corrupt later rebind menus.
            var searchList = deviceBindingNodes == null
                ? new List<DeviceBindingNode>()
                : new List<DeviceBindingNode>(deviceBindingNodes);

            while (searchList.Count > 0)
            {
                var node = searchList[0];
                searchList.RemoveAt(0);

                if (node.IsBinding)
                {
                    var info = node.DeviceBindingInfo;
                    if (info.KeyType == keyType && info.KeyValue == keyValue && info.KeySubValue == keySubValue)
                    {
                        return info.Blockable;
                    }
                }

                if (node.ChildrenNodes != null) searchList.AddRange(node.ChildrenNodes);
            }

            return false;
        }

        public static DeviceBindingCategory MapCategory(BindingCategory bindingInfoCategory)
        {
            switch (bindingInfoCategory)
            {
                case BindingCategory.Event:
                    return DeviceBindingCategory.Event;
                case BindingCategory.Momentary:
                    return DeviceBindingCategory.Momentary;
                case BindingCategory.Signed:
                case BindingCategory.Unsigned:
                    return DeviceBindingCategory.Range;
                case BindingCategory.Delta:
                    return DeviceBindingCategory.Delta;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bindingInfoCategory), bindingInfoCategory, null);
            }
        }

        public void WriteOutput(short value)
        {
            CurrentValue = value;
            OutputSink?.Invoke(value);
        }

        public void EnterBindMode()
        {
            if (IsInBindMode) return;

            // Subscribe and expose bind-mode state before asking providers to enter detection mode.
            // Some providers can report synchronously from SetDetectionMode; subscribing afterwards
            // leaves the binding permanently stuck in bind mode if detection completes immediately.
            Profile.Context.BindingManager.EndBindModeHandler += OnEndBindModeHandler;
            IsInBindMode = true;

            try
            {
                Profile.Context.BindingManager.BeginBindMode(this);
            }
            catch
            {
                IsInBindMode = false;
                Profile.Context.BindingManager.EndBindModeHandler -= OnEndBindModeHandler;
                throw;
            }
        }

        public void ClearBinding()
        {
            KeyType = 0;
            KeyValue = 0;
            KeySubValue = 0;
            DeviceConfigurationGuid = Guid.Empty;
            InvertInput = false;
            IsBound = false;
            Profile.Context.ContextChanged();
            Profile.Context.SubscriptionsManager.RefreshSubscriptionsIfActive(Profile);
        }

        private void OnEndBindModeHandler(DeviceBinding deviceBinding)
        {
            if (deviceBinding == null || deviceBinding.Guid != Guid) return;
            IsInBindMode = false;
            Profile.Context.BindingManager.EndBindModeHandler -= OnEndBindModeHandler;
        }

        private void InputChanged(short value)
        {
            if (InvertInput && DeviceIoType == DeviceIoType.Input &&
                DeviceBindingCategory == DeviceBindingCategory.Range)
            {
                value = Functions.Invert(value);
            }

            CurrentValue = value;
            _callback(value);

            SendAuxiliaryKeyboardKeyIfConfigured(value);
        }

        private bool _auxiliaryKeyDown;

        private void SendAuxiliaryKeyboardKeyIfConfigured(short value)
        {
            if (AuxiliaryKeyboardKeyCode == 0) return;
            // Scoped to buttons: a momentary press/release maps cleanly to a key down/up. An axis
            // crossing some arbitrary threshold does not, and isn't what this feature was asked for.
            if (DeviceBindingCategory != DeviceBindingCategory.Momentary) return;

            var down = value != 0;
            if (down == _auxiliaryKeyDown) return;
            _auxiliaryKeyDown = down;
            NativeKeyboardInput.SendKey(AuxiliaryKeyboardKeyCode, down);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
