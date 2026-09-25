using System.Collections.Generic;

namespace HidWizards.UCR.Core.Models.Binding
{
    /// <summary>
    /// A single leaf-reachable binding found while walking a DeviceBindingNode tree, paired with its
    /// full display path (ancestor titles joined the same way Device.GetBindingName does, e.g.
    /// "Left Stick, X").
    /// </summary>
    public sealed class FlattenedDeviceBinding
    {
        public string Title { get; }
        public DeviceBindingInfo Info { get; }

        public FlattenedDeviceBinding(string title, DeviceBindingInfo info)
        {
            Title = title;
            Info = info;
        }
    }

    public static class DeviceBindingNodeFlattener
    {
        /// <summary>
        /// Recursively walks a device binding menu tree and returns every node that carries a
        /// DeviceBindingInfo, alongside its full "Parent, Child" display path. A node's own
        /// DeviceBindingInfo is collected even if it also has children, matching the traversal
        /// DeviceBindingCompatibility has always used (never assume a menu node is exclusively
        /// branch or leaf).
        /// </summary>
        public static List<FlattenedDeviceBinding> Flatten(IEnumerable<DeviceBindingNode> nodes, string parentTitle = null)
        {
            var result = new List<FlattenedDeviceBinding>();
            if (nodes == null) return result;

            foreach (var node in nodes)
            {
                if (node == null) continue;

                var title = parentTitle == null ? node.Title : $"{parentTitle}, {node.Title}";

                if (node.DeviceBindingInfo != null)
                {
                    result.Add(new FlattenedDeviceBinding(title, node.DeviceBindingInfo));
                }

                if (node.ChildrenNodes != null)
                {
                    result.AddRange(Flatten(node.ChildrenNodes, title));
                }
            }

            return result;
        }
    }
}
