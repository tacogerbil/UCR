using System;
using HidWizards.UCR.Core;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ServiceTests
{
    [TestFixture]
    internal class ScopeProfileAssociationServiceTests
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
        public void GetAssociatedProfile_NoAssociationSet_ReturnsNull()
        {
            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile("scope-1"), Is.Null);
        }

        [Test]
        public void SetAssociatedProfile_ThenGet_ReturnsSameGuid()
        {
            var profileGuid = Guid.NewGuid();

            _context.ScopeProfileAssociationService.SetAssociatedProfile("scope-1", profileGuid);

            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile("scope-1"), Is.EqualTo(profileGuid));
        }

        [Test]
        public void SetAssociatedProfile_Overwrite_ReplacesPreviousAssociation()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            _context.ScopeProfileAssociationService.SetAssociatedProfile("scope-1", first);

            _context.ScopeProfileAssociationService.SetAssociatedProfile("scope-1", second);

            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile("scope-1"), Is.EqualTo(second));
        }

        [Test]
        public void ClearAssociatedProfile_RemovesIt()
        {
            _context.ScopeProfileAssociationService.SetAssociatedProfile("scope-1", Guid.NewGuid());

            _context.ScopeProfileAssociationService.ClearAssociatedProfile("scope-1");

            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile("scope-1"), Is.Null);
        }

        [Test]
        public void SetAssociatedProfile_RaisesChangedEvent()
        {
            var raised = false;
            _context.ScopeProfileAssociationService.ScopeProfileAssociationsChanged += () => raised = true;

            _context.ScopeProfileAssociationService.SetAssociatedProfile("scope-1", Guid.NewGuid());

            Assert.That(raised, Is.True);
        }

        [Test]
        public void ClearAssociatedProfile_WhenNothingToClear_DoesNotRaiseChangedEvent()
        {
            var raised = false;
            _context.ScopeProfileAssociationService.ScopeProfileAssociationsChanged += () => raised = true;

            _context.ScopeProfileAssociationService.ClearAssociatedProfile("scope-with-no-association");

            Assert.That(raised, Is.False);
        }
    }
}
