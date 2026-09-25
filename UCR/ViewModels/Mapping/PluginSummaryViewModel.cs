using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dialogs;
using HidWizards.UCR.ViewModels.ProfileViewModels;
using HidWizards.UCR.Views.Dialogs;
using MaterialDesignThemes.Wpf;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class PluginSummaryViewModel : IDisposable
    {
        public Plugin Plugin { get; }

        public ObservableCollection<DeviceBindingViewModel> DeviceBindings { get; }
        public ObservableCollection<PluginPropertyGroupViewModel> PluginPropertyGroups { get; }

        // "Only active when filter X is [Active/Inactive]" conditions on this plugin (Plugin.Filters).
        // A plugin runs only when every tag here currently passes -- see Plugin.IsFiltered.
        public ObservableCollection<FilterTagViewModel> FilterTags { get; }
        public ICommand AddFilterCommand { get; }

        // deviceBindings is the owning Mapping's DeviceBindings -- one entry per Plugin.InputCategories
        // entry, in the same order (see Mapping.AddPlugin, which builds DeviceBindings by iterating
        // plugin.InputCategories). This panel exists to let the user see/assign each *physical input*
        // a plugin needs (e.g. a merger's "Button 1"/"Button 2"), not the plugin's Output binding --
        // that's always exactly one entry, already fixed to this row's own output slot by
        // MappingRowViewModel.ResolveOutputBinding, and showing it here as an editable picker only
        // invites accidentally repointing a binding that's meant to be locked to this row.
        public PluginSummaryViewModel(Plugin plugin, List<DeviceBinding> deviceBindings)
        {
            Plugin = plugin;
            DeviceBindings = new ObservableCollection<DeviceBindingViewModel>();
            PluginPropertyGroups = new ObservableCollection<PluginPropertyGroupViewModel>();
            FilterTags = new ObservableCollection<FilterTagViewModel>();
            AddFilterCommand = new RelayCommand(async _ => await ExecuteAddFilter());

            PopulateDeviceBindingsViewModels(deviceBindings);
            PopulatePluginProperties();
            PopulateFilterTags();
        }

        private void PopulateFilterTags()
        {
            foreach (var filter in Plugin.Filters)
            {
                FilterTags.Add(new FilterTagViewModel(Plugin, filter, RemoveFilterTag));
            }
        }

        private void RemoveFilterTag(FilterTagViewModel tag)
        {
            FilterTags.Remove(tag);
        }

        private async Task ExecuteAddFilter()
        {
            var profile = Plugin.Profile;
            if (profile == null) return;

            var alreadyApplied = FilterTags.Select(t => t.Name).ToList();
            var available = profile.GetFilters()
                .Where(name => !alreadyApplied.Contains(name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (available.Count == 0)
            {
                HidWizards.UCR.Utilities.DarkMessageBox.Show(
                    "This profile does not currently define any filters. Create one with a Button to Filter or Axis to Filter mapping first.",
                    "No filters defined", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var dialog = new FilterSelectDialog(available);
            var result = await DialogHost.Show(dialog, "RootDialog") as FilterSelectDialogViewModel;
            if (result == null || string.IsNullOrWhiteSpace(result.SelectedFilter)) return;

            var filter = Plugin.AddFilter(result.SelectedFilter.Trim());
            FilterTags.Add(new FilterTagViewModel(Plugin, filter, RemoveFilterTag));
        }

        private void PopulateDeviceBindingsViewModels(List<DeviceBinding> deviceBindings)
        {
            for (var i = 0; i < Plugin.InputCategories.Count && i < deviceBindings.Count; i++)
            {
                DeviceBindings.Add(new DeviceBindingViewModel(deviceBindings[i])
                {
                    DeviceBindingName = Plugin.InputCategories[i].Name,
                    DeviceBindingCategory = Plugin.InputCategories[i].Category,
                    PluginPropertyGroup = GetPluginPropertyGroupForOutput(Plugin.InputCategories[i].GroupName)
                });
            }
        }

        private PluginPropertyGroupViewModel GetPluginPropertyGroupForOutput(string groupName)
        {
            if (groupName == null) return null;
            var matrixGroup = Plugin.GetGuiMatrix().Find(p => p.GroupName.Equals(groupName));
            return matrixGroup != null ? new PluginPropertyGroupViewModel(matrixGroup) : null;
        }

        private void PopulatePluginProperties()
        {
            foreach (var pluginPropertyGroup in Plugin.PluginPropertyGroups)
            {
                if (pluginPropertyGroup.PluginProperties.Count == 0 || pluginPropertyGroup.GroupType.Equals(PluginPropertyGroup.GroupTypes.Output)) continue;

                PluginPropertyGroups.Add(new PluginPropertyGroupViewModel(pluginPropertyGroup));
            }
        }

        public void Dispose()
        {
            foreach (var binding in DeviceBindings)
            {
                binding.Dispose();
            }
        }
    }
}
