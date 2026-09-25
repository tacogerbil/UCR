using System.Collections.Generic;
using System.Linq;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Plugins.Remapper;
using HidWizards.UCR.ViewModels.Mapping;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    // Covers the "reintroduce Filters" work (2026-09-22): PluginSummaryViewModel.FilterTags mirrors
    // Plugin.Filters (the "only run this plugin when filter X is [Active/Inactive]" conditions), and
    // stays in sync with it. AddFilterCommand itself isn't covered here -- it shows a DialogHost dialog
    // and can't run headless -- see the vault docs for why that boundary is drawn this way elsewhere
    // in this codebase.
    [TestFixture]
    internal class PluginSummaryViewModelTests
    {
        private static PluginSummaryViewModel CreateSummary(AxisToAxis plugin)
        {
            return new PluginSummaryViewModel(plugin, new List<DeviceBinding>());
        }

        [Test]
        public void FilterTags_PopulatedFromExistingPluginFilters()
        {
            var plugin = new AxisToAxis();
            plugin.AddFilter("Aim Mode");

            var summary = CreateSummary(plugin);

            Assert.That(summary.FilterTags, Has.Count.EqualTo(1));
            Assert.That(summary.FilterTags[0].Name, Is.EqualTo("Aim Mode"));
            Assert.That(summary.FilterTags[0].Negative, Is.False);
        }

        [Test]
        public void NoFilters_ProducesAnEmptyFilterTagsCollection()
        {
            var summary = CreateSummary(new AxisToAxis());

            Assert.That(summary.FilterTags, Is.Empty);
        }

        [Test]
        public void FilterTagRemoveCommand_RemovesFromBothThePluginAndTheCollection()
        {
            var plugin = new AxisToAxis();
            plugin.AddFilter("Aim Mode");
            var summary = CreateSummary(plugin);
            var tag = summary.FilterTags.Single();

            tag.RemoveCommand.Execute(null);

            Assert.That(summary.FilterTags, Is.Empty);
            Assert.That(plugin.Filters, Is.Empty);
        }

        [Test]
        public void SettingFilterTagNegative_UpdatesTheUnderlyingFilter()
        {
            var plugin = new AxisToAxis();
            var filter = plugin.AddFilter("Aim Mode");
            var summary = CreateSummary(plugin);
            var tag = summary.FilterTags.Single();

            tag.Negative = true;

            Assert.That(filter.Negative, Is.True);
        }
    }
}
