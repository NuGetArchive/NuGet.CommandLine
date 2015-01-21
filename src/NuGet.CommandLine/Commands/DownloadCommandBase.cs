using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;


namespace NuGet.CommandLine.Commands
{
    public abstract class DownloadCommandBase : Command
    {
        private readonly List<string> _sources = new List<string>();

        protected PackageSaveModes EffectivePackageSaveMode { get; set; }

        protected DownloadCommandBase()
        {
           
        }

        [Option(typeof(NuGetCommandResourceType), "CommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommandResourceType), "CommandNoCache")]
        public bool NoCache { get; set; }

        [Option(typeof(NuGetCommandResourceType), "CommandDisableParallelProcessing")]
        public bool DisableParallelProcessing { get; set; }

        [Option(typeof(NuGetCommandResourceType), "CommandPackageSaveMode")]
        public string PackageSaveMode { get; set; }

        protected void CalculateEffectivePackageSaveMode()
        {
            string packageSaveModeValue = PackageSaveMode;
            if (string.IsNullOrEmpty(packageSaveModeValue))
            {
                var settingValue = Settings.GetSettingValues("PackageSaveMode");
                if (settingValue.Any())
                {
                    packageSaveModeValue = settingValue.FirstOrDefault().Value;
                }
            }

            EffectivePackageSaveMode = PackageSaveModes.None;
            if (!string.IsNullOrEmpty(packageSaveModeValue))
            {
                foreach (var v in packageSaveModeValue.Split(';'))
                {
                    if (v.Equals(PackageSaveModes.Nupkg.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= PackageSaveModes.Nupkg;
                    }
                    else if (v.Equals(PackageSaveModes.Nuspec.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= PackageSaveModes.Nuspec;
                    }
                    else
                    {
                        string message = String.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_InvalidPackageSaveMode"),
                            v);
                        Console.WriteWarning(message);
                    }
                }
            }
        }  
    }  
}
