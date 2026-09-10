using System;
using System.Collections.Generic;
using System.Reflection;
using HidWizards.IOWrapper.Core;
using HidWizards.IOWrapper.DataTransferObjects;
using HidWizards.IOWrapper.ProviderInterface.Interfaces;
using NLog;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.Core.Adapters
{
    public class DeviceProviderAdapter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public SortedDictionary<string, ProviderReport> GetProviderReportsResilient(IOController ioController, DeviceIoType type)
        {
            if (ioController == null) return new SortedDictionary<string, ProviderReport>();

            try
            {
                // IOWrapper's aggregate GetInputList/GetOutputList calls every provider without isolating
                // exceptions. One bad optional provider must not hide every healthy keyboard/controller.
                // The pinned IOWrapper exposes its provider dictionary through this stable private field.
                var providersField = ioController.GetType().GetField(
                    "_providers", BindingFlags.Instance | BindingFlags.NonPublic);
                var providers = providersField?.GetValue(ioController) as IDictionary<string, IProvider>;
                
                if (providers == null)
                {
                    return type == DeviceIoType.Input
                        ? ioController.GetInputList()
                        : ioController.GetOutputList();
                }

                var probes = new List<KeyValuePair<string, Func<ProviderReport>>>();
                foreach (var entry in providers)
                {
                    var providerName = entry.Key;
                    var provider = entry.Value;
                    if (provider == null || string.IsNullOrWhiteSpace(providerName)) continue;

                    if (type == DeviceIoType.Input && provider is IInputProvider inputProvider)
                    {
                        probes.Add(new KeyValuePair<string, Func<ProviderReport>>(
                            providerName, () => inputProvider.GetInputList()));
                    }
                    else if (type == DeviceIoType.Output && provider is IOutputProvider outputProvider)
                    {
                        probes.Add(new KeyValuePair<string, Func<ProviderReport>>(
                            providerName, () => outputProvider.GetOutputList()));
                    }
                }

                return CollectProviderReports(probes, (providerName, exception) =>
                    Logger.Error(exception, "Unable to enumerate " + type + " devices from provider: " + providerName));
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Unable to enumerate IOWrapper providers individually");
                return new SortedDictionary<string, ProviderReport>();
            }
        }

        public static SortedDictionary<string, ProviderReport> CollectProviderReports(
            IEnumerable<KeyValuePair<string, Func<ProviderReport>>> probes,
            Action<string, Exception> onProviderError = null)
        {
            var reports = new SortedDictionary<string, ProviderReport>(StringComparer.OrdinalIgnoreCase);
            if (probes == null) return reports;

            foreach (var probe in probes)
            {
                try
                {
                    var report = probe.Value?.Invoke();
                    if (report != null && !string.IsNullOrWhiteSpace(probe.Key)) reports[probe.Key] = report;
                }
                catch (Exception exception)
                {
                    onProviderError?.Invoke(probe.Key, exception);
                }
            }

            return reports;
        }
    }
}
