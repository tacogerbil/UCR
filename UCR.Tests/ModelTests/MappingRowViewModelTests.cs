using System;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Services;
using HidWizards.UCR.Plugins.Remapper;
using HidWizards.UCR.ViewModels.Mapping;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // Exercises MappingRowViewModel's plugin-creation / merge-swap / output-auto-resolution logic
    // directly via the internal (resolvePluginTemplate) constructor. PluginManager.Plugins is always
    // empty in UCR.Tests (nothing copies UCR.Plugins.dll into a "Plugins" folder next to the test
    // runner's output the way UCR.Plugins.csproj's post-build step does for the real app), so these
    // tests supply their own plugin instances instead of relying on MEF discovery.
    [TestFixture]
    internal class MappingRowViewModelTests
    {
        private Context _context;
        private Profile _profile;
        private Guid _outputDeviceGuid;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            var profile = _context.ProfilesManager.CreateProfile("Base Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            _profile = _context.Profiles[0];
            _outputDeviceGuid = Guid.NewGuid();
        }

        private static Plugin ResolveTemplate(string name)
        {
            switch (name)
            {
                case "Axis to Axis": return new AxisToAxis();
                case "Button to Button": return new ButtonToButton();
                case "Axis Merger": return new AxisMerger();
                case "Button Merger": return new ButtonMerger();
                default: return null;
            }
        }

        private MappingRowViewModel CreateRow(OutputSlot slot)
        {
            var mapping = new Mapping(_profile, slot.Title) { TargetOutputKey = slot.SlotKey };
            _profile.Mappings.Add(mapping);
            return new MappingRowViewModel(_context, mapping, slot, _outputDeviceGuid, ResolveTemplate);
        }

        private static OutputSlot AxisSlot => new OutputSlot("Left Stick, X", 1, 0, 0, DeviceBindingCategory.Range);
        private static OutputSlot ButtonSlot => new OutputSlot("Button A", 2, 0, 0, DeviceBindingCategory.Momentary);

        [Test]
        public void FirstBinding_OnAxisRow_CreatesAxisToAxisAndResolvesOutput()
        {
            var slot = AxisSlot;
            var row = CreateRow(slot);

            var binding = row.GetOrCreateNextBinding();

            Assert.That(binding, Is.Not.Null);
            Assert.That(_profile.Mappings[0].Plugins.Count, Is.EqualTo(1));
            Assert.That(_profile.Mappings[0].Plugins[0], Is.InstanceOf<AxisToAxis>());

            var output = _profile.Mappings[0].Plugins[0].Outputs[0];
            Assert.That(output.DeviceConfigurationGuid, Is.EqualTo(_outputDeviceGuid));
            Assert.That(output.KeyType, Is.EqualTo(slot.KeyType));
            Assert.That(output.KeyValue, Is.EqualTo(slot.KeyValue));
            Assert.That(output.KeySubValue, Is.EqualTo(slot.KeySubValue));
            Assert.That(output.IsBound, Is.True);
        }

        [Test]
        public void FirstBinding_OnButtonRow_CreatesButtonToButtonAndResolvesOutput()
        {
            var slot = ButtonSlot;
            var row = CreateRow(slot);

            row.GetOrCreateNextBinding();

            Assert.That(_profile.Mappings[0].Plugins[0], Is.InstanceOf<ButtonToButton>());
            var output = _profile.Mappings[0].Plugins[0].Outputs[0];
            Assert.That(output.KeyType, Is.EqualTo(slot.KeyType));
            Assert.That(output.IsBound, Is.True);
        }

        [Test]
        public void SecondInput_OnAxisRow_SwapsToAxisMerger_AndPreservesFirstInputBinding()
        {
            var row = CreateRow(AxisSlot);
            var mapping = _profile.Mappings[0];

            var firstBinding = row.GetOrCreateNextBinding();
            var firstInputGuid = Guid.NewGuid();
            firstBinding.SetDeviceConfigurationGuid(firstInputGuid);
            firstBinding.SetKeyTypeValue(9, 1, 0);

            var secondBinding = row.GetOrCreateNextBinding();

            Assert.That(mapping.Plugins.Count, Is.EqualTo(1));
            Assert.That(mapping.Plugins[0], Is.InstanceOf<AxisMerger>());
            Assert.That(row.IsMerged, Is.True);

            Assert.That(mapping.DeviceBindings.Count, Is.EqualTo(2));
            Assert.That(mapping.DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(firstInputGuid));
            Assert.That(mapping.DeviceBindings[0].KeyType, Is.EqualTo(9));
            Assert.That(mapping.DeviceBindings[0].IsBound, Is.True);

            Assert.That(secondBinding, Is.SameAs(mapping.DeviceBindings[1]));
            Assert.That(secondBinding.IsBound, Is.False);

            // The Output binding is re-resolved against the same slot after the plugin swap, so it
            // should end up identical to what the 1:1 plugin had before swapping.
            var output = mapping.Plugins[0].Outputs[0];
            Assert.That(output.DeviceConfigurationGuid, Is.EqualTo(_outputDeviceGuid));
            Assert.That(output.KeyType, Is.EqualTo(AxisSlot.KeyType));
        }

        [Test]
        public void SecondInput_OnButtonRow_SwapsToButtonMerger()
        {
            var row = CreateRow(ButtonSlot);
            var mapping = _profile.Mappings[0];

            var firstBinding = row.GetOrCreateNextBinding();
            firstBinding.SetDeviceConfigurationGuid(Guid.NewGuid());
            firstBinding.SetKeyTypeValue(3, 0, 0);

            row.GetOrCreateNextBinding();

            Assert.That(mapping.Plugins[0], Is.InstanceOf<ButtonMerger>());
            Assert.That(mapping.DeviceBindings.Count, Is.EqualTo(2));
            Assert.That(row.IsMerged, Is.True);
        }

        [Test]
        public void GetOrCreateNextBinding_OnceMerged_NeverGrowsBeyondTwoBindings()
        {
            // GetOrCreateNextBinding has no explicit "already full" guard of its own — that guard is
            // in ExecuteListen/ExecuteClear/the manual picker's command (they check IsMerged and
            // redirect to the Advanced panel before ever calling this method). Calling this method
            // directly, as these tests do, bypasses that UI-level guard, so this test documents the
            // underlying structural guarantee instead: a merge plugin only ever has as many
            // DeviceBindings as its own InputCategories count (2 for AxisMerger/ButtonMerger), so
            // repeated calls can only toggle between the two existing bindings, never add a third.
            var row = CreateRow(AxisSlot);
            var mapping = _profile.Mappings[0];

            var first = row.GetOrCreateNextBinding();
            first.SetDeviceConfigurationGuid(Guid.NewGuid());
            first.SetKeyTypeValue(1, 0, 0);

            var second = row.GetOrCreateNextBinding();
            second.SetDeviceConfigurationGuid(Guid.NewGuid());
            second.SetKeyTypeValue(1, 1, 0);

            row.GetOrCreateNextBinding();

            Assert.That(mapping.DeviceBindings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Unmerge_OnMergedAxisRow_RestoresFirstInputAsPlainAxisToAxis()
        {
            var row = CreateRow(AxisSlot);
            var mapping = _profile.Mappings[0];

            var firstBinding = row.GetOrCreateNextBinding();
            var firstInputGuid = Guid.NewGuid();
            firstBinding.SetDeviceConfigurationGuid(firstInputGuid);
            firstBinding.SetKeyTypeValue(9, 1, 0);

            row.GetOrCreateNextBinding(); // triggers the merge swap
            Assert.That(row.IsMerged, Is.True);

            row.UnmergeCommand.Execute(null);

            Assert.That(row.IsMerged, Is.False);
            Assert.That(mapping.Plugins.Count, Is.EqualTo(1));
            Assert.That(mapping.Plugins[0], Is.InstanceOf<AxisToAxis>());

            Assert.That(mapping.DeviceBindings.Count, Is.EqualTo(1));
            Assert.That(mapping.DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(firstInputGuid));
            Assert.That(mapping.DeviceBindings[0].KeyType, Is.EqualTo(9));
            Assert.That(mapping.DeviceBindings[0].IsBound, Is.True);

            var output = mapping.Plugins[0].Outputs[0];
            Assert.That(output.DeviceConfigurationGuid, Is.EqualTo(_outputDeviceGuid));
            Assert.That(output.KeyType, Is.EqualTo(AxisSlot.KeyType));
            Assert.That(output.IsBound, Is.True);
        }

        [Test]
        public void Unmerge_WhenNotMerged_DoesNothing()
        {
            var row = CreateRow(AxisSlot);
            var mapping = _profile.Mappings[0];
            row.GetOrCreateNextBinding();

            row.UnmergeCommand.Execute(null);

            Assert.That(mapping.Plugins[0], Is.InstanceOf<AxisToAxis>());
            Assert.That(mapping.DeviceBindings.Count, Is.EqualTo(1));
        }
    }
}
