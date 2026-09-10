using System;
using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Managers;

namespace HidWizards.UCR.Core.Services
{
    public class DeviceAliasService
    {
        private readonly Context _context;

        public DeviceAliasService(Context context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string GetDisplayTitle(Device device)
        {
            if (device == null) return string.Empty;
            var alias = FindAlias(device);
            return alias == null || string.IsNullOrWhiteSpace(alias.Alias) ? device.Title : alias.Alias;
        }

        public string GetDeviceAlias(Device device)
        {
            return FindAlias(device)?.Alias;
        }

        public bool GetDeviceHidden(Device device)
        {
            return FindAlias(device)?.Hidden ?? false;
        }

        public int GetDeviceSortOrder(Device device)
        {
            return FindAlias(device)?.SortOrder ?? int.MaxValue;
        }

        public DeviceOutlineColor GetDeviceOutlineColor(Device device)
        {
            return FindAlias(device)?.OutlineColor ?? DeviceOutlineColor.Default;
        }

        public bool CanPersistDeviceAlias(Device device, IEnumerable<Device> liveDevices)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.ProviderName)) return false;
            if (string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrWhiteSpace(BuildLogicalDeviceKey(device));
            if (!string.IsNullOrWhiteSpace(device.HidPath)) return true;
            if (UsesLogicalSlotIdentity(device.ProviderName)) return !string.IsNullOrWhiteSpace(device.DeviceHandle);
            if (string.IsNullOrWhiteSpace(device.DeviceHandle)) return false;
            return CountHandleMatches(device, liveDevices) == 1;
        }

        public bool TrySetDeviceAlias(Device device, IEnumerable<Device> liveDevices, string alias, out string error)
        {
            error = null;
            if (device == null)
            {
                error = "No device is selected.";
                return false;
            }

            if (_context.DeviceAliases == null) _context.DeviceAliases = new List<DeviceAlias>();

            var identity = BuildAliasIdentity(device);
            if (identity == null)
            {
                error = "This device does not expose enough identity information for a persistent alias.";
                return false;
            }

            var normalizedAlias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
            var existing = _context.DeviceAliases.FirstOrDefault(candidate => AliasIdentityEquals(candidate, identity));

            if (normalizedAlias == null)
            {
                if (existing == null)
                {
                    device.Alias = null;
                    return true;
                }

                if (existing.Alias == null)
                {
                    device.Alias = null;
                    return true;
                }

                existing.Alias = null;
                device.Alias = null;
                if (!existing.HasPresentationSettings) _context.DeviceAliases.Remove(existing);
                _context.ContextChanged();
                _context.OnDeviceAliasesChangedEvent();
                return true;
            }

            if (!CanPersistDeviceAlias(device, liveDevices))
            {
                error = "UCR cannot safely persist an individual alias for this device because the provider does not expose a unique identity for it while identical devices are present.";
                return false;
            }

            if (existing == null)
            {
                existing = identity;
                _context.DeviceAliases.Add(existing);
            }

            if (string.Equals(existing.Alias, normalizedAlias, StringComparison.Ordinal))
            {
                device.Alias = normalizedAlias;
                return true;
            }

            existing.Alias = normalizedAlias;
            device.Alias = normalizedAlias;
            _context.ContextChanged();
            _context.OnDeviceAliasesChangedEvent();
            return true;
        }

