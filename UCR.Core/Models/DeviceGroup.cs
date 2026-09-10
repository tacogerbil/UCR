using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HidWizards.UCR.Core.Models
{
    public class DeviceGroup
    {
        [XmlAttribute]
        public Guid Guid { get; set; }

        [XmlAttribute]
        public string Title { get; set; }

        public List<string> MemberDeviceIdentities { get; set; }

        public DeviceGroup()
        {
            Guid = Guid.NewGuid();
            MemberDeviceIdentities = new List<string>();
        }

        public DeviceGroup(string title, List<string> memberIdentities) : this()
        {
            Title = title;
            MemberDeviceIdentities = memberIdentities ?? new List<string>();
        }
    }
}
