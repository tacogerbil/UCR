using System.Collections.Generic;
using HidWizards.UCR.Core.Models;
using System;

namespace HidWizards.UCR.Core.Services
{
    public class DeviceInventoryService
    {
        private readonly DeviceCacheService _cacheService;

        public DeviceInventoryService(DeviceCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public List<Device> GetManagementDeviceList(DeviceIoType type)
        {
            // Will merge live backend data and _cacheService fallback
            throw new NotImplementedException();
        }
    }
}
