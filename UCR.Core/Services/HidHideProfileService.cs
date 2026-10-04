using System;
using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Adapters;
using HidWizards.UCR.Core.Models;
using NLog;

namespace HidWizards.UCR.Core.Services
{
    /// <summary>
    /// Applies a profile's HiddenDevices to the HidHide driver when that profile becomes active and
    /// releases them when it stops, so each game profile hides exactly the devices it needs.
    /// Idempotent: calling Apply repeatedly with the same profile changes nothing. Never throws --
    /// a missing/busy driver is logged and reported, since hiding is optional and must not break
    /// profile activation.
    /// Side effects: HidHide driver state only (through the injected adapter).
    /// </summary>
    public sealed class HidHideProfileService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IHidHideAdapter _adapter;
        private readonly string _selfExecutablePath;
        private List<string> _owned = new List<string>();
        private bool? _activeStateBeforeUcr;

        /// <param name="adapter">Driver boundary.</param>
        /// <param name="selfExecutablePath">UCR's own exe; whitelisted so UCR can still read hidden devices.</param>
        public HidHideProfileService(IHidHideAdapter adapter, string selfExecutablePath)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _selfExecutablePath = selfExecutablePath;
        }

        /// <summary>Instance IDs UCR currently has blocked (for diagnostics/tests).</summary>
        public IReadOnlyList<string> OwnedInstanceIds => _owned;

        /// <summary>
        /// Moves the driver to <paramref name="profile"/>'s hidden-device set; null releases everything UCR hid.
        /// </summary>
        /// <returns>Result describing success and a short message suitable for logging.</returns>
        public HidHideApplyResult Apply(Profile profile)
        {
            var desired = (profile?.HiddenDevices ?? new List<HiddenDevice>()).Select(d => d.InstanceId).ToList();
            if (desired.Count == 0 && _owned.Count == 0) return HidHideApplyResult.Ok("Nothing to hide.");

            try
            {
                if (!_adapter.IsInstalled)
                {
                    return desired.Count == 0
                        ? HidHideApplyResult.Ok("Nothing to hide.")
                        : HidHideApplyResult.Fail("HidHide is not installed; devices were not hidden.");
                }
                return ApplyPlan(desired);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "HidHide update failed.");
                return HidHideApplyResult.Fail("HidHide update failed: " + exception.Message);
            }
        }

        /// <summary>Releases everything UCR hid. Safe to call repeatedly (e.g. at shutdown).</summary>
        public HidHideApplyResult ReleaseAll() => Apply(null);

        private HidHideApplyResult ApplyPlan(List<string> desired)
        {
            var plan = HidHidePlanner.Plan(desired, _owned, _adapter.BlockedInstanceIds);
            if (plan.ToBlock.Count > 0 && !EnsureSelfWhitelisted())
                return HidHideApplyResult.Fail("UCR's own path is unavailable, so hiding was skipped to avoid blinding UCR.");

            foreach (var id in plan.ToUnblock) _adapter.RemoveBlockedInstanceId(id);
            foreach (var id in plan.ToBlock) _adapter.AddBlockedInstanceId(id);
            _owned = plan.OwnedAfter.ToList();

            UpdateGlobalSwitch();
            Logger.Info($"HidHide: blocked {plan.ToBlock.Count}, released {plan.ToUnblock.Count}, owned now {_owned.Count}.");
            return HidHideApplyResult.Ok($"Hiding {_owned.Count} device(s).");
        }

        private bool EnsureSelfWhitelisted()
        {
            if (string.IsNullOrWhiteSpace(_selfExecutablePath)) return false;
            var alreadyListed = _adapter.ApplicationPaths.Any(p => string.Equals(p, _selfExecutablePath, StringComparison.OrdinalIgnoreCase));
            if (!alreadyListed) _adapter.AddApplicationPath(_selfExecutablePath);
            return true;
        }

        // Turn the global cloak on while we own anything; put it back how we found it once we own nothing.
        private void UpdateGlobalSwitch()
        {
            if (_owned.Count > 0)
            {
                if (_activeStateBeforeUcr == null) _activeStateBeforeUcr = _adapter.IsActive;
                if (!_adapter.IsActive) _adapter.IsActive = true;
                return;
            }

            if (_activeStateBeforeUcr.HasValue)
            {
                if (_adapter.IsActive != _activeStateBeforeUcr.Value) _adapter.IsActive = _activeStateBeforeUcr.Value;
                _activeStateBeforeUcr = null;
            }
        }
    }

    public sealed class HidHideApplyResult
    {
        public bool Success { get; }
        public string Message { get; }

        private HidHideApplyResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static HidHideApplyResult Ok(string message) => new HidHideApplyResult(true, message);
        public static HidHideApplyResult Fail(string message) => new HidHideApplyResult(false, message);
    }
}
