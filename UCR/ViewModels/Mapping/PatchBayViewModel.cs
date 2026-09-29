using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Services;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.ViewModels.Mapping
{
    // Named PatchBayViewModel (not MappingViewModel) to avoid colliding with the unrelated
    // HidWizards.UCR.ViewModels.ProfileViewModels.MappingViewModel used by the legacy nested-tab UI.
    public class PatchBayViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;
        private Profile _currentProfile;

        public ObservableCollection<InputScopeItem> FullCatalog { get; set; }

        private InputScopeItem _currentScope;
        public InputScopeItem CurrentScope
        {
            get => _currentScope;
            set
            {
                if (_currentScope == value) return;
                _currentScope = value;
                OnPropertyChanged();

                foreach (var row in Rows)
                {
                    row.CurrentScope = value;
                }
            }
        }

        // The Output Device the toolbar's "Output Device" dropdown currently has selected
        // (Dashboard.OutputDeviceControlViewModel.SelectedDeviceConfiguration). Rows are generated
        // from THIS device's own output binding tree, not a hardcoded key table, so changing it
        // regenerates the whole row list.
        private DeviceConfiguration _selectedOutputDeviceConfiguration;
        public DeviceConfiguration SelectedOutputDeviceConfiguration
        {
            get => _selectedOutputDeviceConfiguration;
            set
            {
                if (_selectedOutputDeviceConfiguration == value) return;
                _selectedOutputDeviceConfiguration = value;
                PopulateRows();
            }
        }

        public ObservableCollection<MappingRowViewModel> Rows { get; } = new ObservableCollection<MappingRowViewModel>();

        // "Button to Filter"/"Axis to Filter" mappings -- distinguished from a Patch Bay row by having
        // no TargetOutputKey at all (Patch Bay rows always set one; see AddRow). Listed separately
        // because they don't target an output slot, so they can never be one of Rows.
        public ObservableCollection<FilterProducerRowViewModel> FilterProducers { get; } = new ObservableCollection<FilterProducerRowViewModel>();
        public ICommand AddButtonFilterCommand { get; }
        public ICommand AddAxisFilterCommand { get; }

        // "Button to Keyboard Key" mappings -- same shape as FilterProducers: no TargetOutputKey,
        // since sending a keystroke doesn't target any output-device slot.
        public ObservableCollection<KeyboardShortcutRowViewModel> KeyboardShortcuts { get; } = new ObservableCollection<KeyboardShortcutRowViewModel>();
        public ICommand AddKeyboardShortcutCommand { get; }

        // Plugin template lookup by name, defaulting to the real MEF-discovered catalog in production.
        // Exposed as an injectable seam (internal ctor overload below) for the same reason
        // MappingRowViewModel needs one: PluginManager.Plugins is only ever populated when a "Plugins"
        // folder sits next to the running executable, which the test runner's output never has.
        private readonly Func<string, Plugin> _resolvePluginTemplate;

        public PatchBayViewModel(Context context)
            : this(context, pluginName => context.PluginManager.Plugins.FirstOrDefault(p => p.PluginName == pluginName))
        {
        }

        internal PatchBayViewModel(Context context, Func<string, Plugin> resolvePluginTemplate)
        {
            _context = context;
            _resolvePluginTemplate = resolvePluginTemplate;
            AddButtonFilterCommand = new RelayCommand(_ => AddFilterProducer("Button to Filter"));
            AddAxisFilterCommand = new RelayCommand(_ => AddFilterProducer("Axis to Filter"));
            AddKeyboardShortcutCommand = new RelayCommand(_ => AddKeyboardShortcut());
        }

        public void SetProfile(Profile profile)
        {
            _currentProfile = profile;
            PopulateRows();
            PopulateFilterProducers();
            PopulateKeyboardShortcuts();
        }

        private void PopulateFilterProducers()
        {
            foreach (var row in FilterProducers) row.Dispose();
            FilterProducers.Clear();
            if (_currentProfile == null) return;

            foreach (var mapping in _currentProfile.Mappings)
            {
                if (!string.IsNullOrEmpty(mapping.TargetOutputKey)) continue;
                if (mapping.Plugins.Count == 0 || mapping.Plugins[0].GetDefinedFilterName() == null) continue;

                AddFilterProducerRow(mapping);
            }
        }

        private void AddFilterProducerRow(Core.Models.Mapping mapping)
        {
            var row = new FilterProducerRowViewModel(mapping) { Remove = RemoveFilterProducer };
            FilterProducers.Add(row);
        }

        private void AddFilterProducer(string pluginName)
        {
            if (_currentProfile == null) return;
            var templatePlugin = _resolvePluginTemplate(pluginName);
            if (templatePlugin == null) return;

            var mapping = new Core.Models.Mapping(_currentProfile, pluginName);
            _currentProfile.Mappings.Add(mapping);

            var newPlugin = _context.PluginManager.GetNewPlugin(templatePlugin);
            mapping.AddPlugin(newPlugin);

            AddFilterProducerRow(mapping);
        }

        private void RemoveFilterProducer(FilterProducerRowViewModel row)
        {
            _currentProfile?.RemoveMapping(row.Mapping);
            row.Dispose();
            FilterProducers.Remove(row);
        }

        private void PopulateKeyboardShortcuts()
        {
            foreach (var row in KeyboardShortcuts) row.Dispose();
            KeyboardShortcuts.Clear();
            if (_currentProfile == null) return;

            foreach (var mapping in _currentProfile.Mappings)
            {
                if (!string.IsNullOrEmpty(mapping.TargetOutputKey)) continue;
                if (mapping.Plugins.Count == 0 || !mapping.Plugins[0].IsKeyboardKeyProducer) continue;

                AddKeyboardShortcutRow(mapping);
            }
        }

        private void AddKeyboardShortcutRow(Core.Models.Mapping mapping)
        {
            var row = new KeyboardShortcutRowViewModel(mapping) { Remove = RemoveKeyboardShortcut };
            KeyboardShortcuts.Add(row);
        }

        private void AddKeyboardShortcut()
        {
            if (_currentProfile == null) return;
            var templatePlugin = _resolvePluginTemplate("Button to Keyboard Key");
            if (templatePlugin == null) return;

            var mapping = new Core.Models.Mapping(_currentProfile, "Button to Keyboard Key");
            _currentProfile.Mappings.Add(mapping);

            var newPlugin = _context.PluginManager.GetNewPlugin(templatePlugin);
            mapping.AddPlugin(newPlugin);

            AddKeyboardShortcutRow(mapping);
        }

        private void RemoveKeyboardShortcut(KeyboardShortcutRowViewModel row)
        {
            _currentProfile?.RemoveMapping(row.Mapping);
            row.Dispose();
            KeyboardShortcuts.Remove(row);
        }

        // Keeps SelectedOutputDeviceConfiguration in sync with the toolbar's Output Device dropdown
        // (Dashboard.OutputDeviceControlViewModel.SelectedDeviceConfiguration) for the lifetime of
        // this ViewModel. Pulled into PatchBayViewModel rather than left as inline subscription
        // management in MainWindow's code-behind, which is already over MCCC's file-size guidance.
        private DashboardViewModel _dashboard;
        private ProfileDeviceListControlViewModel _observedOutputDeviceControl;

        // Exposed so the Mapping tab itself can offer a scope selector (Dashboard.InputSources /
        // SelectedInputScope) without needing a second, parallel wiring mechanism — setting
        // Dashboard.SelectedInputScope here flows back through the same MainWindow property-changed
        // subscription that already keeps CurrentScope in sync.
        public DashboardViewModel Dashboard => _dashboard;

        public void AttachToDashboard(DashboardViewModel dashboard)
        {
            _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _dashboard.PropertyChanged += DashboardOnPropertyChanged;
            ObserveOutputDeviceControl(_dashboard.OutputDeviceControlViewModel);
        }

        private void DashboardOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DashboardViewModel.OutputDeviceControlViewModel))
            {
                ObserveOutputDeviceControl(_dashboard.OutputDeviceControlViewModel);
            }
        }

        private void ObserveOutputDeviceControl(ProfileDeviceListControlViewModel outputDeviceControl)
        {
            if (_observedOutputDeviceControl != null)
            {
                _observedOutputDeviceControl.PropertyChanged -= OutputDeviceControlOnPropertyChanged;
            }

            _observedOutputDeviceControl = outputDeviceControl;
            SelectedOutputDeviceConfiguration = outputDeviceControl?.SelectedDeviceConfiguration?.DeviceConfiguration;

            if (_observedOutputDeviceControl != null)
            {
                _observedOutputDeviceControl.PropertyChanged += OutputDeviceControlOnPropertyChanged;
            }
        }

        private void OutputDeviceControlOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileDeviceListControlViewModel.SelectedDeviceConfiguration))
            {
                SelectedOutputDeviceConfiguration = _observedOutputDeviceControl?.SelectedDeviceConfiguration?.DeviceConfiguration;
            }
        }

        private void PopulateRows()
        {
            Rows.Clear();
            if (_currentProfile == null) return;

            var outputDevice = SelectedOutputDeviceConfiguration?.Device;
            if (outputDevice == null) return;

            var outputMenu = outputDevice.GetDeviceBindingMenu(_context, DeviceIoType.Output);
            var slots = OutputSlotResolver.Resolve(outputMenu);

            foreach (var slot in slots)
            {
                AddRow(slot);
            }
        }

        // Mappings are matched (and, for a first-time slot, created) by OutputSlot.SlotKey — the
        // slot's own KeyType/KeyValue/KeySubValue — rather than a display title. A Mapping for a
        // slot that doesn't exist on the currently selected Output Device (e.g. the user switched
        // from an Xbox 360 pad to a DS4, or to a device with fewer axes) is deliberately left alone
        // in Profile.Mappings rather than deleted: it simply won't render a row until a device with
        // a matching slot is selected again. This mirrors the "never destructively discard a binding"
        // convention DeviceBindingCompatibility already uses elsewhere in this codebase.
        private void AddRow(OutputSlot slot)
        {
            var mapping = _currentProfile.Mappings.FirstOrDefault(m => m.TargetOutputKey == slot.SlotKey);
            if (mapping == null)
            {
                mapping = new Core.Models.Mapping(_currentProfile, slot.Title)
                {
                    TargetOutputKey = slot.SlotKey
                };
                _currentProfile.Mappings.Add(mapping);
            }

            var rowVm = new MappingRowViewModel(_context, mapping, slot, SelectedOutputDeviceConfiguration.Guid)
            {
                CurrentScope = CurrentScope,
                FullCatalog = FullCatalog
            };
            Rows.Add(rowVm);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
