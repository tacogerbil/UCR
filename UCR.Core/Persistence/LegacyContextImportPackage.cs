using System.Collections.Generic;
using System.Xml.Serialization;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.Core.Persistence
{
    [XmlRoot("Context")]
    public class LegacyContextImportPackage
    {
        [XmlArray("Profiles")]
        [XmlArrayItem("Profile")]
        public List<Profile> Profiles { get; set; }

        [XmlArray("DeviceAliases")]
        [XmlArrayItem("DeviceAlias")]
        public List<DeviceAlias> DeviceAliases { get; set; }

        public LegacyContextImportPackage()
        {
            Profiles = new List<Profile>();
            DeviceAliases = new List<DeviceAlias>();
        }
    }
}
