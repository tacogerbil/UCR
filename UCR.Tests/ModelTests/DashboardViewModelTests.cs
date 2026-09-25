using System;
using HidWizards.UCR.Core;
using HidWizards.UCR.ViewModels.Dashboard;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // Covers DashboardViewModel.CanBeginMapping/ExecuteBeginMapping/ApplyAssociatedProfileForCurrentScope
    // -- the exact code path that silently never activated a profile before the 2026-09-22 fix (Listen
    // mode, the manual picker, and live device input all depend on Context.ActiveProfile being set,
    // which previously only happened via a separate, disconnected toolbar button nothing in this flow
    // ever pointed the user at).
    [TestFixture]
    internal class DashboardViewModelTests
    {
        private Context _context;
        private DashboardViewModel _dashboard;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            _dashboard = new DashboardViewModel(_context);
        }

        private InputScopeItem MakeScope(string id = "scope-1") =>
            new InputScopeItem(id, "Scope Display Name", false, new System.Collections.Generic.List<string> { id });

        [Test]
        public void CanBeginMapping_NoScopeSelected_IsFalse()
        {
            Assert.That(_dashboard.BeginMappingCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void CanBeginMapping_ScopeSelectedButNoAssociatedProfile_IsFalse()
        {
            _dashboard.SelectedInputScope = MakeScope();

            Assert.That(_dashboard.BeginMappingCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void CanBeginMapping_ScopeSelectedWithAssociatedProfile_IsTrue()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var scope = MakeScope();
            _context.ScopeProfileAssociationService.SetAssociatedProfile(scope.Id, profile.Guid);

            _dashboard.SelectedInputScope = scope;

            Assert.That(_dashboard.BeginMappingCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void ExecuteBeginMapping_ResolvesAssociatedProfile_AndUnlocksMapping()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var scope = MakeScope();
            _context.ScopeProfileAssociationService.SetAssociatedProfile(scope.Id, profile.Guid);
            _dashboard.SelectedInputScope = scope;

            _dashboard.BeginMappingCommand.Execute(null);

            Assert.That(_dashboard.IsMappingUnlocked, Is.True);
            Assert.That(_dashboard.SelectedProfileItem?.Profile.Guid, Is.EqualTo(profile.Guid));
        }

        [Test]
        public void ExecuteBeginMapping_RequestsShowMappingEveryTime_NotJustOnFirstUnlock()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var scope = MakeScope();
            _context.ScopeProfileAssociationService.SetAssociatedProfile(scope.Id, profile.Guid);
            _dashboard.SelectedInputScope = scope;

            var requestCount = 0;
            _dashboard.RequestShowMapping = () => requestCount++;

            _dashboard.BeginMappingCommand.Execute(null);
            _dashboard.BeginMappingCommand.Execute(null);

            // Regression guard for the bug where IsMappingUnlocked's setter no-ops once already true,
            // so navigation-on-PropertyChanged only ever fired the first time -- Begin Mapping became
            // permanently unable to bring the user back to the Mapping tab after they'd navigated away.
            Assert.That(requestCount, Is.EqualTo(2));
        }

        [Test]
        public void SelectedInputScope_ChangedWhileMappingUnlocked_ReResolvesAssociatedProfile()
        {
            var firstProfile = _context.ProfilesManager.CreateProfile("First", null, null);
            _context.ProfilesManager.AddProfile(firstProfile);
            var secondProfile = _context.ProfilesManager.CreateProfile("Second", null, null);
            _context.ProfilesManager.AddProfile(secondProfile);

            var firstScope = MakeScope("scope-1");
            var secondScope = MakeScope("scope-2");
            _context.ScopeProfileAssociationService.SetAssociatedProfile(firstScope.Id, firstProfile.Guid);
            _context.ScopeProfileAssociationService.SetAssociatedProfile(secondScope.Id, secondProfile.Guid);

            _dashboard.SelectedInputScope = firstScope;
            _dashboard.BeginMappingCommand.Execute(null);
            Assert.That(_dashboard.SelectedProfileItem?.Profile.Guid, Is.EqualTo(firstProfile.Guid));

            _dashboard.SelectedInputScope = secondScope;

            Assert.That(_dashboard.SelectedProfileItem?.Profile.Guid, Is.EqualTo(secondProfile.Guid));
        }

        [Test]
        public void SelectedInputScope_ChangedBeforeMappingUnlocked_DoesNotAutoResolveProfile()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var scope = MakeScope();
            _context.ScopeProfileAssociationService.SetAssociatedProfile(scope.Id, profile.Guid);

            _dashboard.SelectedInputScope = scope;

            Assert.That(_dashboard.IsMappingUnlocked, Is.False);
            Assert.That(_dashboard.SelectedProfileItem, Is.Null);
        }
    }
}
