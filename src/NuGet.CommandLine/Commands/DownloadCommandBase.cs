using NuGet.Client;
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
            else
            {
                // Default to nupkg only
                EffectivePackageSaveMode = PackageSaveModes.Nupkg;
            }
        }  

        protected static SourceRepository GetPrimarySourceRepository(ICollection<string> sources,
            SourceRepositoryProvider sourceRepositoryProvider)
        {
            // TODO: Consider adding support for multiple primary repositories
            string primarySource = null;
            SourceRepository primarySourceRepository = null;
            if(sources != null && sources.Any())
            {
                primarySource = sources.First();
            }

            if(!String.IsNullOrEmpty(primarySource))
            {
                primarySourceRepository = sourceRepositoryProvider.CreateRepository(new Configuration.PackageSource(primarySource));
            }
            else
            {
                primarySourceRepository = sourceRepositoryProvider.GetRepositories().Where(s => s.PackageSource.IsEnabled).FirstOrDefault();
            }

            if(primarySourceRepository == null)
            {
                throw new InvalidOperationException(NuGetResources.DownloadCommandBaseNoEnabledPackageSource);
            }

            return primarySourceRepository;
        }

        protected static IEnumerable<SourceRepository> GetSourceRepositories(ICollection<string> sources,
            SourceRepositoryProvider sourceRepositoryProvider)
        {
            if (sources != null && sources.Any())
            {
                List<SourceRepository> sourceRepositories = new List<SourceRepository>();
                foreach(var source in sources)
                {
                    sourceRepositories.Add(sourceRepositoryProvider.CreateRepository(new NuGet.Configuration.PackageSource(source)));
                }

                return sourceRepositories;
            }

            return null;
        }
    }  
}
