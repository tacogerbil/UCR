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

        // Returns null when a group with this title (case-insensitive) already exists.
        public DeviceGroup CreateGroup(string title, List<string> memberIdentities)
        {
            if (IsDuplicateTitle(title, null)) return null;

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
            // A group's scope id (used by ScopeProfileAssociationService) is just its Guid as a
            // string -- without this, a deleted group's profile association would dangle forever in
            // groups.json, keyed by a group Guid nothing will ever reference again.
            _context.ScopeProfileAssociationService.ClearAssociatedProfile(groupGuid.ToString());
            _context.ContextChanged();
            DeviceGroupsChanged?.Invoke();
            return true;
        }

        // Returns false when no such group exists, or another group already has this title.
        public bool RenameGroup(Guid groupGuid, string newTitle)
        {
            var group = _context.DeviceGroups.FirstOrDefault(g => g.Guid == groupGuid);
            if (group == null) return false;
            if (IsDuplicateTitle(newTitle, groupGuid)) return false;

            group.Title = newTitle;
            _context.ContextChanged();
            DeviceGroupsChanged?.Invoke();
            return true;
        }

        private bool IsDuplicateTitle(string title, Guid? excludingGuid)
        {
            return _context.DeviceGroups.Any(g =>
                g.Guid != excludingGuid &&
                string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase));
        }

        public bool AddDeviceToGroup(Guid groupGuid, string deviceIdentity)
        {
            var group = _context.DeviceGroups.FirstOrDefault(g => g.Guid == groupGuid);
            if (group == null) return false;
            if (group.MemberDeviceIdentities.Contains(deviceIdentity)) return false;

            group.MemberDeviceIdentities.Add(deviceIdentity);
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