        public bool TrySetDevicePresentation(Device device, IEnumerable<Device> liveDevices, string alias, bool hidden,
            int sortOrder, DeviceOutlineColor outlineColor, out string error)
        {
            error = null;
            if (device == null)
            {
                error = "No device is selected.";
                return false;
            }

            if (_context.DeviceAliases == null) _context.DeviceAliases = new List<DeviceAlias>();

            var identity = BuildAliasIdentity(device);
            if (identity == null)
            {
                error = "This device does not expose enough identity information for persistent device settings.";
                return false;
            }

            var existing = _context.DeviceAliases.FirstOrDefault(candidate => AliasIdentityEquals(candidate, identity));
            var normalizedAlias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
            var normalizedSortOrder = sortOrder < 0 ? int.MaxValue : sortOrder;
            
            var wantsPersistentSettings = normalizedAlias != null || hidden || normalizedSortOrder != int.MaxValue ||
                                          outlineColor != DeviceOutlineColor.Default;

            if (wantsPersistentSettings && !CanPersistDeviceAlias(device, liveDevices))
            {
                error = "UCR cannot safely persist individual settings for this device because the provider does not expose a unique identity for it while identical devices are present.";
                return false;
            }

            if (!wantsPersistentSettings)
            {
                if (existing != null)
                {
                    _context.DeviceAliases.Remove(existing);
                    _context.ContextChanged();
                    _context.OnDeviceAliasesChangedEvent();
                }
                device.Alias = null;
                return true;
            }

            if (existing == null)
            {
                existing = identity;
                _context.DeviceAliases.Add(existing);
            }
            else if (string.Equals(existing.Alias, normalizedAlias, StringComparison.Ordinal) &&
                     existing.Hidden == hidden && existing.SortOrder == normalizedSortOrder &&
                     existing.OutlineColor == outlineColor &&
                     string.IsNullOrWhiteSpace(existing.DefaultOutlineColor))
            {
                device.Alias = normalizedAlias;
                return true;
            }

            existing.Alias = normalizedAlias;
            existing.Hidden = hidden;
            existing.SortOrder = normalizedSortOrder;
            existing.OutlineColor = outlineColor;
            existing.DefaultOutlineColor = null;
            device.Alias = normalizedAlias;
            _context.ContextChanged();
            _context.OnDeviceAliasesChangedEvent();
            return true;
        }

        public void MergeDeviceAliases(IEnumerable<DeviceAlias> aliases, bool overwriteExisting)
        {
            if (aliases == null) return;
            if (_context.DeviceAliases == null) _context.DeviceAliases = new List<DeviceAlias>();
            var changed = false;

            foreach (var imported in aliases.Where(alias => alias != null))
            {
                var existing = _context.DeviceAliases.FirstOrDefault(candidate => AliasIdentityEquals(candidate, imported));
                if (existing == null)
                {
                    var clone = imported.Clone();
                    clone.DefaultOutlineColor = null;
                    _context.DeviceAliases.Add(clone);
                    changed = true;
                }
                else if (overwriteExisting &&
                         (!string.Equals(existing.Alias, imported.Alias, StringComparison.Ordinal) ||
                          existing.Hidden != imported.Hidden ||
                          existing.Removed != imported.Removed ||
                          existing.SortOrder != imported.SortOrder ||
                          existing.OutlineColor != imported.OutlineColor ||
                          !string.IsNullOrWhiteSpace(existing.DefaultOutlineColor)))
                {
                    existing.Alias = imported.Alias;
                    existing.Hidden = imported.Hidden;
                    existing.Removed = imported.Removed;
                    existing.SortOrder = imported.SortOrder;
                    existing.OutlineColor = imported.OutlineColor;
                    existing.DefaultOutlineColor = null;
                    changed = true;
                }
            }

            if (changed) _context.OnDeviceAliasesChangedEvent();
        }

        public void ReplaceDeviceAliases(IEnumerable<DeviceAlias> aliases)
        {
            _context.DeviceAliases = aliases == null
                ? new List<DeviceAlias>()
                : aliases.Where(alias => alias != null).Select(alias =>
                {
                    var clone = alias.Clone();
                    clone.DefaultOutlineColor = null;
                    return clone;
                }).ToList();
            _context.OnDeviceAliasesChangedEvent();
        }

        public static bool AliasIdentityEquals(DeviceAlias left, DeviceAlias right)
        {
            return DeviceIdentity.AliasEquals(left, right);
        }

        public static DeviceAlias BuildAliasIdentity(Device device)
        {
            return DeviceIdentity.BuildAliasIdentity(device);
        }

