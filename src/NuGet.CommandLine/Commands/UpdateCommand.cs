using NuGet.Client;
using NuGet.CommandLine.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommandResourceType), "update", "UpdateCommandDescription", UsageSummary = "<packages.config|solution|project>",
      UsageExampleResourceName = "UpdateCommandUsageExamples")]
    public class UpdateCommand : Command
    {
        private readonly List<string> _sources = new List<string>();
        [Option(typeof(NuGetCommandResourceType), "UpdateCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }
        public async override Task ExecuteCommand()
        {
            IEnumerable<SourceRepository> sourceRepository;
            if (Source.Any())
            {
                List<SourceRepository> sourceList = new List<SourceRepository>();
                foreach (var source in Source)
                {
                    sourceList.Add(new SourceRepository(new PackageSource(source), ResourceProviders));
                }
                sourceRepository = sourceList;
            }
            else
            {
                var packageSourceProvider = new PackageSourceProvider(Settings);
                var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, ResourceProviders);
                sourceRepository = sourceRepositoryProvider.GetRepositories();
            }

            var selfUpdater = new SelfUpdater(sourceRepository.FirstOrDefault())
            {
                Console = Console
            };

            await selfUpdater.UpdateSelf();
        }
    }
}
