using System;

namespace HidWizards.UCR.Core.Services
{
    /// <summary>
    /// Tracks which Game Profile a mapping scope (a single device or a DeviceGroup) is bound to.
    /// Scope ids are the same ones already used to display the toolbar's Input Scope list: a device's
    /// DeviceIdentity.BuildLogicalKey, or a DeviceGroup's Guid as a string.
    /// </summary>
    public class ScopeProfileAssociationService
    {
        private readonly Context _context;

        public ScopeProfileAssociationService(Context context)
        {
            _context = context;
        }

        public event Action ScopeProfileAssociationsChanged;

        public Guid? GetAssociatedProfile(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId)) return null;
            return _context.ScopeProfileAssociations.TryGetValue(scopeId, out var profileGuid) ? profileGuid : (Guid?)null;
        }

        public void SetAssociatedProfile(string scopeId, Guid profileGuid)
        {
            if (string.IsNullOrEmpty(scopeId)) return;

            _context.ScopeProfileAssociations[scopeId] = profileGuid;
            _context.ContextChanged();
            ScopeProfileAssociationsChanged?.Invoke();
        }

        public void ClearAssociatedProfile(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId)) return;
            if (!_context.ScopeProfileAssociations.Remove(scopeId)) return;

            _context.ContextChanged();
            ScopeProfileAssociationsChanged?.Invoke();
        }
    }
}
