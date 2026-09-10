using System;
using System.Collections.Generic;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Utilities;

namespace HidWizards.UCR.Core.Services
{
    public class DeviceCacheService
    {
        private readonly DeviceCacheStore _deviceCacheStore;

        public DeviceCacheService(string cacheRoot)
        {
            _deviceCacheStore = new DeviceCacheStore(cacheRoot);
        }

        public List<Device> LoadAllProviders()
        {
            return _deviceCacheStore.LoadAllProviders();
        }

        public List<Device> LoadProvider(string providerName)
        {
            return _deviceCacheStore.LoadProvider(providerName);
        }

        public bool UpdateDeviceCache(IEnumerable<Device> availableDeviceList, Func<Device, DeviceIoType, bool, List<DeviceBindingNode>> getBindingMenuFunc)
        {
            var success = true;
            _deviceCacheStore.RemoveOverlapping(availableDeviceList);
            foreach (var device in availableDeviceList)
            {
                success &= _deviceCacheStore.Save(device, getBindingMenuFunc(device, DeviceIoType.Input, false));
            }
            _deviceCacheStore.ClearMemoryCache();
            return success;
        }
    }
}
