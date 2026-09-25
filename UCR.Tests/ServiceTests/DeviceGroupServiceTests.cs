using HidWizards.UCR.Core;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ServiceTests
{
    [TestFixture]
    internal class DeviceGroupServiceTests
    {
        private Context _context;

        [SetUp]
        public void SetUp()
        {
            _context = new Context();
            _context.IOController?.Dispose();
            _context.IOController = null;
        }

        [Test]
        public void CreateGroup_DuplicateTitle_ReturnsNull()
        {
            _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            Assert.That(result, Is.Null);
            Assert.That(_context.DeviceGroups, Has.Count.EqualTo(1));
        }

        [Test]
        public void CreateGroup_DuplicateTitleDifferentCase_ReturnsNull()
        {
            _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.CreateGroup("racing rig", new System.Collections.Generic.List<string>());

            Assert.That(result, Is.Null);
        }

        [Test]
        public void RenameGroup_ToAnotherGroupsTitle_ReturnsFalseAndLeavesTitleUnchanged()
        {
            var first = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());
            var second = _context.DeviceGroupService.CreateGroup("Flight Sim", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.RenameGroup(second.Guid, "Racing Rig");

            Assert.That(result, Is.False);
            Assert.That(second.Title, Is.EqualTo("Flight Sim"));
        }

        [Test]
        public void RenameGroup_ToItsOwnCurrentTitle_Succeeds()
        {
            var group = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.RenameGroup(group.Guid, "Racing Rig");

            Assert.That(result, Is.True);
        }

        [Test]
        public void AddDeviceToGroup_NewDevice_AddsIt()
        {
            var group = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.AddDeviceToGroup(group.Guid, "device-1");

            Assert.That(result, Is.True);
            Assert.That(group.MemberDeviceIdentities, Does.Contain("device-1"));
        }

        [Test]
        public void AddDeviceToGroup_AlreadyAMember_ReturnsFalseAndDoesNotDuplicate()
        {
            var group = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string> { "device-1" });

            var result = _context.DeviceGroupService.AddDeviceToGroup(group.Guid, "device-1");

            Assert.That(result, Is.False);
            Assert.That(group.MemberDeviceIdentities, Has.Count.EqualTo(1));
        }

        [Test]
        public void AddDeviceToGroup_UnknownGroup_ReturnsFalse()
        {
            var result = _context.DeviceGroupService.AddDeviceToGroup(System.Guid.NewGuid(), "device-1");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RemoveGroup_ExistingGroup_RemovesItAndReturnsTrue()
        {
            var group = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());

            var result = _context.DeviceGroupService.RemoveGroup(group.Guid);

            Assert.That(result, Is.True);
            Assert.That(_context.DeviceGroups, Has.Count.EqualTo(0));
        }

        [Test]
        public void RemoveGroup_UnknownGroup_ReturnsFalse()
        {
            var result = _context.DeviceGroupService.RemoveGroup(System.Guid.NewGuid());

            Assert.That(result, Is.False);
        }

        [Test]
        public void RemoveGroup_WithAssociatedProfile_ClearsTheAssociationTooInsteadOfLeavingItOrphaned()
        {
            var group = _context.DeviceGroupService.CreateGroup("Racing Rig", new System.Collections.Generic.List<string>());
            var scopeId = group.Guid.ToString();
            _context.ScopeProfileAssociationService.SetAssociatedProfile(scopeId, System.Guid.NewGuid());

            _context.DeviceGroupService.RemoveGroup(group.Guid);

            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile(scopeId), Is.Null);
        }
    }
}
