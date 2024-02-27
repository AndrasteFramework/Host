using System;
using System.Diagnostics;
using System.IO;
using EasyHook;

namespace Andraste.Host
{
    #nullable enable
    public abstract class EntryPoint
    {
        public void Initialize()
        {
        }

        /// <summary>
        /// Starts the application and injects the mod dll into it.
        /// </summary>
        /// <param name="applicationPath">The Path to the Application to launch</param>
        /// <param name="commandLine">The CommandLine Flags to pass to the Application</param>
        /// <param name="modFrameworkPath">The Path to the DLL to inject into the process (Payload)</param>
        /// <param name="profileFolder">The Folder where all the mods/mods.json are located (profile folder)</param>
        /// <param name="args">Additional args that are passed to the Payload side of Andraste</param>
        /// <returns>The Process or null, if the application has crashed</returns>
        /// <exception cref="Exception">Various Exceptions may be thrown if the application could not be started or the injection failed</exception>
        public virtual Process? StartApplication(string applicationPath, string commandLine, string modFrameworkPath, 
            string profileFolder, params object[] args)
        {
            if (!File.Exists(applicationPath))
            {
                throw new ArgumentException("Application file does not exist", nameof(applicationPath));
            }

            if (!File.Exists(modFrameworkPath))
            {
                throw new ArgumentException("Mod Framework file does not exist", nameof(modFrameworkPath));
            }
            
            Inject(applicationPath, commandLine, 0, modFrameworkPath,
                modFrameworkPath, profileFolder, out int pid, args);

            try
            {
                return Process.GetProcessById(pid);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public virtual void AttachToApplication(Process process, string modFrameworkPath, string profileFolder, 
            params object[] args)
        {
            if (!File.Exists(modFrameworkPath))
            {
                throw new ArgumentException("Mod Framework file does not exist", nameof(modFrameworkPath));
            }
            
            InjectRunning(process, modFrameworkPath,modFrameworkPath, profileFolder, args);
        }

        /// <summary>
        /// This method starts the application and injects the given DLL into the target process.<br />
        /// Since this implementation is almost a direct translation call to
        /// <see cref="RemoteHooking.CreateAndInject(string,string,int,InjectionOptions,string,string,out int,object[])"/>,
        /// there isn't much need in overriding the content.
        /// </summary>
        /// <param name="applicationPath">The Path to the Application to launch</param>
        /// <param name="commandLine">The CommandLine Flags to pass to the Application</param>
        /// <param name="additionalCreateProcessFlags">Additional flags being passed to CreateProcess</param>
        /// <param name="injectionLibrary32">The 32bit DLL to inject into the process</param>
        /// <param name="injectionLibrary64">The 64bit DLL to inject into the process</param>
        /// <param name="profileFolder">The Folder where all the mods/mods.json are located (profile folder)</param>
        /// <param name="targetPid">The PID of the freshly created process</param>
        /// <param name="args">Additional args that are passed to the Payload side of Andraste</param>
        protected virtual void Inject(string applicationPath, string commandLine, int additionalCreateProcessFlags,
            string injectionLibrary32, string injectionLibrary64, string profileFolder, out int targetPid,  
            params object[] args)
        {
            var argsArray = new object[args.Length + 1];
            argsArray[0] = profileFolder;
            args.CopyTo(argsArray, 1);
            
            // start and inject into a new process
            RemoteHooking.CreateAndInject(
                applicationPath, // executable to run
                commandLine,                 // command line arguments for target
                additionalCreateProcessFlags,                  // additional process creation flags to pass to CreateProcess
                InjectionOptions.DoNotRequireStrongName | InjectionOptions.NoWOW64Bypass, // allow injectionLibrary to be unsigned
                injectionLibrary32,   // 32-bit library to inject (if target is 32-bit)
                injectionLibrary64,   // 64-bit library to inject (if target is 64-bit)
                out targetPid,      // retrieve the newly created process ID
                argsArray // the parameters to pass into injected library
            );
        }

        protected virtual void InjectRunning(Process process, string injectionLibrary32, string injectionLibrary64,
            string profileFolder, params object[] args)
        {
            var argsArray = new object[args.Length + 1];
            argsArray[0] = profileFolder;
            args.CopyTo(argsArray, 1);
            
            RemoteHooking.Inject(process.Id, InjectionOptions.DoNotRequireStrongName | InjectionOptions.NoWOW64Bypass,
                injectionLibrary32, injectionLibrary64, argsArray);
        }
    }
    #nullable restore
}
