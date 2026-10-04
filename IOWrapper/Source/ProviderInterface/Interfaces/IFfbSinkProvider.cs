using HidWizards.IOWrapper.DataTransferObjects;

namespace HidWizards.IOWrapper.ProviderInterface.Interfaces
{
    /// <inheritdoc />
    /// <summary>
    /// Provider can play Force Feedback commands on one of its own (real) devices
    /// (eg SharpDX_DirectInput, replaying decoded FFB effects on a physical wheel)
    /// </summary>
    public interface IFfbSinkProvider : IProvider
    {
        /// <summary>
        /// True if this provider can drive Force Feedback on the given device right now
        /// (eg the device reports FFB capability via DirectInput)
        /// </summary>
        bool IsDeviceFfbCapable(DeviceDescriptor deviceDescriptor);

        /// <summary>
        /// Play a decoded Force Feedback command on the given device.
        /// Acquiring the device exclusively (required for FFB) is this provider's own responsibility,
        /// and should only happen for a device actually designated as an FFB target -
        /// never as a side effect of ordinary input polling
        /// </summary>
        void SubmitFfbCommand(DeviceDescriptor deviceDescriptor, FfbCommand command);
    }
}
