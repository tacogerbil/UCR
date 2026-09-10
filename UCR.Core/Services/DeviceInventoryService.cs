using System;
using System.Collections.Generic;
using HidWizards.UCR.Core.Models;
using NLog;

namespace HidWizards.UCR.Core.Services
{
    public class DeviceInventoryService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DeviceCacheService _cacheService;

        public DeviceInventoryService(DeviceCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public List<Device> GetManagementDeviceList(
            DeviceIoType type,
            bool isIoControllerAvailable,
            Func<DeviceIoType, List<Device>> getLiveDevices,
            Func<List<Device>> getCachedInputInventory)
        {
            if (isIoControllerAvailable)
            {
                try
                {
                    var devices = getLiveDevices(type);
                    if (devices.Count > 0 || type == DeviceIoType.Output) return devices;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Unable to enumerate devices for the Devices management page");
                }
            }

            return type == DeviceIoType.Input
                ? getCachedInputInventory()
                : new List<Device>();
        }
    }
}
