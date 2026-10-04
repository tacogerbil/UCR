using System;
using System.Collections.Generic;
using System.Linq;

namespace HidWizards.UCR.Core.Services
{
    /// <summary>The driver changes needed to move from the current state to a profile's desired state.</summary>
    public sealed class HidHidePlan
    {
        public IReadOnlyList<string> ToBlock { get; }
        public IReadOnlyList<string> ToUnblock { get; }
        /// <summary>Every instance ID UCR is responsible for after the plan is carried out.</summary>
        public IReadOnlyList<string> OwnedAfter { get; }

        public HidHidePlan(IReadOnlyList<string> toBlock, IReadOnlyList<string> toUnblock, IReadOnlyList<string> ownedAfter)
        {
            ToBlock = toBlock;
            ToUnblock = toUnblock;
            OwnedAfter = ownedAfter;
        }
    }

    /// <summary>Pure planning logic for HidHideProfileService. No I/O, deterministic.</summary>
    public static class HidHidePlanner
    {
        // Virtual output devices UCR itself creates (ViGEm Xbox 360 / DS4 emulation shows up as
        // vid_045e&pid_028e; vJoy as vid_1234&pid_bead). Hiding these would hide UCR's own output
        // from the game, defeating the whole point, so they are never blockable.
        private static readonly string[] ProtectedHardwareIds =
        {
            "vid_045e&pid_028e",
            "vid_1234&pid_bead",
            // Root-enumerated virtual HID devices (vJoy's HID child is HID\HIDCLASS\..., with no VID/PID in
            // its instance ID). Real hardware always carries a VID_/PID_ in its instance ID.
            @"hid\hidclass\"
        };

        public static bool IsProtected(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            return ProtectedHardwareIds.Any(id => instanceId.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Plans the driver edits. Only IDs UCR added itself (<paramref name="owned"/>) are ever removed,
        /// so devices the user hid by hand in HidHide are left alone; a desired ID that is already
        /// blocked by someone else is not claimed as owned.
        /// </summary>
        /// <param name="desired">Instance IDs the active profile wants hidden (empty/null = release all).</param>
        /// <param name="owned">IDs UCR blocked earlier and still considers its own.</param>
        /// <param name="currentlyBlocked">IDs the driver reports as blocked right now.</param>
        public static HidHidePlan Plan(IEnumerable<string> desired, IEnumerable<string> owned, IEnumerable<string> currentlyBlocked)
        {
            var wanted = Normalize(desired).Where(id => !IsProtected(id)).ToList();
            var ownedSet = Normalize(owned).ToList();
            var blockedSet = new HashSet<string>(Normalize(currentlyBlocked), StringComparer.OrdinalIgnoreCase);
            var wantedSet = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);

            var toUnblock = ownedSet.Where(id => !wantedSet.Contains(id) && blockedSet.Contains(id)).ToList();
            var ownedSetLookup = new HashSet<string>(ownedSet, StringComparer.OrdinalIgnoreCase);
            var toBlock = wanted.Where(id => !blockedSet.Contains(id)).ToList();
            var ownedAfter = wanted.Where(id => ownedSetLookup.Contains(id) || toBlock.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();

            return new HidHidePlan(toBlock, toUnblock, ownedAfter);
        }

        private static IEnumerable<string> Normalize(IEnumerable<string> ids)
        {
            return (ids ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
