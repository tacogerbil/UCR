using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HidWizards.UCR.Core.Services;

namespace HidWizards.UCR.ViewModels.Dialogs
{
    /// <summary>Checkbox row for one device in a profile's "Hide from game" list.</summary>
    public class HiddenDeviceChoiceViewModel : INotifyPropertyChanged
    {
        private readonly Action _onChanged;
        private bool _isSelected;

        public string InstanceId { get; }
        public string DisplayName { get; }
        public bool IsConnected { get; }

        public string Label => IsConnected ? DisplayName : DisplayName + " (not connected)";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }

        public HiddenDeviceChoiceViewModel(HiddenDeviceChoice choice, Action onChanged)
        {
            InstanceId = choice.InstanceId;
            DisplayName = choice.DisplayName;
            IsConnected = choice.IsConnected;
            _isSelected = choice.IsSelected;
            _onChanged = onChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
