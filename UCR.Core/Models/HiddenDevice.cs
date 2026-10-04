using System.Xml.Serialization;

namespace HidWizards.UCR.Core.Models
{
    /// <summary>
    /// A physical device a profile asks HidHide to hide from every application except UCR while the
    /// profile is active (so a game only sees UCR's virtual output, not the raw wheel/pedals).
    /// Persisted per profile. InstanceId is the Windows device instance ID HidHide blocks on; it is
    /// stable per USB port, so re-plugging into a different port needs the device re-ticked.
    /// </summary>
    public sealed class HiddenDevice
    {
        [XmlAttribute]
        public string InstanceId { get; set; }

        [XmlAttribute]
        public string DisplayName { get; set; }

        public HiddenDevice()
        {
        }

        public HiddenDevice(string instanceId, string displayName)
        {
            InstanceId = instanceId;
            DisplayName = displayName;
        }

        public HiddenDevice Clone() => new HiddenDevice(InstanceId, DisplayName);
    }
}
