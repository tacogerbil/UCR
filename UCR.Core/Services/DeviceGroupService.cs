using System;
using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.Core.Services
{
    public class DeviceGroupService
    {
        private readonly Context _context;

        public DeviceGroupService(Context context)
        {
            _context = context;
        }

        public event Action DeviceGroupsChanged;

        public DeviceGroup CreateGroup(string title, List<string> memberIdentities)
        {
            var group = new DeviceGroup(title, memberIdentities);
            _context.DeviceGroups.Add(group);
            _context.ContextChanged();
            DeviceGroupsChanged?.Invoke();
            return group;
        }

        public bool RemoveGroup(Guid groupGuid)
        {
            var group = _context.DeviceGroups.FirstOrDefault(g => g.Guid == groupGuid);
            if (group == null) return false;

            _context.DeviceGroups.Remove(group);
            _context.ContextChanged();
            DeviceGroupsChanged?.Invoke();
            return true;
        }

        public bool RenameGroup(Guid groupGuid, string newTitle)
        {
            var group = _context.DeviceGroups.FirstOrDefault(g => g.Guid == groupGuid);
            if (group == null) return false;

            group.Title = newTitle;
            _context.ContextChanged();
            DeviceGroupsChanged?.Invoke();
            return true;
        }

        public bool RemoveDeviceFromGroup(Guid groupGuid, string deviceIdentity)
        {
            var group = _context.DeviceGroups.FirstOrDefault(g => g.Guid == groupGuid);
            if (group == null) return false;

            if (group.MemberDeviceIdentities.Remove(deviceIdentity))
            {
                _context.ContextChanged();
                DeviceGroupsChanged?.Invoke();
                return true;
            }
            return false;
        }
    }
}
