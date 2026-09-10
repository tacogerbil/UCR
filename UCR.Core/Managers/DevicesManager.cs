using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HidWizards.IOWrapper.DataTransferObjects;
using HidWizards.IOWrapper.ProviderInterface.Interfaces;
using HidWizards.UCR.Core.Adapters;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Utilities;
using NLog;
using Logger = NLog.Logger;
using HidWizards.UCR.Core.Services;

namespace HidWizards.UCR.Core.Managers
{
    public class DevicesManager
    {
        private readonly Context _context;

        private readonly HidWizards.UCR.Core.Services.DeviceCacheService _deviceCacheService;
        private readonly HidWizards.UCR.Core.Services.DeviceInventoryService _inventoryService;
        // Raw provider slots are runtime endpoints, not user-facing physical identity. Detection claims
        // let us distinguish a genuinely second identical Core_Interception device without leaking
        // ordinary slot churn such as #4/#6 into the UI.
        private readonly Dictionary<string, List<string>> _detectedLogicalInputEndpoints = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan DeviceDetectionArmDelay = TimeSpan.FromMilliseconds(350);
        private readonly DeviceDetectionAdapter _deviceDetectionAdapter = new DeviceDetectionAdapter();
        private readonly DeviceProviderAdapter _providerAdapter = new DeviceProviderAdapter();


        public DevicesManager(Context context)
        {
            _context = context;
            _deviceCacheService = new HidWizards.UCR.Core.Services.DeviceCacheService(_context.Store.CacheRoot);
            _inventoryService = new HidWizards.UCR.Core.Services.DeviceInventoryService(_deviceCacheService);
        }

        /// <summary>
        /// Gets a list of available devices from the backend
        /// </summary>
        /// <param name="type"></param>
        public List<Device> GetAvailableDeviceList(DeviceIoType type, bool includeCache = true)
        {
            var raw = GetRawAvailableDeviceList(type, includeCache);
            var result = CollapseLogicalDevicesForDisplay(raw, type);
            _context.DeviceAliasService.ApplyAliases(result);
            return _context.DeviceAliasService.SortDevices(result);
        }

        /// <summary>
        /// Returns the inventory used by the global Devices page. Live provider reports remain authoritative.
        /// If provider enumeration is temporarily unavailable, the input side may fall back to UCR's real
        /// provider-generated device cache; profile configuration is never fabricated into inventory rows.
        /// </summary>
        public List<Device> GetManagementDeviceList(DeviceIoType type)
        {
            return _inventoryService.GetManagementDeviceList(
                type, 
                _context.IOController != null, 
                t => GetAvailableDeviceList(t), 
                GetCachedManagementInputInventory);
        }

        private List<Device> GetCachedManagementInputInventory()
        {
            var result = CollapseLogicalDevicesForDisplay(_deviceCacheService.LoadAllProviders(), DeviceIoType.Input);
            _context.DeviceAliasService.ApplyAliases(result);
            return _context.DeviceAliasService.SortDevices(result);
        }

        public bool HasLoadedProviderReports()
        {
            if (_context.IOController == null) return false;

            var inputs = _providerAdapter.GetProviderReportsResilient(_context.IOController, DeviceIoType.Input);
            var outputs = _providerAdapter.GetProviderReportsResilient(_context.IOController, DeviceIoType.Output);
            return inputs.Count > 0 || outputs.Count > 0;
        }

        private List<Device> GetRawAvailableDeviceList(DeviceIoType type, bool includeCache)
        {
            var result = new List<Device>();
            var providerList = _providerAdapter.GetProviderReportsResilient(_context.IOController, type);

            foreach (var providerReport in providerList)
            {
                var devices = providerReport.Value?.Devices ?? new List<DeviceReport>();
                foreach (var ioWrapperDevice in devices)
                {
                    if (ioWrapperDevice == null) continue;
                    result.Add(new Device(ioWrapperDevice, providerReport.Value, BuildDeviceBindingMenu(ioWrapperDevice.Nodes, type)));
                }

                if (!includeCache) continue;
                var cachedDevices = _deviceCacheService.LoadProvider(providerReport.Value.ProviderDescriptor.ProviderName);
                foreach (var cachedDevice in cachedDevices)
                {
                    if (result.Any(liveDevice => CacheRepresentsLiveEndpoint(cachedDevice, liveDevice))) continue;
                    result.Add(cachedDevice);
                }
            }

            return result;
        }

