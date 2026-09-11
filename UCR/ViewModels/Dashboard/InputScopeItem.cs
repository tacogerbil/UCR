using System.Collections.Generic;

namespace HidWizards.UCR.ViewModels.Dashboard
{
    public class InputScopeItem
    {
        public string Id { get; }
        public string DisplayName { get; }
        public bool IsGroup { get; }
        public List<string> MemberDeviceIds { get; }

        public InputScopeItem(string id, string displayName, bool isGroup, List<string> memberDeviceIds)
        {
            Id = id;
            DisplayName = displayName;
            IsGroup = isGroup;
            MemberDeviceIds = memberDeviceIds ?? new List<string>();
        }
    }
}
