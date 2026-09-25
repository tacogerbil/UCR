using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Mapping
{
    /// <summary>
    /// A "Button to Filter"/"Axis to Filter" mapping. Unlike a Patch Bay row, this doesn't target any
    /// output slot -- its only job is setting a named filter's state, which other rows can then
    /// condition on via PluginSummaryViewModel.FilterTags. Reuses PluginSummaryViewModel entirely for
    /// its input binding (Listen/manual-picker, via the same DeviceBindingControl) and its Filter
    /// name/state GUI properties (FilterName/FilterStateDown/FilterStateUp render generically through
    /// the existing PluginPropertyListControl -- no bespoke UI needed for those).
    /// </summary>
    public class FilterProducerRowViewModel : IDisposable
    {
        private readonly Core.Models.Mapping _mapping;
        private readonly Plugin _plugin;

        public PluginSummaryViewModel PluginSummary { get; }
        public ICommand RemoveCommand { get; }
        public Action<FilterProducerRowViewModel> Remove { get; set; }

        public FilterProducerRowViewModel(Core.Models.Mapping mapping)
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
    }
}
