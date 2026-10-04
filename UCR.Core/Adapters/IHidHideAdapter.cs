using System.Collections.Generic;

namespace HidWizards.UCR.Core.Adapters
{
    /// <summary>
    /// Thin boundary over the HidHide driver. Contains no policy: every member maps 1:1 to a driver
    /// operation so the policy in HidHideProfileService can be tested against a fake.
    /// Any member may throw if the driver is missing or its control handle is busy.
    /// </summary>
    public interface IHidHideAdapter
    {
        /// <summary>True when the HidHide driver is installed and operable.</summary>
        bool IsInstalled { get; }

        /// <summary>Global device-hiding switch (HidHide's "cloak").</summary>
        bool IsActive { get; set; }

        IReadOnlyList<string> BlockedInstanceIds { get; }

        /// <summary>Applications that are still allowed to see hidden devices.</summary>
        IReadOnlyList<string> ApplicationPaths { get; }

        void AddBlockedInstanceId(string instanceId);

        void RemoveBlockedInstanceId(string instanceId);

        void AddApplicationPath(string path);
    }
}
