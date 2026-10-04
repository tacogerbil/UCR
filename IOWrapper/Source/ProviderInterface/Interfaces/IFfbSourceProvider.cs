using System;
using HidWizards.IOWrapper.DataTransferObjects;

namespace HidWizards.IOWrapper.ProviderInterface.Interfaces
{
    /// <inheritdoc />
    /// <summary>
    /// Provider can receive Force Feedback commands a game sends to one of its output devices
    /// (eg vJoy, decoding DirectInput FFB effects a game plays on a vJoy device)
    /// </summary>
    public interface IFfbSourceProvider : IOutputProvider
    {
        /// <summary>
        /// Raised whenever a Force Feedback command arrives for a device this provider outputs to.
        /// The DeviceDescriptor identifies which of this provider's own output devices the command targets -
        /// callers are responsible for correlating that to whichever physical device should receive it
        /// </summary>
        event EventHandler<FfbCommandEventArgs> FfbCommandReceived;
    }

    public class FfbCommandEventArgs : EventArgs
    {
        public DeviceDescriptor DeviceDescriptor { get; }
        public FfbCommand Command { get; }

        public FfbCommandEventArgs(DeviceDescriptor deviceDescriptor, FfbCommand command)
        {
            DeviceDescriptor = deviceDescriptor;
            Command = command;
        }
    }
}
