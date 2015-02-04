using NuGet.Client;
using NuGet.CommandLine.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommandResourceType), "install", "InstallCommandDescription",
        MinArgs = 0, MaxArgs = 1, UsageSummaryResourceName = "InstallCommandUsageSummary",
        UsageDescriptionResourceName = "InstallCommandUsageDescription",
        UsageExampleResourceName = "InstallCommandUsageExamples")]
    public class InstallCommand : DownloadCommandBase
    {
        [Option(typeof(NuGetCommandResourceType), "InstallCommandVersionDescription")]
        public string Version { get; set; }

        [Option(typeof(NuGetCommandResourceType), "InstallCommandPrerelease")]
        public bool Prerelease { get; set; }

        [Option(typeof(NuGetCommandResourceType), "InstallCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommandResourceType), "InstallCommandDependencyBehavior")]
        public DependencyBehavior DependencyBehavior { get; set; }

        [ImportingConstructor]
        public InstallCommand()
            : base()
        {
            DependencyBehavior = DependencyBehavior.Lowest;
        }

        public async override Task ExecuteCommand()
        {
            CalculateEffectivePackageSaveMode();
            string installPath = ResolveInstallPath();

            var packageSourceProvider = new NuGet.Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, ResourceProviders);

            var primarySourceRepository = GetPrimarySourceRepository(Source, sourceRepositoryProvider);

            // BUGBUG: Check that the argument is always passed
            string packageId = Arguments[0];
            NuGetPackageManager packageManager = new NuGetPackageManager(sourceRepositoryProvider, installPath);
            ResolutionContext resolutionContext = new ResolutionContext(dependencyBehavior: DependencyBehavior, includePrelease: Prerelease);
            FolderNuGetProject nugetProject = new FolderNuGetProject(installPath);
            nugetProject.PackageSaveMode = EffectivePackageSaveMode;

            if (Version == null)
            {
                await packageManager.InstallPackageAsync(nugetProject, packageId, resolutionContext, new Common.Console(),
                    primarySourceRepository, null, CancellationToken.None);
            }
            else
            {
                await packageManager.InstallPackageAsync(nugetProject, new PackageIdentity(packageId, new NuGetVersion(Version)), resolutionContext,
                    new Common.Console(), primarySourceRepository, null, CancellationToken.None);
            }           
        }

        internal string ResolveInstallPath()
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                // Use the OutputDirectory if specified.
                return OutputDirectory;
            }

            ISettings currentSettings = Settings;
            string installPath = currentSettings.GetValue(CommandLineConstants.ConfigSection, "repositoryPath", isPath: true);

            if (!String.IsNullOrEmpty(installPath))
            {
                installPath = installPath.Replace('/', Path.DirectorySeparatorChar);
                // If a value is specified in config, use that. 
                return installPath;
            }

            // Use the current directory as output.
            return Directory.GetCurrentDirectory();
        }
    }
}
