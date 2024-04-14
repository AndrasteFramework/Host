using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Andraste.Host.Logging;
using Andraste.Shared.Util;

#nullable enable
namespace Andraste.Host.CommandLine
{
    public class CliEntryPoint: EntryPoint
    {
        private readonly RootCommand _rootCommand;

        public CliEntryPoint()
        {
            _rootCommand = new RootCommand("The Andraste Game Launcher Component");
            BuildSubCommands();
            Initialize();
        }

        private void BuildSubCommands()
        {
            var nonInteractiveOption = new Option<bool>("--non-interactive", () => false, 
                "non-interactive mode: Do not redirect logging output. To be used for programmatic launches");
            
            var fileOption = new Option<string>("--file", 
                "The path to the application's executable")
            {
                IsRequired = true
            };

            var frameworkDllOption = new Option<string>("--frameworkDll",
                GetDefaultFrameworkName,
                "The name of the framework dll (that has to be in _this_ folder) to use")
            {
                IsRequired = true
            };

            var modsJsonPathOption = new Option<string>("--modsJsonPath",
                "The path to the mods.json file, that contains all necessary information");
            var modsFolderPathOption = new Option<string>("--modsPath",
                "The path to the mods folder. If you can't launch by using a mods.json, this will auto-enable " +
                "all mods. Prefer to use --modsJsonPath where possible.");
            ValidateSymbolResult<CommandResult> modsValidator = result =>
            {
                if (result.Children.Count(s => s.Symbol == modsJsonPathOption || s.Symbol == modsFolderPathOption) != 1)
                {
                    result.ErrorMessage = "Either --modsJsonPath or --modsPath have to be specified";
                    return;
                }

                if (result.Children.Any(s => s.Symbol == modsJsonPathOption))
                {
                    var modsJsonPath = result.GetValueForOption(modsJsonPathOption);
                    if (!File.Exists(modsJsonPath))
                    {
                        result.ErrorMessage = $"File {modsJsonPath} does not exist!";
                    }
                }

                if (result.Children.Any(s => s.Symbol == modsFolderPathOption))
                {
                    var modsFolder = result.GetValueForOption(modsFolderPathOption);
                    if (!Directory.Exists(modsFolder))
                    {
                        result.ErrorMessage = $"Folder {modsFolder} does not exist!";
                    }
                }
            };

            ValidateSymbolResult<CommandResult> validateFilesExist = result =>
            {
                var symbol = result.Children.FirstOrDefault(s => s.Symbol == fileOption);
                if (symbol != null)
                {
                    var path = result.GetValueForOption(fileOption);
                    if (!File.Exists(path))
                    {
                        result.ErrorMessage = $"File {path} does not exist!";
                    }
                }

                symbol = result.Children.FirstOrDefault(s => s.Symbol == frameworkDllOption);
                if (symbol != null && result.GetValueForOption(frameworkDllOption) != null)
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        result.GetValueForOption(frameworkDllOption)!);
                    if (!File.Exists(path))
                    {
                        result.ErrorMessage = $"File {path} does not exist!";
                    }
                }
            };

            var commandLineArgument =
                new Option<string>("--commandLine", "The command line to pass to the application");
            var launchCommand = new Command("launch", "Launch an executable by path")
            {
                fileOption,
                frameworkDllOption,
                modsJsonPathOption,
                modsFolderPathOption,
                commandLineArgument
            };
            launchCommand.AddValidator(modsValidator);
            launchCommand.AddValidator(validateFilesExist);

            var monitorCommand = new Command("monitor", "Monitor an executable by path and auto-attach")
            {
                fileOption,
                frameworkDllOption,
                modsJsonPathOption,
                modsFolderPathOption
            };
            monitorCommand.AddValidator(modsValidator);
            monitorCommand.AddValidator(validateFilesExist);

            var pidOption = new Option<int>("pid", "The process id to attach to")
            {
                IsRequired = true
            };

            ValidateSymbolResult<CommandResult> validPidValidator = result =>
            {
                var processId = result.GetValueForOption(pidOption);
                try
                {
                    var proc = Process.GetProcessById(processId);
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"Process {processId} not found! {ex}";
                }
            };
            
            var attachCommand = new Command("attach", "Attach to a running process")
            {
                pidOption,
                frameworkDllOption,
                modsJsonPathOption,
                modsFolderPathOption
            };
            attachCommand.AddValidator(modsValidator);
            attachCommand.AddValidator(validPidValidator);
            
            _rootCommand.AddCommand(launchCommand);
            _rootCommand.AddCommand(monitorCommand);
            _rootCommand.AddCommand(attachCommand);
            _rootCommand.AddGlobalOption(nonInteractiveOption);
            
