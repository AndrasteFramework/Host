using System;
using System.Diagnostics;
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
        /// <param name="args">Additional args that are passed to the Payload side of Andraste</param>
        /// <returns>The Process or null, if the application has crashed</returns>
        /// <exception cref="Exception">Various Exceptions may be thrown if the application could not be started or the injection failed</exception>
        public virtual Process? StartApplication(string applicationPath, string commandLine, string modFrameworkPath, params object[] args)
        {
            Inject(applicationPath, commandLine, 0, modFrameworkPath,
                modFrameworkPath, out int pid, args);

            try
            {
                return Process.GetProcessById(pid);
            }
            catch (Exception)
            {
                return null;
            }
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
        /// <param name="targetPid">The PID of the freshly created process</param>
        /// <param name="args">Additional args that are passed to the Payload side of Andraste</param>
        protected virtual void Inject(string applicationPath, string commandLine, int additionalCreateProcessFlags,
            string injectionLibrary32, string injectionLibrary64, out int targetPid, params object[] args)
        {
            // start and inject into a new process
            RemoteHooking.CreateAndInject(
                applicationPath, // executable to run
                commandLine,                 // command line arguments for target
                additionalCreateProcessFlags,                  // additional process creation flags to pass to CreateProcess
                InjectionOptions.DoNotRequireStrongName, // allow injectionLibrary to be unsigned
                injectionLibrary32,   // 32-bit library to inject (if target is 32-bit)
                injectionLibrary64,   // 64-bit library to inject (if target is 64-bit)
                out targetPid,      // retrieve the newly created process ID
                args // the parameters to pass into injected library
            );
        }
    }
    #nullable restore
}
