using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using HidWizards.UCR.Core;
using HidWizards.UCR.ViewModels.Devices;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // DevicesViewModel's change-notification handlers (OnDeviceGroupsChanged etc.) marshal through
    // Application.Current.Dispatcher.Invoke -- real production code, needed because those events can
    // fire from a background thread (device hotplug). STA + a live Application (same pattern as
    // UiTests/DeviceManagerPageSmokeTests.cs) is what that requires under NUnit; a same-thread
    // Dispatcher.Invoke runs its callback inline without needing an actual running message loop.
    [TestFixture]
    [NonParallelizable]
    [Apartment(ApartmentState.STA)]
    internal class DevicesViewModelTests
    {
        private Context _context;
        private DevicesViewModel _devicesViewModel;

        [SetUp]
        public void Setup()
        {
            EnsureApplicationResources();
            _context = new Context();
            _devicesViewModel = new DevicesViewModel(_context);
        }

        private static void EnsureApplicationResources()
        {
            if (Application.Current != null) return;
            var app = new HidWizards.UCR.App();
            app.InitializeComponent();
        }

        [TearDown]
        public void TearDown()
        {
            _devicesViewModel.Dispose();
        }

        private DeviceGroupViewModel CreateGroupViewModel(string title, List<string> members = null)
        {
            _context.DeviceGroupService.CreateGroup(title, members ?? new List<string>());
            return _devicesViewModel.DeviceGroups.Single(g => g.Name == title);
        }

        [Test]
        public void SelectingGroupForMapping_SetsCurrentScopeToThatGroup()
        {
            var group = CreateGroupViewModel("Racing Rig");

            group.IsSelectedForMapping = true;

            Assert.That(_devicesViewModel.HasSelectedScope, Is.True);
            Assert.That(_devicesViewModel.CurrentScope.Id, Is.EqualTo(group.Guid.ToString()));
            Assert.That(_devicesViewModel.CurrentScope.IsGroup, Is.True);
        }

        [Test]
        public void DeselectingGroupForMapping_ClearsCurrentScope()
        {
            var group = CreateGroupViewModel("Racing Rig");
            group.IsSelectedForMapping = true;

            group.IsSelectedForMapping = false;

            Assert.That(_devicesViewModel.HasSelectedScope, Is.False);
        }

        [Test]
        public void SelectingOneGroup_DeselectsAnyOtherPreviouslySelectedGroup()
        {
            // Each CreateGroupViewModel call fires DeviceGroupsChanged, which rebuilds every
            // DeviceGroupViewModel in DeviceGroups from scratch (see PopulateGroups) -- so both groups
            // must exist before fetching the live instances this test actually acts on, or the first
            // one fetched goes stale the moment the second is created.
            CreateGroupViewModel("Racing Rig");
            CreateGroupViewModel("Flight Sim");
            var first = _devicesViewModel.DeviceGroups.Single(g => g.Name == "Racing Rig");
            var second = _devicesViewModel.DeviceGroups.Single(g => g.Name == "Flight Sim");

            first.IsSelectedForMapping = true;
            second.IsSelectedForMapping = true;

            Assert.That(first.IsSelectedForMapping, Is.False);
            Assert.That(second.IsSelectedForMapping, Is.True);
            Assert.That(_devicesViewModel.CurrentScope.Id, Is.EqualTo(second.Guid.ToString()));
        }

        [Test]
        public void HasAssociatedProfile_ReflectsScopeProfileAssociationService()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var group = CreateGroupViewModel("Racing Rig");

            group.IsSelectedForMapping = true;
            Assert.That(_devicesViewModel.HasAssociatedProfile, Is.False, "no association set yet");

            _context.ScopeProfileAssociationService.SetAssociatedProfile(group.Guid.ToString(), profile.Guid);

            Assert.That(_devicesViewModel.HasAssociatedProfile, Is.True);
            Assert.That(_devicesViewModel.AssociatedProfileName, Is.EqualTo("Racing Profile"));
        }

        [Test]
        public void ChooseProfileCommand_InvokesCallback_WhichSetsTheAssociation()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var group = CreateGroupViewModel("Racing Rig");
            group.IsSelectedForMapping = true;

            _devicesViewModel.RequestChooseProfile = callback => callback(profile.Guid);
            _devicesViewModel.ChooseProfileCommand.Execute(null);

            Assert.That(_context.ScopeProfileAssociationService.GetAssociatedProfile(group.Guid.ToString()), Is.EqualTo(profile.Guid));
        }

        [Test]
        public void EditProfileCommand_RequestsEditForTheAssociatedProfile()
        {
            var profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            var group = CreateGroupViewModel("Racing Rig");
            group.IsSelectedForMapping = true;
            _context.ScopeProfileAssociationService.SetAssociatedProfile(group.Guid.ToString(), profile.Guid);

            Core.Models.Profile requestedProfile = null;
            _devicesViewModel.RequestEditProfile = p => requestedProfile = p;
            _devicesViewModel.EditProfileCommand.Execute(null);

            Assert.That(requestedProfile?.Guid, Is.EqualTo(profile.Guid));
        }

        // Regression test for the RequestRemovalOption static-state bug found in the 2026-09-22 MCCC
        // audit: DeviceGroupMemberViewModel used to read a `static` callback, set by the View's
        // DataContextChanged handler -- but that handler only runs *after* DevicesViewModel's
        // constructor (and its first member-construction pass) has already completed, so capturing the
        // delegate at construction time would have observed null. This asserts the callback set on the
        // ViewModel *after* construction is still picked up correctly.
        [Test]
        public void RemoveMemberCommand_UsesRequestRemovalOptionEvenWhenSetAfterConstruction()
        {
            var group = CreateGroupViewModel("Racing Rig", new List<string> { "device-1" });
            var member = group.Members.Single();

            var wasInvoked = false;
            // Deliberately assigned after DevicesViewModel and its groups/members already exist,
            // mirroring DevicesView.xaml.cs's real DataContextChanged timing.
            _devicesViewModel.RequestRemovalOption = callback =>
            {
                wasInvoked = true;
                callback(false);
            };

            member.RemoveMemberCommand.Execute(null);

            Assert.That(wasInvoked, Is.True);
            // Removal fires DeviceGroupsChanged, which rebuilds DeviceGroups (and every member) from
            // scratch -- re-fetch rather than asserting on the pre-removal `group`/`member` references.
            var refreshedGroup = _devicesViewModel.DeviceGroups.Single(g => g.Name == "Racing Rig");
            Assert.That(refreshedGroup.Members, Is.Empty);
        }

        [Test]
        public void CanAddToExistingGroup_FalseWhenNoGroupsExist()
        {
            Assert.That(_devicesViewModel.CanAddToExistingGroup, Is.False);
        }

        [Test]
        public void RemovingGroup_ClearsScopeIfItWasSelected()
        {
            var group = CreateGroupViewModel("Racing Rig");
            group.IsSelectedForMapping = true;
            Assert.That(_devicesViewModel.HasSelectedScope, Is.True);

            _context.DeviceGroupService.RemoveGroup(group.Guid);

            Assert.That(_devicesViewModel.DeviceGroups, Is.Empty);
            Assert.That(_devicesViewModel.HasSelectedScope, Is.False);
        }
    }
}