        public static string GetLogicalDeviceTitle(Device device)
        {
            return DeviceIdentity.GetLogicalTitle(device);
        }

        public static string BuildLogicalDeviceKey(Device device)
        {
            return DeviceIdentity.BuildLogicalKey(device);
        }

        private static string BuildRuntimeEndpointKey(Device device)
        {
            return DeviceIdentity.BuildRuntimeEndpointKey(device);
        }

        public static List<Device> CollapseLogicalDevices(IEnumerable<Device> devices)
        {
            var source = (devices ?? Enumerable.Empty<Device>()).Where(device => device != null).ToList();
            var result = new List<Device>();
            var seenCore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in source)
            {
                if (!string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(device);
                    continue;
                }

                var logicalKey = BuildLogicalDeviceKey(device);
                if (logicalKey == null || seenCore.Add(logicalKey)) result.Add(device);
            }

            return result;
        }

        private List<Device> CollapseLogicalDevicesForDisplay(IEnumerable<Device> devices, DeviceIoType type)
        {
            var source = (devices ?? Enumerable.Empty<Device>()).Where(device => device != null).ToList();
            var result = new List<Device>();
            var coreGroups = source
                .Where(device => string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
                .GroupBy(BuildLogicalDeviceKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.AddRange(source.Where(device =>
                !string.Equals(device.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase)));

            foreach (var group in coreGroups)
            {
                var endpoints = group.ToList();
                var liveEndpoints = endpoints.Where(device => !device.IsCache).ToList();
                var candidateEndpoints = liveEndpoints.Count > 0 ? liveEndpoints : endpoints;
                if (candidateEndpoints.Count == 0) continue;

                var liveKeys = new HashSet<string>(liveEndpoints.Select(BuildRuntimeEndpointKey),
                    StringComparer.OrdinalIgnoreCase);
                List<string> claims;
                if (!_detectedLogicalInputEndpoints.TryGetValue(group.Key, out claims))
                {
                    claims = new List<string>();
                }
                else if (type == DeviceIoType.Input)
                {
                    // Input enumeration is authoritative for the detection claims. Output enumeration may
                    // expose a different subset of the same physical endpoints, so it must never prune them.
                    claims.RemoveAll(endpointKey => !liveKeys.Contains(endpointKey));
                    if (claims.Count == 0) _detectedLogicalInputEndpoints.Remove(group.Key);
                }

                var representatives = new List<Device>();
                foreach (var endpointKey in claims)
                {
                    var match = liveEndpoints.FirstOrDefault(device =>
                        string.Equals(BuildRuntimeEndpointKey(device), endpointKey, StringComparison.OrdinalIgnoreCase));
                    if (match != null) representatives.Add(match);
                }
                if (representatives.Count == 0) representatives.Add(candidateEndpoints[0]);

                for (var index = 0; index < representatives.Count; index++)
                {
                    var representative = representatives[index];
                    representative.LogicalInstanceNumber = index + 1;
                    var title = GetLogicalDeviceTitle(representative);
                    representative.Title = index == 0 ? title : title + " #" + (index + 1);
                    result.Add(representative);
                }
            }

            return result;
        }

        internal int RegisterDetectedInputEndpoint(Device detectedDevice, IEnumerable<Device> liveDevices)
        {
            if (detectedDevice == null ||
                !string.Equals(detectedDevice.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
                return 0;

            var logicalKey = BuildLogicalDeviceKey(detectedDevice);
            var live = (liveDevices ?? Enumerable.Empty<Device>())
                .Where(device => device != null && string.Equals(BuildLogicalDeviceKey(device), logicalKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            var liveKeys = new HashSet<string>(live.Select(BuildRuntimeEndpointKey),
                StringComparer.OrdinalIgnoreCase);

            List<string> claims;
            if (!_detectedLogicalInputEndpoints.TryGetValue(logicalKey, out claims))
            {
                claims = new List<string>();
                _detectedLogicalInputEndpoints[logicalKey] = claims;
            }

            claims.RemoveAll(endpointKey => !liveKeys.Contains(endpointKey));
            var detectedKey = BuildRuntimeEndpointKey(detectedDevice);
            if (claims.Any(endpointKey => string.Equals(endpointKey, detectedKey, StringComparison.OrdinalIgnoreCase)))
                return claims.Count;

            if (claims.Count == 0)
            {
                claims.Add(detectedKey);
                return claims.Count;
            }

            var claimedDevices = claims.Select(endpointKey => live.FirstOrDefault(device =>
                    string.Equals(BuildRuntimeEndpointKey(device), endpointKey, StringComparison.OrdinalIgnoreCase)))
                .Where(device => device != null).ToList();
            var samePhysicalIndex = claimedDevices.FindIndex(device => SamePhysicalEvidence(device, detectedDevice));
            if (samePhysicalIndex >= 0)
            {
                // Same physical path, new provider slot: ordinary slot churn. Replace the endpoint claim.
                claims[samePhysicalIndex] = detectedKey;
            }
            else if (claimedDevices.Count > 0)
            {
                // This different raw endpoint has now itself produced deliberate input while a previously
                // detected endpoint for the same hardware identity is still live. That explicit activity is
                // the evidence required to expose a real second identical device as #2. Passive enumeration
                // alone never creates numbered duplicates.
                claims.Add(detectedKey);
            }
            else
            {
                claims[0] = detectedKey;
            }

            return claims.Count;
        }

        public Device RegisterDetectedInputDevice(Device detectedDevice)
        {
            if (detectedDevice == null) return null;

            if (string.Equals(detectedDevice.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
            {
                RegisterDetectedInputEndpoint(detectedDevice, GetRawAvailableDeviceList(DeviceIoType.Input, false));
            }

            var reconciled = GetAvailableDeviceList(DeviceIoType.Input, false);
            var endpoint = BuildRuntimeEndpointKey(detectedDevice);
            var logicalDevice = reconciled.FirstOrDefault(device =>
                                    string.Equals(BuildRuntimeEndpointKey(device), endpoint, StringComparison.OrdinalIgnoreCase))
                                ?? reconciled.FirstOrDefault(device => LogicalIdentityEquals(device, detectedDevice))
                                ?? detectedDevice;

            // Restore the reconciled logical ordinal, not the raw provider endpoint. This matters for an
            // explicitly removed second identical device: the raw endpoint itself always starts at ordinal 1.
            RestoreInputDevice(logicalDevice);
            return logicalDevice;
        }

        private static bool SamePhysicalEvidence(Device left, Device right)
        {
            return left != null && right != null &&
                   !string.IsNullOrWhiteSpace(left.HidPath) &&
                   !string.IsNullOrWhiteSpace(right.HidPath) &&
                   string.Equals(left.HidPath, right.HidPath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool LogicalIdentityEquals(Device left, Device right)
        {
            return DeviceIdentity.LogicalEquals(left, right);
        }

        /// <summary>
        /// Returns devices intended for user-selection surfaces. Selection and persistence are separate
        /// concerns: a provider may expose enough runtime identity to use a device right now without
        /// exposing a stable identity that is safe for persistent alias/hide/order metadata. Those
        /// session-only devices must remain selectable. Cached devices are excluded by default so stale
        /// provider enumeration slots do not pollute add-device lists.
        /// </summary>
        public List<Device> GetVisibleDeviceList(DeviceIoType type, bool includeCache = false)
        {
            var devices = GetAvailableDeviceList(type, includeCache);
            return type == DeviceIoType.Input
                ? devices.Where(device => !IsInputRemoved(device)).ToList()
                : devices.Where(device => !IsInputRemoved(device) && !_context.DeviceAliasService.IsDeviceHidden(device, devices)).ToList();
        }

        public bool IsInputRemoved(Device device)
        {
            return _context.DeviceAliasService.IsInputRemoved(device);
        }

        public bool RemoveInputDevice(Device device)
        {
            return _context.DeviceAliasService.RemoveInputDevice(device);
        }

        public bool RestoreInputDevice(Device device)
        {
            return _context.DeviceAliasService.RestoreInputDevice(device);
        }

        /// <summary>
        /// Temporarily listens to all live input devices and returns the first device that produces a
        /// deliberate button/key-style input. Axis movement and delta input are ignored so mouse motion,
        /// stick drift and resting analogue values cannot win device identification accidentally.
        /// </summary>
        public async Task<Device> DetectInputDeviceAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var detected = await DetectInputAsync(timeout, cancellationToken, null);
            return detected?.Device;
        }

        /// <summary>
        /// Captures one control from any live input endpoint. The caller receives the control identity
        /// separately from the physical device so it can use the gesture as a selector without changing
        /// a target output-device assignment.
        /// </summary>
        public Task<DetectedInputControl> DetectInputControlAsync(DeviceBindingCategory requiredCategory,
            TimeSpan timeout, CancellationToken cancellationToken)
        {
            return DetectInputAsync(timeout, cancellationToken, requiredCategory);
        }

        private Task<DetectedInputControl> DetectInputAsync(TimeSpan timeout, CancellationToken cancellationToken,
            DeviceBindingCategory? requiredCategory)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            if (_context.BindingManager != null && _context.BindingManager.IsBindModeActive)
            {
                throw new InvalidOperationException("Finish the current binding operation before detecting an input.");
            }

            // Detection must listen to raw provider endpoints. User-facing enumeration deliberately
            // collapses Core_Interception slot churn, but the detector needs the exact endpoint that
            // produced input so it can distinguish churn from a genuinely second identical device.
            var devices = GetRawAvailableDeviceList(DeviceIoType.Input, false);
            if (devices.Count == 0) return Task.FromResult<DetectedInputControl>(null);

            var task = _deviceDetectionAdapter.ArmDetectionTimer(
                (int)timeout.TotalMilliseconds,
                cancellationToken,
                devices,
                requiredCategory,
                DeviceDetectionArmDelay,
                CompleteDeviceDetectionAction);

            foreach (var device in devices)
            {
                try
                {
                    _context.IOController.SetDetectionMode(DetectionMode.Bind,
                        GetProviderDescriptor(device), GetDeviceDescriptor(device), DeviceDetectionInputChanged);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception,
                        $"Could not enable input detection for provider={device.ProviderName}, handle={device.DeviceHandle}, instance={device.DeviceNumber}");
                }
            }

            return task;
        }

        public void CancelInputDeviceDetection()
        {
            _deviceDetectionAdapter.CancelInputDeviceDetection(CompleteDeviceDetectionAction);
        }

        private void DeviceDetectionInputChanged(ProviderDescriptor providerDescriptor,
            DeviceDescriptor deviceDescriptor, BindingReport bindingReport, short value)
        {
            var state = _deviceDetectionAdapter.GetState();
            if (state == null) return;

            var devices = state.Devices;
            var acceptAfter = state.AcceptAfterUtc;
            var requiredCategory = state.RequiredCategory;

            if (bindingReport == null) return;

            var category = DeviceBinding.MapCategory(bindingReport.Category);
            if (requiredCategory.HasValue)
            {
                if (category != requiredCategory.Value || !IsDetectedControlValueValid(category, value)) return;
            }
            else
            {
                // Device identification intentionally ignores analogue/delta motion so ordinary mouse
                // movement, stick drift and resting values cannot select a device by accident.
                var isDeliberatePress = category == DeviceBindingCategory.Momentary && value != 0;
                var isDiscreteEvent = category == DeviceBindingCategory.Event;
                if (!isDeliberatePress && !isDiscreteEvent) return;
            }

            var device = devices?.FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderName, providerDescriptor?.ProviderName,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.DeviceHandle, deviceDescriptor.DeviceHandle,
                    StringComparison.OrdinalIgnoreCase) &&
                candidate.DeviceNumber == deviceDescriptor.DeviceInstance);

            if (device == null) return;

            // The mouse gesture that opened a detector can leak into Interception briefly. Suppress
            // only pointer presses during that arm window; keyboard/controller input must be accepted
            // immediately instead of being thrown away for a blanket 350ms.
            if (DateTime.UtcNow < acceptAfter && IsPointerDetectionDevice(device)) return;

            var descriptor = bindingReport.BindingDescriptor;
            var detected = new DetectedInputControl
            {
                Device = device,
                ControlTitle = bindingReport.Title ?? string.Empty
            };

            Logger.Debug($"Detected input: provider={device.ProviderName}, handle={device.DeviceHandle}, instance={device.DeviceNumber}, control={detected.ControlTitle}, category={category}, type={descriptor.Type}, index={descriptor.Index}, subIndex={descriptor.SubIndex}");
            ThreadPool.QueueUserWorkItem(_ => _deviceDetectionAdapter.CompleteDeviceDetection(detected, CompleteDeviceDetectionAction));
        }

        private static bool IsPointerDetectionDevice(Device device)
        {
            if (device == null) return false;
            var title = GetLogicalDeviceTitle(device) ?? string.Empty;
            var handle = device.DeviceHandle ?? string.Empty;
            return title.StartsWith("M:", StringComparison.OrdinalIgnoreCase) ||
                   title.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   handle.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) ||
                   handle.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDetectedControlValueValid(DeviceBindingCategory category, short value)
        {
            switch (category)
            {
                case DeviceBindingCategory.Delta:
                case DeviceBindingCategory.Event:
                    return value != 0 || category == DeviceBindingCategory.Event;
                case DeviceBindingCategory.Momentary:
                    return value != 0;
                case DeviceBindingCategory.Range:
                    var wideValue = Functions.WideAbs(value);
                    return Constants.AxisMaxValue * 0.4 < wideValue && Constants.AxisMaxValue * 0.6 > wideValue;
                default:
                    return false;
            }
        }

        private void CompleteDeviceDetectionAction(DetectedInputControl detectedInput)
        {
            var state = _deviceDetectionAdapter.GetState();
            if (state == null || state.Devices == null) return;

            foreach (var device in state.Devices)
            {
                try
                {
                    _context.IOController.SetDetectionMode(DetectionMode.Subscription,
                        GetProviderDescriptor(device), GetDeviceDescriptor(device));
                }
                catch (Exception exception)
                {
                    Logger.Error(exception,
                        $"Could not restore input subscription after detection for provider={device.ProviderName}, handle={device.DeviceHandle}, instance={device.DeviceNumber}");
                }
            }
        }

        private static DeviceDescriptor GetDeviceDescriptor(Device device)
        {
            return new DeviceDescriptor
            {
                DeviceHandle = device.DeviceHandle,
                DeviceInstance = device.DeviceNumber
            };
        }

        private static ProviderDescriptor GetProviderDescriptor(Device device)
        {
            return new ProviderDescriptor
            {
                ProviderName = device.ProviderName
            };
        }

        public static bool CacheRepresentsLiveEndpoint(Device cachedDevice, Device liveDevice)
        {
            return DeviceIdentity.CacheRepresentsLiveEndpoint(cachedDevice, liveDevice);
        }

        public static bool TryGetWindowsDeviceInstanceId(Device device, out string instanceId)
        {
            instanceId = null;
            var hidPath = device?.HidPath;
            if (string.IsNullOrWhiteSpace(hidPath)) return false;

            var normalized = hidPath.Trim();
            if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal)) normalized = normalized.Substring(4);

            var classGuidIndex = normalized.IndexOf("#{", StringComparison.Ordinal);
            if (classGuidIndex >= 0) normalized = normalized.Substring(0, classGuidIndex);
            normalized = normalized.TrimEnd('#');

            var parts = normalized.Split(new[] { '#' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;

            instanceId = string.Join(@"\", parts);
            return !string.IsNullOrWhiteSpace(instanceId);
        }

        public void RefreshDeviceList()
        {
            _context.IOController.RefreshDevices();
        }

        public List<Device> GetAvailableDevicesListFromSameProvider(DeviceIoType type, Device device)
        {
            var availableDeviceList = GetVisibleDeviceList(type);
            return availableDeviceList.Where(d => d.ProviderName.Equals(device.ProviderName)).ToList();
        }

        /// <summary>
        /// Resolves a persisted/profile device to the descriptor that the provider is using right now.
        /// Prefer the per-device HID path when the provider exposes one. Otherwise, a unique hardware
        /// handle can safely survive provider instance-number changes. Legacy exact descriptor matching
        /// remains as the final fallback for providers such as XInput/ViGEm that expose only numbered slots.
        ///
        /// If a persisted HID path no longer matches, do not guess from a different physical path.
        /// Selecting by an old instance number could silently bind the wrong unit.
        /// </summary>
        public Device ResolveDevice(Device configuredDevice, DeviceIoType type)
        {
            if (configuredDevice == null) return null;

            // "Remove from UCR" is persistent operational exclusion, not merely list decoration. Keep
            // profile configuration intact so Detect Device can restore it later, but do not resolve or
            // subscribe a removed logical device while it is excluded from UCR. This also suppresses
            // the output side of a combined input/output device.
            if (IsInputRemoved(configuredDevice)) return null;

            // Profiles store a provider descriptor, but Core_Interception endpoint slots are volatile.
            // Resolve against raw endpoints so the runtime provider still receives the exact current slot.
            var availableDevices = GetRawAvailableDeviceList(type, false);
            Device resolvedDevice;

            if (string.Equals(configuredDevice.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
            {
                var logicalKey = BuildLogicalDeviceKey(configuredDevice);
                var candidates = availableDevices.Where(device =>
                    string.Equals(BuildLogicalDeviceKey(device), logicalKey, StringComparison.OrdinalIgnoreCase)).ToList();

                if (candidates.Count == 0) return null;

                List<string> claims;
                _detectedLogicalInputEndpoints.TryGetValue(logicalKey, out claims);
                if (type == DeviceIoType.Input && claims != null && claims.Count > 0)
                {
                    var claimed = claims.Select(endpointKey => candidates.FirstOrDefault(device =>
                            string.Equals(BuildRuntimeEndpointKey(device), endpointKey, StringComparison.OrdinalIgnoreCase)))
                        .Where(device => device != null).ToList();
                    var logicalIndex = Math.Max(0, configuredDevice.LogicalInstanceNumber - 1);
                    resolvedDevice = claimed.FirstOrDefault(device => DescriptorEquals(device, configuredDevice))
                                     ?? (logicalIndex < claimed.Count ? claimed[logicalIndex] : claimed.FirstOrDefault());
                }
                else
                {
                    resolvedDevice = candidates.FirstOrDefault(device => DescriptorEquals(device, configuredDevice))
                                     ?? candidates[0];
                }
            }
            else
            {
                resolvedDevice = ResolveDevice(configuredDevice, availableDevices);
            }

            // Migrate legacy profiles forward only when the match is unambiguous.
            if (resolvedDevice != null && string.IsNullOrEmpty(configuredDevice.HidPath) &&
                !string.IsNullOrEmpty(resolvedDevice.HidPath))
            {
                var sameHandleCount = availableDevices.Count(d => d != null &&
                    string.Equals(d.ProviderName, configuredDevice.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.DeviceHandle, configuredDevice.DeviceHandle, StringComparison.OrdinalIgnoreCase));

                if (sameHandleCount == 1)
                {
                    configuredDevice.HidPath = resolvedDevice.HidPath;
                    _context.ContextChanged();
                }
            }

            return resolvedDevice;
        }

        public static Device ResolveDevice(Device configuredDevice, IEnumerable<Device> availableDevices)
        {
            if (configuredDevice == null || availableDevices == null) return null;

            var providerCandidates = availableDevices
                .Where(d => d != null &&
                            string.Equals(d.ProviderName, configuredDevice.ProviderName,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (providerCandidates.Count == 0) return null;

            if (!string.IsNullOrEmpty(configuredDevice.HidPath))
            {
                var hidMatches = providerCandidates
                    .Where(d => !string.IsNullOrEmpty(d.HidPath) &&
                                string.Equals(d.HidPath, configuredDevice.HidPath,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (hidMatches.Count == 1) return hidMatches[0];
                if (hidMatches.Count > 1)
                {
                    return hidMatches.FirstOrDefault(d => DescriptorEquals(d, configuredDevice));
                }
            }

            var handleMatches = providerCandidates
                .Where(d => string.Equals(d.DeviceHandle, configuredDevice.DeviceHandle,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrEmpty(configuredDevice.HidPath))
            {
                // A different non-empty HID path is evidence that this is not the persisted endpoint.
                // It may be the same unit moved to another port, but it may equally be another identical
                // unit; without stronger evidence, requiring an explicit re-selection is safer.
                if (handleMatches.Any(d => !string.IsNullOrEmpty(d.HidPath)))
                {
                    return null;
                }

                // Some providers/builds may stop exposing HidPath. Only then fall back to a unique handle.
                return handleMatches.Count == 1 ? handleMatches[0] : null;
            }

            if (handleMatches.Count == 1) return handleMatches[0];

            if (string.Equals(configuredDevice.ProviderName, "Core_Interception", StringComparison.OrdinalIgnoreCase))
            {
                var logicalKey = BuildLogicalDeviceKey(configuredDevice);
                var logicalMatches = providerCandidates.Where(device =>
                    string.Equals(BuildLogicalDeviceKey(device), logicalKey, StringComparison.OrdinalIgnoreCase)).ToList();
                if (logicalMatches.Count > 0)
                {
                    return logicalMatches.FirstOrDefault(device => DescriptorEquals(device, configuredDevice))
                           ?? logicalMatches[0];
                }
            }

            // Numbered virtual/API slots are the identity those providers intentionally expose.
            if (UsesLogicalSlotIdentity(configuredDevice.ProviderName))
            {
                return handleMatches.FirstOrDefault(d => DescriptorEquals(d, configuredDevice));
            }

            return null;
        }

        private static bool UsesLogicalSlotIdentity(string providerName)
        {
            return DeviceIdentity.UsesLogicalSlotIdentity(providerName);
        }

        public static bool DescriptorEquals(Device left, Device right)
        {
            return DeviceIdentity.DescriptorEquals(left, right);
        }

        public static bool PersistedIdentityEquals(Device left, Device right)
        {
            return DeviceIdentity.PersistedEquals(left, right);
        }

        #region Device aliases (Delegated)
        
        public string GetDisplayTitle(Device device) => _context.DeviceAliasService.GetDisplayTitle(device);
        public string GetDeviceAlias(Device device) => _context.DeviceAliasService.GetDeviceAlias(device);
        public bool GetDeviceHidden(Device device) => _context.DeviceAliasService.GetDeviceHidden(device);
        public int GetDeviceSortOrder(Device device) => _context.DeviceAliasService.GetDeviceSortOrder(device);
        public DeviceOutlineColor GetDeviceOutlineColor(Device device) => _context.DeviceAliasService.GetDeviceOutlineColor(device);
        
        public bool CanPersistDeviceAlias(Device device, IEnumerable<Device> liveDevices)
        {
            return _context.DeviceAliasService.CanPersistDeviceAlias(device, liveDevices);
        }

        public bool CanPersistDeviceAlias(Device device, DeviceIoType type)
        {
            return _context.DeviceAliasService.CanPersistDeviceAlias(device, GetAvailableDeviceList(type, false));
        }

        public bool TrySetDeviceAlias(Device device, IEnumerable<Device> liveDevices, string alias, out string error)
        {
            return _context.DeviceAliasService.TrySetDeviceAlias(device, liveDevices, alias, out error);
        }

        public bool TrySetDeviceAlias(Device device, DeviceIoType type, string alias, out string error)
        {
            return _context.DeviceAliasService.TrySetDeviceAlias(device, GetAvailableDeviceList(type, false), alias, out error);
        }

        public bool TrySetDevicePresentation(Device device, IEnumerable<Device> liveDevices, string alias, bool hidden,
            int sortOrder, DeviceOutlineColor outlineColor, out string error)
        {
            return _context.DeviceAliasService.TrySetDevicePresentation(device, liveDevices, alias, hidden, sortOrder, outlineColor, out error);
        }

        public bool TrySetDevicePresentation(Device device, DeviceIoType type, string alias, bool hidden,
            int sortOrder, DeviceOutlineColor outlineColor, out string error)
        {
            return _context.DeviceAliasService.TrySetDevicePresentation(device, GetAvailableDeviceList(type, false), alias, hidden, sortOrder, outlineColor, out error);
        }
        
        public void MergeDeviceAliases(IEnumerable<DeviceAlias> aliases, bool overwriteExisting)
        {
            _context.DeviceAliasService.MergeDeviceAliases(aliases, overwriteExisting);
        }
        
        public void ReplaceDeviceAliases(IEnumerable<DeviceAlias> aliases)
        {
            _context.DeviceAliasService.ReplaceDeviceAliases(aliases);
        }

        public static bool AliasIdentityEquals(DeviceAlias left, DeviceAlias right)
        {
            return DeviceAliasService.AliasIdentityEquals(left, right);
        }

        public static DeviceAlias BuildAliasIdentity(Device device)
        {
            return DeviceAliasService.BuildAliasIdentity(device);
        }
        
        #endregion



        public List<DeviceBindingNode> GetDeviceBindingMenu(Device device, DeviceIoType type, bool includeCache = true)
        {
            var resolvedDevice = ResolveDevice(device, type);
            if (resolvedDevice != null)
            {
                return resolvedDevice.GetDeviceBindingMenu();
            }

            if (includeCache)
            {
                var cachedDevice = GetAvailableDeviceList(type, true)
                    .FirstOrDefault(candidate => candidate.IsCache && DescriptorEquals(candidate, device));
                if (cachedDevice != null) return cachedDevice.GetDeviceBindingMenu();
            }

            return new List<DeviceBindingNode>
            {
                new DeviceBindingNode()
                {
                    Title = "Device not connected"
                }
            };
        }

        private static List<DeviceBindingNode> BuildDeviceBindingMenu(List<DeviceReportNode> deviceNodes, DeviceIoType type)
        {
            var result = new List<DeviceBindingNode>();
            if (deviceNodes == null) return result;

            foreach (var deviceNode in deviceNodes)
            {
                var groupNode = new DeviceBindingNode()
                {
                    Title = deviceNode.Title,
                    ChildrenNodes = BuildDeviceBindingMenu(deviceNode.Nodes, type),
                };

                if (groupNode.ChildrenNodes == null) groupNode.ChildrenNodes = new List<DeviceBindingNode>();
                

                foreach (var bindingInfo in deviceNode.Bindings)
                {
                    var bindingNode = new DeviceBindingNode()
                    {
                        Title = bindingInfo.Title,
                        DeviceBindingInfo = new DeviceBindingInfo()
                        {
                            KeyType = (int)bindingInfo.BindingDescriptor.Type,
                            KeyValue = bindingInfo.BindingDescriptor.Index,
                            KeySubValue = bindingInfo.BindingDescriptor.SubIndex,
                            DeviceBindingCategory = DeviceBinding.MapCategory(bindingInfo.Category),
                            Blockable = bindingInfo.Blockable
                        }
                    };


                    groupNode.ChildrenNodes.Add(bindingNode);
                }
                result.Add(groupNode);
            }
            return result.Count != 0 ? result : null;
        }

        #region Cache

        public bool UpdateDeviceCache()
        {
            RefreshDeviceList();
            var availableDeviceList = GetAvailableDeviceList(DeviceIoType.Input, false);
            return _deviceCacheService.UpdateDeviceCache(availableDeviceList, GetDeviceBindingMenu);
        }

        #endregion
    }
}
