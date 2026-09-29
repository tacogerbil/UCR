using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.ViewModels;
using HidWizards.UCR.ViewModels.Dialogs;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // Covers the pure decision logic behind MainWindowViewModel.EditProfile's dialog results
    // (Clone/Remove/AddChild/Save-Overwrite). The Handle* methods that show DialogHost/file-picker
    // dialogs themselves are deliberately not exercised here -- they need a live window and can't run
    // headless -- each just hands its actual answer to the internal Execute* method under test, which
    // is where the logic worth regression-testing actually lives.
    [TestFixture]
    internal class MainWindowViewModelTests
    {
        private Context _context;
        private MainWindowViewModel _mainWindowViewModel;
        private Profile _profile;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            _mainWindowViewModel = new MainWindowViewModel(_context);
            _profile = _context.ProfilesManager.CreateProfile("Racing Profile", null, null);
            _context.ProfilesManager.AddProfile(_profile);
            _mainWindowViewModel.ReloadProfileTree();
        }

        [Test]
        public void ExecuteRemove_RemovesProfileFromContext()
        {
            _mainWindowViewModel.ExecuteRemove(_profile);

            Assert.That(_context.Profiles, Does.Not.Contain(_profile));
        }

        [Test]
        public void ExecuteRemove_RefreshesProfileList()
        {
            _mainWindowViewModel.ExecuteRemove(_profile);

            Assert.That(_mainWindowViewModel.Dashboard.ProfileList.Any(p => p.Id == _profile.Guid), Is.False);
        }

        [Test]
        public void ExecuteClone_BlankName_DoesNothingAndReturnsNull()
        {
            var result = _mainWindowViewModel.ExecuteClone(_profile, "  ", new List<ProfileApplicationRule>());

            Assert.That(result, Is.Null);
            Assert.That(_context.Profiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void ExecuteClone_ValidName_CreatesSeparateProfileWithGivenTitle()
        {
            var result = _mainWindowViewModel.ExecuteClone(_profile, "Racing Profile Clone", new List<ProfileApplicationRule>());

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Guid, Is.Not.EqualTo(_profile.Guid));
            Assert.That(result.Title, Is.EqualTo("Racing Profile Clone"));
            Assert.That(_context.Profiles, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExecuteClone_AppliesTheGivenAutoActivateRules_ReplacingAnyExisting()
        {
            _profile.AutoActivateApplications.Add(new ProfileApplicationRule(@"C:\old.exe"));
            var newRules = new List<ProfileApplicationRule> { new ProfileApplicationRule(@"C:\new.exe") };

            var result = _mainWindowViewModel.ExecuteClone(_profile, "Racing Profile Clone", newRules);

            Assert.That(result.AutoActivateApplications.Select(r => r.Executable), Is.EquivalentTo(new[] { @"C:\new.exe" }));
        }

        [Test]
        public void ExecuteClone_KeepingAnExistingRuleUnedited_DoesNotShareItWithTheSourceProfile()
        {
            // Matches the real dialog flow: opening ProfileEditDialogViewModel copies the source
            // profile's existing rules into its own ObservableCollection by reference (see its
            // constructor), then the user adds one more rule for the new game without touching the
            // old one -- that untouched rule instance is what gets handed to ExecuteClone here.
            var existingRule = new ProfileApplicationRule(@"C:\shared-game.exe");
            _profile.AutoActivateApplications.Add(existingRule);
            var dialogRules = new List<ProfileApplicationRule>
            {
                existingRule,
                new ProfileApplicationRule(@"C:\new-game.exe")
            };

            var clone = _mainWindowViewModel.ExecuteClone(_profile, "Racing Profile Clone", dialogRules);

            var clonedRule = clone.AutoActivateApplications.Single(r => r.Executable == @"C:\shared-game.exe");
            Assert.That(clonedRule, Is.Not.SameAs(existingRule),
                "the clone must own an independent copy, not the same mutable rule the source profile still references");

            clonedRule.Executable = @"C:\edited-on-clone.exe";
            Assert.That(_profile.AutoActivateApplications.Single().Executable, Is.EqualTo(@"C:\shared-game.exe"),
                "editing the clone's rule must not bleed back into the source profile's own rule");
        }

        [Test]
        public void ExecuteClone_RefreshesProfileList()
        {
            var result = _mainWindowViewModel.ExecuteClone(_profile, "Racing Profile Clone", new List<ProfileApplicationRule>());

            Assert.That(_mainWindowViewModel.Dashboard.ProfileList.Any(p => p.Id == result.Guid), Is.True);
        }

        [Test]
        public void ExecuteAddChild_AddsChildUnderTheGivenParent()
        {
            var child = _mainWindowViewModel.ExecuteAddChild(_profile);

            Assert.That(_profile.ChildProfiles, Has.Count.EqualTo(1));
            Assert.That(_profile.ChildProfiles[0].Guid, Is.EqualTo(child.Guid));
        }

        [Test]
        public void ExecuteAddChild_RefreshesProfileList()
        {
            var child = _mainWindowViewModel.ExecuteAddChild(_profile);

            var parentItem = _mainWindowViewModel.Dashboard.ProfileList.Single(p => p.Id == _profile.Guid);
            Assert.That(parentItem.Items.Any(i => i.Id == child.Guid), Is.True);
        }

        [Test]
        public void ExecuteSaveOverwrite_PersistsNameChangeOntoTheSameProfile()
        {
            var vm = new ProfileEditDialogViewModel(_profile) { ProfileName = "Renamed" };

            _mainWindowViewModel.ExecuteSaveOverwrite(vm);

            Assert.That(_profile.Title, Is.EqualTo("Renamed"));
            Assert.That(_context.Profiles, Has.Count.EqualTo(1), "overwrite must not create a second profile");
            vm.Dispose();
        }

        [Test]
        public void ApplyAutoActivateRules_ClearsExistingAndAddsGivenRules()
        {
            _profile.AutoActivateApplications.Add(new ProfileApplicationRule(@"C:\old.exe"));

            MainWindowViewModel.ApplyAutoActivateRules(_profile, new[] { new ProfileApplicationRule(@"C:\new.exe") });

            Assert.That(_profile.AutoActivateApplications.Select(r => r.Executable), Is.EquivalentTo(new[] { @"C:\new.exe" }));
        }

        [Test]
        public void ApplyAutoActivateRules_NullProfile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MainWindowViewModel.ApplyAutoActivateRules(null, new List<ProfileApplicationRule>()));
        }
    }
}
