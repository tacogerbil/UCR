using System;
using System.Collections.ObjectModel;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.ViewModels.ProfileViewModels;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class PluginSummaryViewModel : IDisposable
    {
        public Plugin Plugin { get; }

        public ObservableCollection<DeviceBindingViewModel> DeviceBindings { get; }
        public ObservableCollection<PluginPropertyGroupViewModel> PluginPropertyGroups { get; }

        public PluginSummaryViewModel(Plugin plugin)
        {
            Plugin = plugin;
            DeviceBindings = new ObservableCollection<DeviceBindingViewModel>();
            PluginPropertyGroups = new ObservableCollection<PluginPropertyGroupViewModel>();

            PopulateDeviceBindingsViewModels();
            PopulatePluginProperties();
        }

        private void PopulateDeviceBindingsViewModels()
        {
            for (var i = 0; i < Plugin.OutputCategories.Count; i++)
            {
                DeviceBindings.Add(new DeviceBindingViewModel(Plugin.Outputs[i])
                {
                    DeviceBindingName = Plugin.OutputCategories[i].Name,
                    DeviceBindingCategory = Plugin.OutputCategories[i].Category,
                    PluginPropertyGroup = GetPluginPropertyGroupForOutput(Plugin.OutputCategories[i].GroupName)
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
