# HidHide reference (fetched via Context7, 2026-10-03)

Sources: `/nefarius/nefarius.drivers.hidhide` (managed API, NuGet `Nefarius.Drivers.HidHide` 3.4.0),
`/websites/nefarius_at_projects_hidhide` (driver docs).

## Model
HidHide hides HID/XInput devices from every process **except** whitelisted applications.
Three pieces of driver state: blocked device instance IDs, whitelisted application paths, global
`IsActive` switch. Driver control handle is **exclusive** -- one process at a time, close promptly.

## `HidHideControlService` (used by `UCR.Core/Adapters/HidHideDriverAdapter.cs`)
- `new HidHideControlService()` (non-DI ctor)
- `bool IsInstalled` -- driver present and operable (can throw `HidHideDetectionFailedException`)
- `bool IsActive { get; set; }` -- global hiding on/off
- `IReadOnlyList<string> BlockedInstanceIds`, `IReadOnlyList<string> ApplicationPaths`
- `AddBlockedInstanceId(string)`, `RemoveBlockedInstanceId(string)`
- `AddApplicationPath(string path, bool throwIfInvalid)` -- absolute local path, e.g. `C:\...\app.exe`
- Exceptions: `HidHideDriverAccessFailedException` (handle busy), `HidHideDriverNotFoundException`,
  `HidHideRequestFailedException`, `HidHideBufferOverflowException`.

## Instance IDs
UCR derives them from a device's HID interface path via `DevicesManager.TryGetWindowsDeviceInstanceId`
(`\\?\HID#VID_xxxx&PID_yyyy#7&..#{guid}` -> `HID\VID_xxxx&PID_yyyy\7&..`).

## UCR usage
Per-profile `Profile.HiddenDevices`; `HidHideProfileService` applies on `Context.ActiveProfileChangedEvent`
and releases on stop/exit. UCR's own exe is always whitelisted first. ViGEm/vJoy virtual devices
(`vid_045e&pid_028e`, `vid_1234&pid_bead`) are never blockable (`HidHidePlanner.IsProtected`).
Hiding only affects handles opened *after* it is applied -- start the UCR profile before launching the game.
