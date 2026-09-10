using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core;
using System.Linq;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class MappingRowViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;
        private readonly Core.Models.Mapping _mapping;
        
        public string Title { get; }
        public string TargetOutputKey { get; }
        public bool IsAxis { get; }

        public MappingRowViewModel(Context context, Core.Models.Mapping mapping, string title, string targetOutputKey, bool isAxis)
        {
            _context = context;
            _mapping = mapping;
            Title = title;
            TargetOutputKey = targetOutputKey;
            IsAxis = isAxis;
            
            ListenCommand = new RelayCommand(ExecuteListen);
            ClearCommand = new RelayCommand(ExecuteClear);
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
        
        public ICommand ListenCommand { get; }
        public ICommand ClearCommand { get; }
        
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
