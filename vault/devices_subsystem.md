# Devices Subsystem

The legacy `DevicesManager` handles caching, live enumeration, and polling.
Refactoring splits this into:
- `DeviceDetectionAdapter`: Wraps background thread timers.
- `DeviceCacheService`: Manages fallback states.
- `DeviceInventoryService`: Merges cached and live devices for the UI.
