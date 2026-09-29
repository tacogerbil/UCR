using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Mapping
{
    /// <summary>
    /// A "Button to Keyboard Key" mapping. Same shape as FilterProducerRowViewModel: doesn't target
    /// any output slot, its only job is sending a synthetic keystroke while its bound button is held.
    /// Reuses PluginSummaryViewModel for the input binding (Listen/manual-picker via the same
    /// DeviceBindingControl every other row uses) -- only the key itself needs bespoke UI
    /// (KeyCaptureControl), since a raw virtual-key code has no sensible generic property editor.
    /// </summary>
    public class KeyboardShortcutRowViewModel : IDisposable, INotifyPropertyChanged
    {
        private readonly Core.Models.Mapping _mapping;
        private readonly Plugin _plugin;

        public PluginSummaryViewModel PluginSummary { get; }
        public ICommand RemoveCommand { get; }
        public Action<KeyboardShortcutRowViewModel> Remove { get; set; }

        // Read/written via Plugin.GetKeyboardKeyCode()/SetKeyboardKeyCode() (reflection-based, same
        // pattern as GetDefinedFilterName()) rather than a direct ButtonToKeyboardKey reference -- UCR
        // has no compile-time reference to UCR.Plugins.
        public ushort KeyCode
        {
            get => _plugin.GetKeyboardKeyCode();
            set
            {
                if (_plugin.GetKeyboardKeyCode() == value) return;
                _plugin.SetKeyboardKeyCode(value);
                OnPropertyChanged();
            }
        }

        public KeyboardShortcutRowViewModel(Core.Models.Mapping mapping)
        {
            _mapping = mapping;
            _plugin = mapping.Plugins[0];
            PluginSummary = new PluginSummaryViewModel(_plugin, mapping.DeviceBindings);
            RemoveCommand = new RelayCommand(_ => Remove?.Invoke(this));
        }

        internal Core.Models.Mapping Mapping => _mapping;

        public void Dispose()
        {
            PluginSummary.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
