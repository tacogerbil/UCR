using System.Collections.Generic;
using System.Xml.Serialization;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.Core.Persistence
{
    public enum ProfileExportKind
    {
        Profile,
        ProfileList
    }

    public enum ProfileListImportMode
    {
        Merge,
        Replace
    }

    [XmlRoot("UcrExport")]
    public class ProfileExportPackage
    {
        [XmlAttribute]
        public int FormatVersion { get; set; }

        [XmlAttribute]
        public ProfileExportKind Kind { get; set; }

        [XmlArray("Profiles")]
        [XmlArrayItem("Profile")]
        public List<Profile> Profiles { get; set; }

        [XmlArray("DeviceAliases")]
        [XmlArrayItem("DeviceAlias")]
        public List<DeviceAlias> DeviceAliases { get; set; }

        public ProfileExportPackage()
        {
            Profiles = new List<Profile>();
            DeviceAliases = new List<DeviceAlias>();
        }
    }
}