        private DeviceAlias FindAlias(Device device)
        {
            if (device == null || _context.DeviceAliases == null) return null;
            var identity = BuildAliasIdentity(device);
            if (identity == null) return null;

            var exact = _context.DeviceAliases.FirstOrDefault(alias => AliasIdentityEquals(alias, identity));
            if (exact != null) return exact;

            if (string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(device.DeviceHandle) && device.LogicalInstanceNumber <= 1)
            {
                var legacy = _context.DeviceAliases.FirstOrDefault(alias => alias != null &&
                    alias.IdentityKind == DeviceAliasIdentityKind.HardwareHandle && alias.DeviceNumber == 0 &&
                    string.Equals(alias.ProviderName, device.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(alias.IdentityValue, device.DeviceHandle, StringComparison.OrdinalIgnoreCase));
                if (legacy != null)
                {
                    legacy.IdentityValue = identity.IdentityValue;
                    _context.ContextChanged();
                    return legacy;
                }
            }

            return null;
        }

        public bool IsInputRemoved(Device device)
        {
            return FindAlias(device)?.Removed ?? false;
        }

        public bool RemoveInputDevice(Device device)
        {
            if (device == null) return false;
            if (_context.DeviceAliases == null) _context.DeviceAliases = new List<DeviceAlias>();
            var identity = BuildAliasIdentity(device);
            if (identity == null) return false;

            var existing = _context.DeviceAliases.FirstOrDefault(candidate => AliasIdentityEquals(candidate, identity));
            if (existing == null)
            {
                existing = identity;
                _context.DeviceAliases.Add(existing);
            }
            if (existing.Removed) return true;

            existing.Removed = true;
            existing.Hidden = false;
            _context.ContextChanged();
            _context.OnDeviceAliasesChangedEvent();
            return true;
        }

        public bool RestoreInputDevice(Device device)
        {
            var existing = FindAlias(device);
            if (existing == null || !existing.Removed) return false;

            existing.Removed = false;
            if (!existing.HasPresentationSettings) _context.DeviceAliases.Remove(existing);
            _context.ContextChanged();
            _context.OnDeviceAliasesChangedEvent();
            return true;
        }

        public void ApplyAliases(List<Device> devices)
        {
            if (devices == null) return;
            foreach (var device in devices)
            {
                device.Alias = null;
                var alias = FindAlias(device);
                if (alias == null || string.IsNullOrWhiteSpace(alias.Alias)) continue;

                if (alias.IdentityKind == DeviceAliasIdentityKind.HardwareHandle &&
                    !string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase) &&
                    CountHandleMatches(device, GetRelevantIdentityPopulation(device, devices)) != 1)
                {
                    continue;
                }

                device.Alias = alias.Alias;
            }
        }

        public bool IsDeviceHidden(Device device, IEnumerable<Device> population)
        {
            var preference = FindAlias(device);
            if (preference == null || !preference.Hidden) return false;

            var devices = population == null ? new List<Device>() : population.ToList();
            if (preference.IdentityKind == DeviceAliasIdentityKind.HardwareHandle &&
                !string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase) &&
                CountHandleMatches(device, GetRelevantIdentityPopulation(device, devices)) != 1)
            {
                return false;
            }

            return true;
        }

        public List<Device> SortDevices(List<Device> devices)
        {
            return devices
                .Select((device, index) =>
                {
                    var preference = FindAlias(device);
                    if (preference != null && preference.IdentityKind == DeviceAliasIdentityKind.HardwareHandle &&
                        !string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase) &&
                        CountHandleMatches(device, GetRelevantIdentityPopulation(device, devices)) != 1)
                    {
                        preference = null;
                    }

                    return new
                    {
                        Device = device,
                        OriginalIndex = index,
                        Preference = preference
                    };
                })
                .OrderBy(item => item.Preference?.SortOrder ?? int.MaxValue)
                .ThenBy(item => item.OriginalIndex)
                .Select(item => item.Device)
                .ToList();
        }

        private static List<Device> GetRelevantIdentityPopulation(Device device, List<Device> devices)
        {
            var liveMatches = devices.Where(candidate => candidate != null && !candidate.IsCache &&
                string.Equals(candidate.ProviderName, device.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.DeviceHandle, device.DeviceHandle, StringComparison.OrdinalIgnoreCase)).ToList();

            return liveMatches.Count > 0 ? liveMatches : devices;
        }

        private static int CountHandleMatches(Device device, IEnumerable<Device> devices)
        {
            if (device == null || devices == null) return 0;
            return devices.Count(candidate => candidate != null &&
                string.Equals(candidate.ProviderName, device.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.DeviceHandle, device.DeviceHandle, StringComparison.OrdinalIgnoreCase));
        }

        private static bool UsesLogicalSlotIdentity(string providerName)
        {
            return string.Equals(providerName, "Core_vJoyInterfaceWrap", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(providerName, "Core_ViGEm", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(providerName, "Core_TitanOne", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildLogicalDeviceKey(Device device)
        {
            if (device == null || string.IsNullOrWhiteSpace(device.DeviceHandle)) return string.Empty;
            var start = device.DeviceHandle.IndexOf('{');
            if (start < 0) return string.Empty;
            return device.DeviceHandle.Substring(start);
        }
    }
}
