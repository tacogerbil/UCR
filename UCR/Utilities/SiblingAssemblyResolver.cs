using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using HidWizards.UCR.Core.Utilities;

namespace HidWizards.UCR.Utilities
{
    /// <summary>
    /// .NET Framework's LoadFrom context implicitly probed a dynamically loaded assembly's own
    /// directory for its dependencies. .NET (Core+) does not. MEF's DirectoryCatalog relies on that
    /// old behavior to load IOWrapper providers from Providers/&lt;name&gt; and UCR plugins from
    /// Plugins/&lt;name&gt;, so a provider/plugin with its own extra dependency DLL (e.g.
    /// Core_vJoyInterfaceWrap needing vJoyInterfaceWrap.dll from its own folder) fails to load under
    /// net8.0-windows unless something restores that probing.
    /// </summary>
    public static class SiblingAssemblyResolver
    {
        private static bool _registered;

        public static void Register(params string[] rootFolderNames)
        {
            if (_registered) return;
            _registered = true;

            var roots = rootFolderNames
                .Select(name => Path.Combine(AppContext.BaseDirectory, name))
                .Where(Directory.Exists)
                .ToArray();

            AssemblyLoadContext.Default.Resolving += (_, assemblyName) => Resolve(roots, assemblyName);
        }

        internal static string FindSiblingAssemblyFile(string rootDirectory, string fileName)
        {
            if (!Directory.Exists(rootDirectory)) return null;

            return Directory.EnumerateDirectories(rootDirectory)
                .Select(subfolder => Path.Combine(subfolder, fileName))
                .FirstOrDefault(File.Exists);
        }

        private static Assembly Resolve(string[] roots, AssemblyName assemblyName)
        {
            var fileName = assemblyName.Name + ".dll";
            var candidate = roots
                .Select(root => FindSiblingAssemblyFile(root, fileName))
                .FirstOrDefault(path => path != null);
            if (candidate == null) return null;

            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            }
            catch (Exception e)
            {
                Logger.Warn($"Found candidate assembly '{candidate}' for '{assemblyName}' but failed to load it", e);
                return null;
            }
        }
    }
}