            launchCommand.SetHandler(LaunchGame, nonInteractiveOption, fileOption, frameworkDllOption, modsJsonPathOption, modsFolderPathOption, commandLineArgument);
            monitorCommand.SetHandler(MonitorGame, nonInteractiveOption, fileOption, frameworkDllOption, modsJsonPathOption, modsFolderPathOption);
            attachCommand.SetHandler(AttachGame, nonInteractiveOption, pidOption, frameworkDllOption, modsJsonPathOption, modsFolderPathOption);
        }

        protected virtual string GetDefaultFrameworkName()
        {
            return "Andraste.Payload.Generic.dll";
        }

        public void InvokeSync(string commandLine, IConsole? outputConsole)
        {
            _rootCommand.Invoke(commandLine, outputConsole ?? new SystemConsole());
        }
        
        public void InvokeSync(string[] commandLine, IConsole? outputConsole) {
            _rootCommand.Invoke(commandLine, outputConsole ?? new SystemConsole());
        }

        // Not sure if there is a useful use case.
        public async Task InvokeAsync(string commandLine, IConsole? outputConsole)
        {
            await _rootCommand.InvokeAsync(commandLine, outputConsole ?? new SystemConsole());
        }

        protected virtual void LaunchGame(bool nonInteractive, string applicationPath, string frameworkDllName,
            string? modsJsonPath, string? modsFolder, string commandLine)
        {
            var profileFolder = PreLaunch(modsJsonPath, modsFolder);
            // actually, we need the framework folder but with the game name? This fixes binding redirects apparently.
            SetupBindingRedirects(applicationPath, frameworkDllName);
            var process = StartApplication(applicationPath, commandLine, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, frameworkDllName), profileFolder);
            PostLaunch(process, profileFolder, nonInteractive);
        }

        protected virtual void SetupBindingRedirects(string applicationPath, string frameworkDllName)
        {
            var redirectFile = frameworkDllName + ".config";
            if (!File.Exists(redirectFile))
            {
                // Fall back to the generic DLL
                redirectFile = "Andraste.Payload.Generic.dll.config";
            }

            BindingRedirects.CopyRedirects(redirectFile, AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(applicationPath));
        }

        protected virtual void MonitorGame(bool nonInteractive, string applicationPath, string frameworkDllName,
            string? modsJsonPath, string modsFolder)
        {
            PreLaunch(modsJsonPath, modsFolder);
            SetupBindingRedirects(applicationPath, frameworkDllName);
            // TODO: PostLaunch
            //PostLaunch();
        }

        protected virtual void AttachGame(bool nonInteractive, int pid, string frameworkDllName,
            string? modsJsonPath, string modsFolder)
        {
            var profileFolder = PreLaunch(modsJsonPath, modsFolder);
            var process = Process.GetProcessById(pid);
            // TODO: Does this MainModule work?
            SetupBindingRedirects(process.MainModule!.FileName, frameworkDllName);
            PostLaunch(process, profileFolder, nonInteractive);
        }

        protected virtual string PreLaunch(string? modsJsonPath, string? modsFolder)
        {
            if (!string.IsNullOrEmpty(modsJsonPath))
            {
                return Directory.GetParent(modsJsonPath)!.FullName;
            }
            
            if (!Directory.Exists(modsFolder))
            {
                Console.WriteLine("Creating \"mods\" folder");
                Directory.CreateDirectory(modsFolder);
            }
            
            // Build the mods.json
            var modsJson = ModJsonBuilder.WriteModsJson(modsFolder);
            File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods.json"), modsJson);
            return Directory.GetParent(modsFolder)!.FullName;

        }

        protected virtual void PostLaunch(Process? process, string profileFolder, bool nonInteractive)
        {
            if (nonInteractive)
            {
                // return PID and terminate. We use the PID as the exit code here as well, because that's easy to read out. 
                Console.WriteLine(process?.Id ?? -1);
                Environment.Exit(process?.Id ?? -1);
            }
            else
            {
                if (process == null)
                {
                    Console.Error.WriteLine("Failed to launch the application!");
                    return;
                }
                
                Console.Title = $"Andraste Console Launcher - Attached to PID {process.Id}";

                #region Logging
                var output = new FileLoggingHost(Path.Combine(profileFolder, "output.log"));
                var err = new FileLoggingHost(Path.Combine(profileFolder, "error.log"));
                output.LoggingEvent += (sender, args) => Console.WriteLine(args.Text);
                err.LoggingEvent += (sender, args) => Console.Error.WriteLine(args.Text);
                output.StartListening();
                err.StartListening();
                #endregion

                // Keep this thread (and thus the application) running
                process.WaitForExit();

                // Dispose/Cleanup
                output.StopListening();
                err.StopListening();
                Console.WriteLine("Process exited");
            }
        }
    }
}
#nullable restore
