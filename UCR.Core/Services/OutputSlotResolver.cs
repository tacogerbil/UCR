using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Models.Binding;

namespace HidWizards.UCR.Core.Services
{
    /// <summary>
    /// A single bindable output position on a device (e.g. "Left Stick, X" on a ViGEm Xbox 360 pad),
    /// resolved from that device's own binding menu rather than a hardcoded key table.
    /// </summary>
    public sealed class OutputSlot
    {
        public string Title { get; }
        public int KeyType { get; }
        public int KeyValue { get; }
        public int KeySubValue { get; }
        public DeviceBindingCategory Category { get; }

        public OutputSlot(string title, int keyType, int keyValue, int keySubValue, DeviceBindingCategory category)
        {
            Title = title;
            KeyType = keyType;
            KeyValue = keyValue;
            KeySubValue = keySubValue;
            Category = category;
        }

        /// <summary>
        /// Stable, order-independent identity for this slot within its device's schema
        /// (KeyType/KeyValue/KeySubValue never change position even if menu ordering does).
        /// Persisted as Mapping.TargetOutputKey so a saved Patch Bay row can be re-matched after the
        /// row list is regenerated from a live device tree (on load, or after switching output devices).
        /// </summary>
        public string SlotKey => $"{KeyType}:{KeyValue}:{KeySubValue}";
    }

    /// <summary>
    /// Turns a Device's output binding menu tree into an ordered, flat list of bindable slots — the
    /// thing the Patch Bay renders one row per, instead of a hardcoded Xbox 360 key table. Pure
    /// function over the tree; takes no Device/Context dependency so it's directly unit-testable.
    /// </summary>
    public static class OutputSlotResolver
    {
        public static List<OutputSlot> Resolve(IEnumerable<DeviceBindingNode> outputBindingMenu)
        {
            return DeviceBindingNodeFlattener.Flatten(outputBindingMenu)
                .Select(f => new OutputSlot(f.Title, f.Info.KeyType, f.Info.KeyValue, f.Info.KeySubValue, f.Info.DeviceBindingCategory))
                .ToList();
        }
    }
}
