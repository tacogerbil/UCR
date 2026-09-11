using System;
using System.Collections.Generic;
using System.IO;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Managers;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Persistence;
using HidWizards.UCR.ViewModels.Devices;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    [TestFixture]
    internal class DeviceRemovalTests
    {
        private Context _context;
        private Profile _profile;
        private Mapping _mapping;
        private DeviceConfiguration _deviceConfiguration;
        private DeviceGroup _deviceGroup;
        private string _deviceId;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            // Suppress IO for tests
            _context.IOController?.Dispose();
            _context.IOController = null;

            _profile = new Profile(_context) { Title = "Test Profile" };
            _context.Profiles.Add(_profile);

            _mapping = _profile.AddMapping("Test Mapping");

            var device = new Device("Test Keyboard", "Core_Interception", @"Keyboard\HID\VID_1234&PID_5678", 0);
            _deviceId = DeviceIdentity.BuildLogicalKey(device);
            _deviceConfiguration = new DeviceConfiguration(device);
            
            _profile.InputDeviceConfigurations.Add(_deviceConfiguration);

            var plugin = new HidWizards.UCR.Plugins.Remapper.ButtonToButton();
            _profile.AddPlugin(_mapping, plugin);

            var binding = _mapping.DeviceBindings[0];
            binding.SetDeviceConfigurationGuid(_deviceConfiguration.Guid);
            binding.IsBound = true;
            binding.KeyType = 1;
            binding.KeyValue = 2;

            _context.DeviceGroupService.CreateGroup("Test Group", new List<string> { _deviceId });
            _deviceGroup = _context.DeviceGroups[0];
        }

        [Test]
        public void RemoveMemberWithRetainOptionDoesNotClearBindings()
        {
            var viewModel = new DeviceGroupViewModel(_deviceGroup, _context);

            Assert.That(_mapping.DeviceBindings[0].IsBound, Is.True);
            Assert.That(_mapping.DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(_deviceConfiguration.Guid));

            // Retain -> deleteMappings = false
            viewModel.RemoveMember(_deviceId, deleteMappings: false);

            // Group member should be removed
            Assert.That(_deviceGroup.MemberDeviceIdentities.Contains(_deviceId), Is.False);

            // Mapping should be retained
            Assert.That(_mapping.DeviceBindings[0].IsBound, Is.True, "Binding should remain bound when Retain is selected.");
            Assert.That(_mapping.DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(_deviceConfiguration.Guid));
            Assert.That(_mapping.DeviceBindings[0].KeyValue, Is.EqualTo(2));
        }

        [Test]
        public void RemoveMemberWithDeleteOptionClearsBindings()
        {
            var viewModel = new DeviceGroupViewModel(_deviceGroup, _context);

            Assert.That(_mapping.DeviceBindings[0].IsBound, Is.True);

            // Delete -> deleteMappings = true
            viewModel.RemoveMember(_deviceId, deleteMappings: true);

            // Group member should be removed
            Assert.That(_deviceGroup.MemberDeviceIdentities.Contains(_deviceId), Is.False);

            // Mapping should be cleared
            Assert.That(_mapping.DeviceBindings[0].IsBound, Is.False, "Binding should be cleared when Delete is selected.");
            Assert.That(_mapping.DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(Guid.Empty));
            Assert.That(_mapping.DeviceBindings[0].KeyValue, Is.EqualTo(0));
        }
    }
}
