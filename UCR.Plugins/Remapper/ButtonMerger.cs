using System;
using System.Reflection;
using HidWizards.UCR.Core.Attributes;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;

namespace HidWizards.UCR.Plugins.Remapper
{
    [Plugin("Button Merger", Group = "Button", Description = "Merge two buttons into one")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button 1")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button 2")]
    [PluginOutput(DeviceBindingCategory.Momentary, "Button")]
    public class ButtonMerger : Plugin
    {
        public override void Update(params short[] values)
        {
            var value = (short)(values[0] != 0 || values[1] != 0 ? 1 : 0);
            WriteOutput(0, value);
        }
    }
}
