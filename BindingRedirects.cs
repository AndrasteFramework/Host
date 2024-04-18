using System;
using System.IO;

namespace Andraste.Host
{
    public static class BindingRedirects
    {
        [Obsolete("Prefer to use the variants that require specifying the path to the actual .config files")]
        public static void Setup(string exePath, string dllName)
        {
            // Unfortunately, .NET FX requires us to add the config file with the bindings redirect, otherwise it fails to load assemblies.
            // This fails when you run the game multiple times with different .configs (or if the .config is locked by the file?), but that's a corner case.
            // TODO: In theory we'd need to merge files, because here, dllName.config does not containing transitive rewrites that are part in Andraste.Shared.dll.config
            var bindingRedirectFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName + ".config");
            var bindingRedirectShared = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Andraste.Shared.dll.config");
            if (File.Exists(bindingRedirectFile))
            {
                File.Copy(bindingRedirectFile, exePath + ".config", true);
                // For some reason, debugging has shown that sometimes, it tries to resolve the .configs in the Launcher directory. Is that dependant on the app?
                File.Copy(bindingRedirectFile,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(exePath)) + ".config", true);
                //File.Copy(bindingRedirectShared, Path.Combine(Path.GetDirectoryName(exePath)!, "Andraste.Shared.dll.config"), true);
            }
            else if (File.Exists(bindingRedirectShared))
            {
                Console.WriteLine("Warning: Framework does not have a specific binding redirect file. Trying Andraste.Shared");
                File.Copy(bindingRedirectShared, exePath + ".config", true);
            }
            else
            {
                Console.WriteLine(
                    $"Warning: Could not find a binding redirect file at {bindingRedirectFile}. Try to have your IDE generate one.");
            }
        }

        public static void CopyRedirects(string sourceFile, string applicationPath, string applicationFile)
        {
            File.Copy(sourceFile, Path.Combine(applicationPath, applicationFile + ".config"), true);
        }
        
        // TODO: Merge XML files, but this may be non-trivial due to actual version conflicts, so rather make downstream frameworks supply the right config files.
    }
}