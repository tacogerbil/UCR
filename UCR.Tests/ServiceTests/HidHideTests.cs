using System;
using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Adapters;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Services;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ServiceTests
{
    internal sealed class FakeHidHideAdapter : IHidHideAdapter
    {
        public bool IsInstalled { get; set; } = true;
        public bool IsActive { get; set; }
        public List<string> Blocked { get; } = new List<string>();
        public List<string> Apps { get; } = new List<string>();
        public bool ThrowOnBlock { get; set; }

        public IReadOnlyList<string> BlockedInstanceIds => Blocked;
        public IReadOnlyList<string> ApplicationPaths => Apps;

        public void AddBlockedInstanceId(string instanceId)
        {
            if (ThrowOnBlock) throw new InvalidOperationException("driver busy");
            Blocked.Add(instanceId);
        }

        public void RemoveBlockedInstanceId(string instanceId) => Blocked.RemoveAll(id => string.Equals(id, instanceId, StringComparison.OrdinalIgnoreCase));
        public void AddApplicationPath(string path) => Apps.Add(path);
    }

    [TestFixture]
    public class HidHidePlannerTests
    {
        private const string Wheel = @"HID\VID_3416&PID_1021\7&A";
        private const string Pedals = @"HID\VID_3416&PID_1018\7&B";

        [Test]
        public void Plan_BlocksWantedDevicesNotYetBlocked()
        {
            var plan = HidHidePlanner.Plan(new[] { Wheel, Pedals }, new string[0], new string[0]);
            Assert.That(plan.ToBlock, Is.EquivalentTo(new[] { Wheel, Pedals }));
            Assert.That(plan.OwnedAfter, Is.EquivalentTo(new[] { Wheel, Pedals }));
        }

        [Test]
        public void Plan_IsIdempotent_WhenAlreadyOwnedAndBlocked()
        {
            var plan = HidHidePlanner.Plan(new[] { Wheel }, new[] { Wheel }, new[] { Wheel });
            Assert.That(plan.ToBlock, Is.Empty);
            Assert.That(plan.ToUnblock, Is.Empty);
            Assert.That(plan.OwnedAfter, Is.EquivalentTo(new[] { Wheel }));
        }

        [Test]
        public void Plan_ReleasesOnlyOwnedDevices()
        {
            var userHidden = @"HID\VID_AAAA&PID_BBBB\1";
            var plan = HidHidePlanner.Plan(new string[0], new[] { Wheel }, new[] { Wheel, userHidden });
            Assert.That(plan.ToUnblock, Is.EquivalentTo(new[] { Wheel }));
            Assert.That(plan.OwnedAfter, Is.Empty);
        }

        [Test]
        public void Plan_DoesNotClaimDeviceTheUserAlreadyHid()
        {
            var plan = HidHidePlanner.Plan(new[] { Wheel }, new string[0], new[] { Wheel });
            Assert.That(plan.ToBlock, Is.Empty);
            Assert.That(plan.OwnedAfter, Is.Empty, "A device blocked by the user must not be un-hidden when the profile stops.");
        }

        [Test]
        public void Plan_ComparesInstanceIdsCaseInsensitively()
        {
            var plan = HidHidePlanner.Plan(new[] { Wheel.ToLowerInvariant() }, new[] { Wheel }, new[] { Wheel });
            Assert.That(plan.ToBlock, Is.Empty);
            Assert.That(plan.ToUnblock, Is.Empty);
        }

        [TestCase(@"USB\VID_045E&PID_028E\1&2", true)]
        [TestCase(@"HID\VID_1234&PID_BEAD\1", true)]
        [TestCase(Wheel, false)]
        [TestCase(null, false)]
        public void IsProtected_FlagsUcrVirtualOutputDevices(string instanceId, bool expected)
        {
            Assert.That(HidHidePlanner.IsProtected(instanceId), Is.EqualTo(expected));
        }

        [Test]
        public void Plan_NeverBlocksProtectedDevices()
        {
            var plan = HidHidePlanner.Plan(new[] { @"USB\VID_045E&PID_028E\1&2", Wheel }, new string[0], new string[0]);
            Assert.That(plan.ToBlock, Is.EquivalentTo(new[] { Wheel }));
        }
    }

    [TestFixture]
    public class HidHideProfileServiceTests
    {
        private const string Wheel = @"HID\VID_3416&PID_1021\7&A";
        private const string SelfPath = @"C:\UCR\UCR.exe";

        private static Profile ProfileHiding(params string[] ids)
        {
            var profile = new Profile();
            profile.HiddenDevices = ids.Select(id => new HiddenDevice(id, id)).ToList();
            return profile;
        }

        [Test]
        public void Apply_BlocksDevices_WhitelistsUcr_AndTurnsCloakOn()
        {
            var adapter = new FakeHidHideAdapter();
            var service = new HidHideProfileService(adapter, SelfPath);

            var result = service.Apply(ProfileHiding(Wheel));

            Assert.That(result.Success, Is.True);
            Assert.That(adapter.Blocked, Is.EquivalentTo(new[] { Wheel }));
            Assert.That(adapter.Apps, Is.EquivalentTo(new[] { SelfPath }));
            Assert.That(adapter.IsActive, Is.True);
        }

        [Test]
        public void Apply_Twice_ChangesNothingTheSecondTime()
        {
            var adapter = new FakeHidHideAdapter();
            var service = new HidHideProfileService(adapter, SelfPath);
            var profile = ProfileHiding(Wheel);

            service.Apply(profile);
            service.Apply(profile);

            Assert.That(adapter.Blocked.Count, Is.EqualTo(1));
            Assert.That(adapter.Apps.Count, Is.EqualTo(1));
        }

        [Test]
        public void Apply_Null_ReleasesDevices_AndRestoresCloakState()
        {
            var adapter = new FakeHidHideAdapter { IsActive = false };
            var service = new HidHideProfileService(adapter, SelfPath);
            service.Apply(ProfileHiding(Wheel));

            service.Apply(null);

            Assert.That(adapter.Blocked, Is.Empty);
            Assert.That(adapter.IsActive, Is.False, "Cloak was off before UCR, so it must be off again.");
        }

        [Test]
        public void Apply_Null_LeavesCloakOn_WhenItWasAlreadyOn()
        {
            var adapter = new FakeHidHideAdapter { IsActive = true };
            var service = new HidHideProfileService(adapter, SelfPath);
            service.Apply(ProfileHiding(Wheel));

            service.Apply(null);

            Assert.That(adapter.IsActive, Is.True);
        }

        [Test]
        public void Apply_SwitchingProfiles_SwapsTheHiddenSet()
        {
            var adapter = new FakeHidHideAdapter();
            var service = new HidHideProfileService(adapter, SelfPath);
            var pedals = @"HID\VID_3416&PID_1018\7&B";

            service.Apply(ProfileHiding(Wheel));
            service.Apply(ProfileHiding(pedals));

            Assert.That(adapter.Blocked, Is.EquivalentTo(new[] { pedals }));
        }

        [Test]
        public void Apply_WhenHidHideMissing_FailsGracefullyWithoutTouchingDriver()
        {
            var adapter = new FakeHidHideAdapter { IsInstalled = false };
            var service = new HidHideProfileService(adapter, SelfPath);

            var result = service.Apply(ProfileHiding(Wheel));

            Assert.That(result.Success, Is.False);
            Assert.That(adapter.Blocked, Is.Empty);
        }

        [Test]
        public void Apply_WhenHidHideMissingAndNothingToHide_IsNotAnError()
        {
            var service = new HidHideProfileService(new FakeHidHideAdapter { IsInstalled = false }, SelfPath);
            Assert.That(service.Apply(ProfileHiding()).Success, Is.True);
        }

        [Test]
        public void Apply_WithoutOwnPath_RefusesToHide()
        {
            var adapter = new FakeHidHideAdapter();
            var service = new HidHideProfileService(adapter, null);

            var result = service.Apply(ProfileHiding(Wheel));

            Assert.That(result.Success, Is.False);
            Assert.That(adapter.Blocked, Is.Empty, "Hiding without whitelisting UCR would blind UCR itself.");
        }

        [Test]
        public void Apply_WhenDriverThrows_ReturnsFailureInsteadOfThrowing()
        {
            var service = new HidHideProfileService(new FakeHidHideAdapter { ThrowOnBlock = true }, SelfPath);
            HidHideApplyResult result = null;

            Assert.DoesNotThrow(() => result = service.Apply(ProfileHiding(Wheel)));
            Assert.That(result.Success, Is.False);
        }
    }

    [TestFixture]
    public class HiddenDeviceCatalogTests
    {
        private const string WheelPath = @"\\?\HID#VID_3416&PID_1021#7&A&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        private const string WheelId = @"HID\VID_3416&PID_1021\7&A&0&0000";

        private static Device DeviceAt(string title, string hidPath)
        {
            return new Device(new DeviceCache
            {
                Title = title,
                ProviderName = "SharpDX_DirectInput",
                DeviceHandle = title,
                HidPath = hidPath,
                DeviceBindingMenu = new List<DeviceBindingNode>()
            });
        }

        [Test]
        public void Build_ListsConnectedDevices_AndMarksSavedOnesSelected()
        {
            var rows = HiddenDeviceCatalog.Build(new[] { DeviceAt("Wheel", WheelPath) }, d => d.Title,
                new[] { new HiddenDevice(WheelId, "Wheel") });

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].IsSelected, Is.True);
            Assert.That(rows[0].IsConnected, Is.True);
        }

        [Test]
        public void Build_DeduplicatesDevicesSharingAnInstanceId()
        {
            var rows = HiddenDeviceCatalog.Build(new[] { DeviceAt("Wheel", WheelPath), DeviceAt("Wheel (again)", WheelPath) },
                d => d.Title, null);
            Assert.That(rows.Count, Is.EqualTo(1));
        }

        [Test]
        public void Build_KeepsSavedDevicesThatAreNotConnected()
        {
            var rows = HiddenDeviceCatalog.Build(new Device[0], d => d.Title, new[] { new HiddenDevice(WheelId, "Wheel") });

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].IsConnected, Is.False);
            Assert.That(rows[0].IsSelected, Is.True);
        }

        [Test]
        public void Build_SkipsVJoy_WhoseHidPathHasNoVidPid()
        {
            var vjoy = new Device(new DeviceCache
            {
                Title = "vJoy Device",
                ProviderName = "SharpDX_DirectInput",
                DeviceHandle = "VID_1234&PID_BEAD",
                HidPath = @"\\?\hid#hidclass#1&4784345&6&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
                DeviceBindingMenu = new List<DeviceBindingNode>()
            });

            Assert.That(HiddenDeviceCatalog.Build(new[] { vjoy }, d => d.Title, null), Is.Empty);
        }

        [Test]
        public void Build_DropsSavedProtectedDevices()
        {
            var rows = HiddenDeviceCatalog.Build(new Device[0], d => d.Title,
                new[] { new HiddenDevice(@"HID\HIDCLASS\1&4784345&6&0000", "vJoy Device") });
            Assert.That(rows, Is.Empty);
        }

        [Test]
        public void Build_SkipsDevicesWithoutHidPath_AndProtectedVirtualDevices()
        {
            var virtualPad = @"\\?\HID#VID_045E&PID_028E#7&C&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
            var rows = HiddenDeviceCatalog.Build(new[] { DeviceAt("No path", null), DeviceAt("ViGEm pad", virtualPad) },
                d => d.Title, null);
            Assert.That(rows, Is.Empty);
        }
    }
}
