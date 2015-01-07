using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    [Export(typeof(IMachineWideSettings))]
    public class CommandLineMachineWideSettings : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        public CommandLineMachineWideSettings()
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _settings = new Lazy<IEnumerable<Settings>>(
                () => Configuration.Settings.LoadMachineWideSettings(
                    baseDirectory));
        }

        public IEnumerable<Settings> Settings
        {
            get
            {
                return _settings.Value;
            }
        }
    }
}
