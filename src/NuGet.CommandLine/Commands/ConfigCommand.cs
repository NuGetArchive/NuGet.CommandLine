using NuGet.CommandLine.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Commands
{
    public class ConfigCommand : Command
    {
        private readonly Dictionary<string, string> _setValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [Option(typeof(NuGetCommandResourceType), "ConfigCommandSetDesc")]
        public Dictionary<string, string> Set
        {
            get { return _setValues; }
        }

        [Option(typeof(NuGetCommandResourceType), "ConfigCommandAsPathDesc")]
        public bool AsPath
        {
            get;
            set;
        }

        public override Task ExecuteCommand()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SettingsIsNull"));
            }

            string getKey = Arguments.FirstOrDefault();
            if (Set.Any())
            {
                foreach (var property in Set)
                {
                    if (String.IsNullOrEmpty(property.Value))
                    {
                        Settings.DeleteValue(CommandLineConstants.ConfigSection, property.Key);
                    }
                    else
                    {
                        //TODO: need encrypt for password
                        Settings.SetValue(CommandLineConstants.ConfigSection,property.Key, property.Value);
                    }
                }
            }
            else if (!String.IsNullOrEmpty(getKey))
            {
                string value = Settings.GetValue(CommandLineConstants.ConfigSection,getKey, isPath: AsPath);
                if (String.IsNullOrEmpty(value))
                {
                    Console.WriteWarning(LocalizedResourceManager.GetString("ConfigCommandKeyNotFound"), getKey);
                }
                else
                {
                    Console.WriteLine(value);
                }
            }

            return Task.FromResult(0);
        }
    }
}
