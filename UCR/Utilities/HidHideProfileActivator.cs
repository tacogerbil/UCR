using System;
using System.Diagnostics;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Adapters;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Services;
using HidWizards.UCR.Core.Utilities;

namespace HidWizards.UCR.Utilities
{
    /// <summary>
    /// Composition glue: keeps HidHide in step with whichever profile is active. Hides the active
    /// profile's HiddenDevices when it activates (manually or via auto-activate) and releases them
    /// when it stops or UCR exits. All policy lives in HidHideProfileService.
    /// </summary>
    public sealed class HidHideProfileActivator : IDisposable
    {
        private readonly Context _context;
        private readonly HidHideProfileService _service;
        private bool _disposed;

        public HidHideProfileActivator(Context context)
            : this(context, new HidHideProfileService(new HidHideDriverAdapter(), GetOwnExecutablePath()))
        {
        }

        internal HidHideProfileActivator(Context context, HidHideProfileService service)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _context.ActiveProfileChangedEvent += OnActiveProfileChanged;
        }

        private void OnActiveProfileChanged(Profile profile)
        {
            if (_disposed) return;
            var result = _service.Apply(profile);
            if (!result.Success) Logger.Warn("HidHide: " + result.Message);
        }

        private static string GetOwnExecutablePath()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.MainModule?.FileName;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context.ActiveProfileChangedEvent -= OnActiveProfileChanged;
            _service.ReleaseAll();
        }
    }
}
