using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;

namespace HidWizards.UCR.Core.Adapters
{
    public class DetectedInputControl
    {
        public Device Device { get; set; }
        public string ControlTitle { get; set; }
    }

    public class DeviceDetectionAdapter
    {
        private readonly object _deviceDetectionLock = new object();
        private TaskCompletionSource<DetectedInputControl> _deviceDetectionCompletion;
        private List<Device> _deviceDetectionDevices;
        private Timer _deviceDetectionTimer;
        private CancellationTokenRegistration _deviceDetectionCancellation;
        private DateTime _deviceDetectionAcceptAfterUtc = DateTime.MaxValue;
        private DeviceBindingCategory? _deviceDetectionRequiredCategory;

        public class DetectionState
        {
            public List<Device> Devices { get; set; }
            public DateTime AcceptAfterUtc { get; set; }
            public DeviceBindingCategory? RequiredCategory { get; set; }
        }

        public DetectionState GetState()
        {
            lock (_deviceDetectionLock)
            {
                if (_deviceDetectionCompletion == null) return null;
                return new DetectionState
                {
                    Devices = _deviceDetectionDevices,
                    AcceptAfterUtc = _deviceDetectionAcceptAfterUtc,
                    RequiredCategory = _deviceDetectionRequiredCategory
                };
            }
        }

        public Task<DetectedInputControl> ArmDetectionTimer(
            int timeout, 
            CancellationToken cancellationToken, 
            List<Device> devices,
            DeviceBindingCategory? requiredCategory,
            TimeSpan armDelay,
            Action<DetectedInputControl> completeCallback)
        {
            var completion = new TaskCompletionSource<DetectedInputControl>();
            lock (_deviceDetectionLock)
            {
                _deviceDetectionCompletion = completion;
                _deviceDetectionDevices = devices;
                _deviceDetectionAcceptAfterUtc = DateTime.UtcNow + armDelay;
                _deviceDetectionRequiredCategory = requiredCategory;

                _deviceDetectionTimer = new Timer(_ => CompleteDeviceDetection(null, completeCallback), null, timeout, Timeout.Infinite);
                if (cancellationToken.CanBeCanceled)
                {
                    _deviceDetectionCancellation = cancellationToken.Register(() =>
                        ThreadPool.QueueUserWorkItem(_ => CompleteDeviceDetection(null, completeCallback)));
                }
            }
            return completion.Task;
        }

        public void CancelInputDeviceDetection(Action<DetectedInputControl> completeCallback)
        {
            CompleteDeviceDetection(null, completeCallback);
        }

        public void CompleteDeviceDetection(DetectedInputControl detectedInput, Action<DetectedInputControl> completeCallback)
        {
            TaskCompletionSource<DetectedInputControl> completion;
            List<Device> devices;
            Timer timer;
            CancellationTokenRegistration cancellation;

            lock (_deviceDetectionLock)
            {
                completion = _deviceDetectionCompletion;
                if (completion == null) return;

                devices = _deviceDetectionDevices ?? new List<Device>();
                timer = _deviceDetectionTimer;
                cancellation = _deviceDetectionCancellation;

                _deviceDetectionCompletion = null;
                _deviceDetectionDevices = null;
                _deviceDetectionTimer = null;
                _deviceDetectionCancellation = default(CancellationTokenRegistration);
                _deviceDetectionAcceptAfterUtc = DateTime.MaxValue;
                _deviceDetectionRequiredCategory = null;
            }

            timer?.Dispose();
            cancellation.Dispose();
            
            completeCallback?.Invoke(detectedInput);
        }
    }
}
