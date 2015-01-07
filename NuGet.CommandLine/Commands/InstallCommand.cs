using NuGet.Resolver;
using System.ComponentModel.Composition;

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
           
        }

        public override void ExecuteCommand()
        {
          
        }       
    }
}
