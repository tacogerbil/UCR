using System.Collections.Generic;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Services;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ServiceTests
{
    [TestFixture]
    internal class OutputSlotResolverTests
    {
        [Test]
        public void Resolve_NullTree_ReturnsEmptyList()
        {
            Assert.That(OutputSlotResolver.Resolve(null), Is.Empty);
        }

        [Test]
        public void Resolve_EmptyTree_ReturnsEmptyList()
        {
            Assert.That(OutputSlotResolver.Resolve(new List<DeviceBindingNode>()), Is.Empty);
        }

        [Test]
        public void Resolve_FlattensNestedLeavesInOrder_WithJoinedTitles()
        {
            var tree = new List<DeviceBindingNode>
            {
                new DeviceBindingNode
                {
                    Title = "Left Stick",
                    ChildrenNodes = new List<DeviceBindingNode>
                    {
                        new DeviceBindingNode
                        {
                            Title = "X",
                            DeviceBindingInfo = new DeviceBindingInfo
                                { KeyType = 1, KeyValue = 0, KeySubValue = 0, DeviceBindingCategory = DeviceBindingCategory.Range }
                        },
                        new DeviceBindingNode
                        {
                            Title = "Y",
                            DeviceBindingInfo = new DeviceBindingInfo
                                { KeyType = 1, KeyValue = 1, KeySubValue = 0, DeviceBindingCategory = DeviceBindingCategory.Range }
                        }
                    }
                },
                new DeviceBindingNode
                {
                    Title = "A",
                    DeviceBindingInfo = new DeviceBindingInfo
                        { KeyType = 2, KeyValue = 0, KeySubValue = 0, DeviceBindingCategory = DeviceBindingCategory.Momentary }
                }
            };

            var slots = OutputSlotResolver.Resolve(tree);

            Assert.That(slots.Count, Is.EqualTo(3));

            Assert.That(slots[0].Title, Is.EqualTo("Left Stick, X"));
            Assert.That(slots[0].Category, Is.EqualTo(DeviceBindingCategory.Range));
            Assert.That(slots[0].SlotKey, Is.EqualTo("1:0:0"));

            Assert.That(slots[1].Title, Is.EqualTo("Left Stick, Y"));
            Assert.That(slots[1].SlotKey, Is.EqualTo("1:1:0"));

            Assert.That(slots[2].Title, Is.EqualTo("A"));
            Assert.That(slots[2].Category, Is.EqualTo(DeviceBindingCategory.Momentary));
            Assert.That(slots[2].SlotKey, Is.EqualTo("2:0:0"));
        }

        [Test]
        public void Resolve_DistinctKeyValues_ProduceDistinctSlotKeys()
        {
            var tree = new List<DeviceBindingNode>
            {
                new DeviceBindingNode { Title = "A", DeviceBindingInfo = new DeviceBindingInfo { KeyType = 2, KeyValue = 0 } },
                new DeviceBindingNode { Title = "B", DeviceBindingInfo = new DeviceBindingInfo { KeyType = 2, KeyValue = 1 } }
            };

            var slots = OutputSlotResolver.Resolve(tree);

            Assert.That(slots[0].SlotKey, Is.Not.EqualTo(slots[1].SlotKey));
        }
    }
}
