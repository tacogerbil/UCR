using System;
using System.Collections.Generic;
using Nefarius.Drivers.HidHide;

namespace HidWizards.UCR.Core.Adapters
{
    /// <summary>
    /// IHidHideAdapter backed by Nefarius.Drivers.HidHide's HidHideControlService. No business logic.
    /// </summary>
    public sealed class HidHideDriverAdapter : IHidHideAdapter
    {
        private readonly HidHideControlService _service = new HidHideControlService();

        public bool IsInstalled
        {
            get
            {
                try { return _service.IsInstalled; }
                catch (Exception) { return false; }
            }
        }

        public bool IsActive
        {
            get => _service.IsActive;
            set => _service.IsActive = value;
        }

        public IReadOnlyList<string> BlockedInstanceIds => _service.BlockedInstanceIds;

        public IReadOnlyList<string> ApplicationPaths => _service.ApplicationPaths;

        public void AddBlockedInstanceId(string instanceId) => _service.AddBlockedInstanceId(instanceId);

        public void RemoveBlockedInstanceId(string instanceId) => _service.RemoveBlockedInstanceId(instanceId);

        public void AddApplicationPath(string path) => _service.AddApplicationPath(path, true);
    }
}
