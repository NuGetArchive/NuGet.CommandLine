using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Diagnostics;
using NuGet.CommandLine.Common;
using NuGet.Configuration;
using System.Reflection;
using NuGet.PackageManagement;
using NuGet.Client;

namespace NuGet.CommandLine.Commands
{
    public abstract class Command : ICommand
    {
        private const string CommandSuffix = "Command";
        private CommandAttribute _commandAttribute;

        protected Command()
        {
            Arguments = new List<string>();
        }

        public IList<string> Arguments { get; private set; }

        [Import]
        public IConsole Console { get; set; }

        [Import]
        public HelpCommand HelpCommand { get; set; }

        [Import]
        public ICommandManager Manager { get; set; }

        [Import]
        public IMachineWideSettings MachineWideSettings { get; set; }

        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> ResourceProviders { get; set; }

        [Option("help", AltName = "?")]
        public bool Help { get; set; }

        [Option(typeof(NuGetCommandResourceType), "Option_Verbosity")]
        public Verbosity Verbosity { get; set; }

        [Option(typeof(NuGetCommandResourceType), "Option_NonInteractive")]
        public bool NonInteractive { get; set; }

        [Option(typeof(NuGetCommandResourceType), "Option_ConfigFile")]
        public string ConfigFile { get; set; }

        protected internal ISettings Settings { get; set; }

        public CommandAttribute CommandAttribute
        {
            get
            {
                if (_commandAttribute == null)
                {
                    _commandAttribute = GetCommandAttribute();
                }
                return _commandAttribute;
            }
        }

        public virtual bool IncludedInHelp(string optionName)
        {
            return true;
        }

        public void Execute()
        {
            if (Help)
            {
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);
            }
            else
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                if (String.IsNullOrEmpty(ConfigFile))
                {
                    Settings = Configuration.Settings.LoadDefaultSettings(
                       Directory.GetCurrentDirectory(),
                        configFileName: null,
                        machineWideSettings: MachineWideSettings);
                }
                else
                {
                    //if config file doesn't exist, set configfile name null
                    var directory = Path.GetDirectoryName(Path.GetFullPath(ConfigFile));
                    var configFileName = Path.GetFileName(ConfigFile);
                  
                    if (!File.Exists(Path.GetFullPath(ConfigFile)))
                    {
                        configFileName = null;
                    }

                    Settings = Configuration.Settings.LoadDefaultSettings(
                        Path.GetFullPath(ConfigFile),
                        configFileName,
                        MachineWideSettings);
                }

                ExecuteCommand();
                watch.Stop();
                DisplayExecutedTime(watch.Elapsed, CommandAttribute.CommandName);
            }
        }

        public abstract void ExecuteCommand();

        protected void DisplayExecutedTime(TimeSpan elapsed, string executionName)
        {
            if (Verbosity == Verbosity.Detailed)
            {
                Console.WriteLine("Executed '{0}' in {1} seconds", executionName, elapsed.TotalSeconds);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method does quite a bit of processing.")]
        public virtual CommandAttribute GetCommandAttribute()
        {
            var attributes = GetType().GetCustomAttributes(typeof(CommandAttribute), true);
            if (attributes.Any())
            {
                return (CommandAttribute)attributes.FirstOrDefault();
            }

            // Use the command name minus the suffix if present and default description
            string name = GetType().Name;
            int idx = name.LastIndexOf(CommandSuffix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                name = name.Substring(0, idx);
            }
            if (!String.IsNullOrEmpty(name))
            {
                return new CommandAttribute(name, LocalizedResourceManager.GetString("DefaultCommandDescription"));
            }
            return null;
        }
    }
}
