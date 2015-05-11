// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommandResourceType), "list", "ListCommandDescription",
       UsageSummaryResourceName = "ListCommandUsageSummary", UsageDescriptionResourceName = "ListCommandUsageDescription",
       UsageExampleResourceName = "ListCommandUsageExamples")]
    public class ListCommand : Command
    {
        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommandResourceType), "ListCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommandResourceType), "ListCommandAllVersionsDescription")]
        public bool AllVersions { get; set; }

        [Option(typeof(NuGetCommandResourceType), "ListCommandPrerelease")]
        public bool Prerelease { get; set; }

        public async override Task ExecuteCommand()
        {
            IEnumerable<SourceRepository> sourceRepository;
            string searchTerm = Arguments != null ? Arguments.FirstOrDefault() : null;
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

            foreach (var eachRepository in sourceRepository)
            {
                var searchResource = await eachRepository.GetResourceAsync<SimpleSearchResource>();
                if (searchResource != null)
                {
                    int page = 100;
                    int skip = 0;

                    do
                    {
                        var packages = await searchResource.Search(searchTerm, new SearchFilter() { IncludePrerelease = Prerelease, SupportedFrameworks = new string[0] }, skip, page, CancellationToken.None);
                        skip += page;
                        if (!packages.Any()) break;
                        PrintPackages(packages);
                    }
                    while (true);

                }
            }
        }

        private void PrintPackages(IEnumerable<SimpleSearchMetadata> packages)
        {
            Action<string, string, SimpleSearchMetadata> funcPrintPackage = Verbosity == Verbosity.Detailed ? (Action<string, string, SimpleSearchMetadata>)PrintPackageDetailed : PrintPackage;

            if (packages != null && packages.Any())
            {
                foreach (var p in packages)
                {
                    if (AllVersions)
                    {
                        //will implement later
                    }
                    else
                    {
                        funcPrintPackage(p.Identity.Id.ToString(), p.Identity.Version.ToString(), p);
                    }
                }
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString("ListCommandNoPackages"));
            }

        }

        private void PrintPackageDetailed(string packageId, string version, SimpleSearchMetadata package)
        {
            /***********************************************
            * Package-Name
            *  1.0.0.2010
            *  This is the package Summary
            * 
            * Package-Name-Two
            *  2.0.0.2010
            *  This is the second package Summary
            ***********************************************/
            Console.PrintJustified(0, packageId);
            Console.PrintJustified(1, version);
            Console.PrintJustified(1, package.Description.ToString() ?? string.Empty);
            Console.WriteLine();
        }

        private void PrintPackage(string packageId, string version, SimpleSearchMetadata package)
        {
            /***********************************************
            * Package-Name 1.0.0.2010
            * Package-Name-Two 2.0.0.2010
            ***********************************************/
            Console.PrintJustified(0, packageId + " " + version);
        }
    }
}
