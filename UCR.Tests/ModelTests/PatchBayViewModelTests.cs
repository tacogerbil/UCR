using System.Collections.Generic;
using System.Linq;
using HidWizards.IOWrapper.DataTransferObjects;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Plugins.Filter;
using HidWizards.UCR.ViewModels.Mapping;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // Covers the core fix from the 2026-09-19 audit: Patch Bay rows come from the selected Output
    // Device's own binding tree (OutputSlotResolver) instead of a hardcoded Xbox 360 key table, and
    // switching devices must never delete a Mapping that was configured for a different device.
    [TestFixture]
    internal class PatchBayViewModelTests
    {
        private Context _context;
        private Profile _profile;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            var profile = _context.ProfilesManager.CreateProfile("Base Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            _profile = _context.Profiles[0];
        }

        private static Device CreateOutputDevice(string providerName, string deviceHandle, List<DeviceBindingNode> menu)
        {
            var report = new DeviceReport
            {
                DeviceName = deviceHandle,
                DeviceDescriptor = new DeviceDescriptor { DeviceHandle = deviceHandle, DeviceInstance = 0 }
            };
            var provider = new ProviderReport
            {
                ProviderDescriptor = new ProviderDescriptor { ProviderName = providerName }
            };
            return new Device(report, provider, menu);
        }

        // One Range (axis-like) slot and one Momentary (button-like) slot, with KeyType values offset
        // so two menus built with different offsets never collide on SlotKey.
        private static List<DeviceBindingNode> TwoSlotMenu(int keyTypeOffset) => new List<DeviceBindingNode>
        {
            new DeviceBindingNode
            {
                Title = "Axis",
                DeviceBindingInfo = new DeviceBindingInfo
                    { KeyType = keyTypeOffset + 1, KeyValue = 0, DeviceBindingCategory = DeviceBindingCategory.Range }
            },
            new DeviceBindingNode
            {
                Title = "Button",
                DeviceBindingInfo = new DeviceBindingInfo
                    { KeyType = keyTypeOffset + 2, KeyValue = 0, DeviceBindingCategory = DeviceBindingCategory.Momentary }
            }
        };

        [Test]
        public void SelectingOutputDevice_GeneratesOneRowPerSlot_AndOneMappingEach()
        {
            var deviceConfig = new DeviceConfiguration(CreateOutputDevice("Core_ViGEm", "xb360", TwoSlotMenu(0)));

            var patchBay = new PatchBayViewModel(_context);
            patchBay.SetProfile(_profile);
            patchBay.SelectedOutputDeviceConfiguration = deviceConfig;

            Assert.That(patchBay.Rows.Count, Is.EqualTo(2));
            Assert.That(_profile.Mappings.Count, Is.EqualTo(2));
            Assert.That(patchBay.Rows.Select(r => r.TargetOutputKey),
                Is.EquivalentTo(_profile.Mappings.Select(m => m.TargetOutputKey)));
            Assert.That(patchBay.Rows.Any(r => r.IsAxis), Is.True);
            Assert.That(patchBay.Rows.Any(r => !r.IsAxis), Is.True);
        }

        [Test]
        public void SwitchingOutputDevice_DoesNotDeleteMappingsFromThePreviousDevice()
        {
            var configA = new DeviceConfiguration(CreateOutputDevice("Core_ViGEm", "xb360", TwoSlotMenu(0)));
            var configB = new DeviceConfiguration(CreateOutputDevice("Core_ViGEm", "ds4", TwoSlotMenu(100)));

            var patchBay = new PatchBayViewModel(_context);
            patchBay.SetProfile(_profile);

            patchBay.SelectedOutputDeviceConfiguration = configA;
            Assert.That(patchBay.Rows.Count, Is.EqualTo(2));

            patchBay.SelectedOutputDeviceConfiguration = configB;
            Assert.That(patchBay.Rows.Count, Is.EqualTo(2));
            // Device A's two Mappings are still in the profile, just not rendered as rows anymore.
            Assert.That(_profile.Mappings.Count, Is.EqualTo(4));

            patchBay.SelectedOutputDeviceConfiguration = configA;
            Assert.That(patchBay.Rows.Count, Is.EqualTo(2));
            // Switching back re-matches the original two Mappings by SlotKey instead of creating new ones.
            Assert.That(_profile.Mappings.Count, Is.EqualTo(4));
        }

        [Test]
        public void NoOutputDeviceSelected_ProducesNoRows()
        {
            var patchBay = new PatchBayViewModel(_context);
            patchBay.SetProfile(_profile);

            Assert.That(patchBay.Rows.Count, Is.EqualTo(0));
        }

        private static Plugin ResolveFilterTemplate(string name)
        {
            switch (name)
            {
                case "Button to Filter": return new ButtonToFilter();
                case "Axis to Filter": return new AxisToFilter();
                default: return null;
            }
        }

        [Test]
        public void AddButtonFilterCommand_CreatesAFilterProducer_NotAPatchBayRow()
        {
            var patchBay = new PatchBayViewModel(_context, ResolveFilterTemplate);
            patchBay.SetProfile(_profile);

            patchBay.AddButtonFilterCommand.Execute(null);

            Assert.That(patchBay.FilterProducers.Count, Is.EqualTo(1));
            Assert.That(patchBay.Rows.Count, Is.EqualTo(0), "a filter producer must never render as an output-slot row");
            Assert.That(_profile.Mappings, Has.Count.EqualTo(1));
            Assert.That(_profile.Mappings[0].TargetOutputKey, Is.Null.Or.Empty);
        }

        [Test]
        public void AddAxisFilterCommand_CreatesAFilterProducer()
        {
            var patchBay = new PatchBayViewModel(_context, ResolveFilterTemplate);
            patchBay.SetProfile(_profile);

            patchBay.AddAxisFilterCommand.Execute(null);

            Assert.That(patchBay.FilterProducers.Count, Is.EqualTo(1));
            Assert.That(patchBay.FilterProducers[0].PluginSummary.Plugin, Is.InstanceOf<AxisToFilter>());
        }

        [Test]
        public void RemoveCommand_OnAFilterProducer_RemovesItFromProfileAndFromTheList()
        {
            var patchBay = new PatchBayViewModel(_context, ResolveFilterTemplate);
            patchBay.SetProfile(_profile);
            patchBay.AddButtonFilterCommand.Execute(null);
            var producer = patchBay.FilterProducers.Single();

            producer.RemoveCommand.Execute(null);

            Assert.That(patchBay.FilterProducers, Is.Empty);
            Assert.That(_profile.Mappings, Is.Empty);
        }

        [Test]
        public void SetProfile_PopulatesFilterProducers_FromMappingsAlreadySavedOnTheProfile()
        {
            var mapping = new Mapping(_profile, "Button to Filter");
            _profile.Mappings.Add(mapping);
            mapping.AddPlugin(new ButtonToFilter { FilterName = "Aim Mode" });

            var patchBay = new PatchBayViewModel(_context, ResolveFilterTemplate);
            patchBay.SetProfile(_profile);

            Assert.That(patchBay.FilterProducers.Count, Is.EqualTo(1));
        }

        [Test]
        public void SetProfile_DoesNotTreatAPatchBayRowMappingAsAFilterProducer()
        {
            var deviceConfig = new DeviceConfiguration(CreateOutputDevice("Core_ViGEm", "xb360", TwoSlotMenu(0)));
            var patchBay = new PatchBayViewModel(_context, ResolveFilterTemplate);
            patchBay.SetProfile(_profile);
            patchBay.SelectedOutputDeviceConfiguration = deviceConfig;

            Assert.That(patchBay.Rows.Count, Is.EqualTo(2));
            Assert.That(patchBay.FilterProducers, Is.Empty);
        }
    }
}
