using System;
using System.Threading;
using System.Threading.Tasks;

namespace HidWizards.UCR.Core.Adapters
{
    public class DeviceDetectionAdapter
    {
        private static readonly TimeSpan DeviceDetectionArmDelay = TimeSpan.FromMilliseconds(350);
        private readonly object _deviceDetectionLock = new object();
        private Timer _deviceDetectionTimer;
        private CancellationTokenRegistration _deviceDetectionCancellation;

        public void ArmDetectionTimer()
        {
            // Isolated threading logic from DevicesManager
            throw new NotImplementedException();
        }

        public void CancelInputDeviceDetection()
        {
            lock (_deviceDetectionLock)
            {
                _deviceDetectionTimer?.Dispose();
                _deviceDetectionCancellation.Dispose();
            }
        }
    }
}
