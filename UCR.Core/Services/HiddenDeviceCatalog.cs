using System;
using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.Core.Services
{
    /// <summary>One row of the "hide from game" picker.</summary>
    public sealed class HiddenDeviceChoice
    {
        public string InstanceId { get; }
        public string DisplayName { get; }
        /// <summary>False for a device saved in the profile that is not plugged in right now.</summary>
        public bool IsConnected { get; }
        public bool IsSelected { get; }

        public HiddenDeviceChoice(string instanceId, string displayName, bool isConnected, bool isSelected)
        {
            InstanceId = instanceId;
            DisplayName = displayName;
            IsConnected = isConnected;
            IsSelected = isSelected;
        }
    }

    /// <summary>Pure: builds the picker rows from detected devices and a profile's saved selections.</summary>
    public static class HiddenDeviceCatalog
    {
        /// <param name="connectedDevices">Currently detected physical input devices.</param>
        /// <param name="titleOf">Display-name lookup (alias-aware) for a device.</param>
        /// <param name="saved">The profile's current HiddenDevices.</param>
        /// <returns>
        /// One row per distinct Windows instance ID: every connected device with a resolvable instance
        /// ID that is not a protected virtual device, plus any saved selection that is not connected.
        /// </returns>
        public static List<HiddenDeviceChoice> Build(IEnumerable<Device> connectedDevices,
            Func<Device, string> titleOf, IEnumerable<HiddenDevice> saved)
        {
            var savedIds = new HashSet<string>(
                (saved ?? Enumerable.Empty<HiddenDevice>()).Select(d => d.InstanceId).Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);
            var rows = new Dictionary<string, HiddenDeviceChoice>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in connectedDevices ?? Enumerable.Empty<Device>())
            {
                string instanceId;
                if (!DevicesManager.TryGetWindowsDeviceInstanceId(device, out instanceId)) continue;
                // The device handle carries the VID/PID even when the HID path does not (vJoy).
                if (HidHidePlanner.IsProtected(instanceId) || HidHidePlanner.IsProtected(device.DeviceHandle) || rows.ContainsKey(instanceId)) continue;
                rows[instanceId] = new HiddenDeviceChoice(instanceId, titleOf(device) ?? instanceId, true, savedIds.Contains(instanceId));
            }

            foreach (var entry in (saved ?? Enumerable.Empty<HiddenDevice>()))
            {
                if (string.IsNullOrWhiteSpace(entry.InstanceId) || HidHidePlanner.IsProtected(entry.InstanceId) || rows.ContainsKey(entry.InstanceId)) continue;
                rows[entry.InstanceId] = new HiddenDeviceChoice(entry.InstanceId, entry.DisplayName ?? entry.InstanceId, false, true);
            }

            return rows.Values.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
