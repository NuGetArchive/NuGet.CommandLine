using NuGet.Client;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommandResourceType), "restore", "RestoreCommandDescription",
        MinArgs = 0, MaxArgs = 1, UsageSummaryResourceName = "RestoreCommandUsageSummary",
        UsageDescriptionResourceName = "RestoreCommandUsageDescription",
        UsageExampleResourceName = "RestoreCommandUsageExamples")]
    public class RestoreCommand : DownloadCommandBase
    {
        public const string PackagesFolder = "packages";
        // True means we're restoring for a solution; False means we're restoring packages
        // listed in a packages.config file.
        private bool _restoringForSolution;

        private string _solutionFileFullPath;
        private string _packagesConfigFileFullPath;

        // A flag indicating if the opt-out message should be displayed.
        private bool _outputOptOutMessage;

        // lock used to access _outputOptOutMessage.
        private readonly object _outputOptOutMessageLock = new object();

        [Option(typeof(NuGetCommandResourceType), "RestoreCommandRequireConsent")]
        public bool RequireConsent { get; set; }

        [Option(typeof(NuGetCommandResourceType), "RestoreCommandPackagesDirectory", AltName = "OutputDirectory")]
        public string PackagesDirectory { get; set; }

        [Option(typeof(NuGetCommandResourceType), "RestoreCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal bool RestoringForSolution
        {
            get { return _restoringForSolution; }
        }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal string SolutionFileFullPath
        {
            get { return _solutionFileFullPath; }
        }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal string PackagesConfigFileFullPath
        {
            get { return _packagesConfigFileFullPath; }
        }

        [ImportingConstructor]
        public RestoreCommand()
        {
            _outputOptOutMessage = true;
        }

        public override void ExecuteCommand()
        {
            CalculateEffectivePackageSaveMode();
            DetermineRestoreMode();
            if (_restoringForSolution && !String.IsNullOrEmpty(SolutionDirectory))
            {
                // option -SolutionDirectory is not valid when we are restoring packages for a solution
                throw new InvalidOperationException(LocalizedResourceManager.GetString("RestoreCommandOptionSolutionDirectoryIsInvalid"));
            }
            
            string packagesFolderPath = GetPackagesFolderPath();
            var packageSourceProvider = new NuGet.Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, ResourceProviders);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, packagesFolderPath);
            HashSet<PackageReference> installedPackageReferences;
            Stopwatch watch = new Stopwatch();
            if (_restoringForSolution)
            {                
                watch.Restart();
                installedPackageReferences = GetInstalledPackageReferencesFromSolutionFile(_solutionFileFullPath);
                watch.Stop();
                DisplayExecutedTime(watch.Elapsed, "GetInstalledPackageReferencesFromSolution");
            }
            else
            {
                // By default the PackageReferenceFile does not throw if the file does not exist at the specified path.
                // So we'll need to verify that the file exists.
                if (!File.Exists(_packagesConfigFileFullPath))
                {
                    string message = String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("RestoreCommandFileNotFound"), _packagesConfigFileFullPath);
                    throw new InvalidOperationException(message);
                }

                watch.Restart();
                installedPackageReferences = GetInstalledPackageReferences(_packagesConfigFileFullPath);
                watch.Stop();
                DisplayExecutedTime(watch.Elapsed, "GetInstalledPackageReferences");
            }

            watch.Restart();
            var missingPackages = PackageRestoreManager.GetMissingPackages(nuGetPackageManager, installedPackageReferences);
            watch.Stop();
            DisplayExecutedTime(watch.Elapsed, "GetMissingPackages");

            watch.Restart();
            PackageRestoreManager.RestoreMissingPackages(nuGetPackageManager, missingPackages, Console, CancellationToken.None, null, GetSourceRepositories(Source, sourceRepositoryProvider)).Wait();
            watch.Stop();
            DisplayExecutedTime(watch.Elapsed, "RestorePackages");
        }

        internal void DetermineRestoreMode()
        {
            if (Arguments.Count == 0)
            {
                // look for solution files first
                _solutionFileFullPath = GetSolutionFile("");
                if (_solutionFileFullPath != null)
                {
                    _restoringForSolution = true;
                    if (Verbosity == Verbosity.Detailed)
                    {
                        Console.WriteLine(LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesForSolution"), _solutionFileFullPath);
                    }

                    return;
                }

                // look for packages.config file
                if (File.Exists(Constants.PackageReferenceFile))
                {
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(Constants.PackageReferenceFile);
                    if (Verbosity == Verbosity.Detailed)
                    {
                        Console.WriteLine(LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesFromPackagesConfigFile"));
                    }

                    return;
                }

                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_NoSolutionFileNorePackagesConfigFile"));
            }
            else
            {
                if (Path.GetFileName(Arguments[0]).Equals(Constants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase))
                {
                    // restoring from packages.config file
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(Arguments[0]);
                }
                else
                {
                    _restoringForSolution = true;
                    _solutionFileFullPath = GetSolutionFile(Arguments[0]);
                    if (_solutionFileFullPath == null)
                    {
                        throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_CannotLocateSolutionFile"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the solution file, in full path format. If <paramref name="solutionFileOrDirectory"/> is a file, 
        /// that file is returned. Otherwise, searches for a *.sln file in
        /// directory <paramref name="solutionFileOrDirectory"/>. If exactly one sln file is found, 
        /// that file is returned. If multiple sln files are found, an exception is thrown. 
        /// If no sln files are found, returns null.
        /// </summary>
        /// <param name="solutionFileOrDirectory">The solution file or directory to search for solution files.</param>
        /// <returns>The full path of the solution file. Or null if no solution file can be found.</returns>
        private string GetSolutionFile(string solutionFileOrDirectory)
        {
            if (File.Exists(solutionFileOrDirectory))
            {
                return Path.GetFullPath(solutionFileOrDirectory);
            }

            // look for solution files
            var slnFiles = Directory.EnumerateFiles(Path.GetDirectoryName(solutionFileOrDirectory), "*.sln").ToArray();
            if (slnFiles.Length > 1)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_MultipleSolutions"));
            }

            if (slnFiles.Length == 1)
            {
                return Path.GetFullPath(slnFiles[0]);
            }

            return null;
        }

        private void ReadSettings(string solutionDirectory)
        {
            if(String.IsNullOrEmpty(solutionDirectory))
            {
                throw new ArgumentNullException(solutionDirectory);
            }

            // Read the solution-level settings
            string solutionSettingsFolder = Path.Combine(
                solutionDirectory,
                NuGetConstants.NuGetSolutionSettingsFolder);

            if (ConfigFile != null)
            {
                ConfigFile = FileSystemUtility.GetFullPath(solutionSettingsFolder, ConfigFile);
            }

            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(
                solutionSettingsFolder,
                configFileName: ConfigFile,
                machineWideSettings: MachineWideSettings);
        }

        private string GetEffectiveSolutionDirectory()
        {
            return _restoringForSolution ?
                    Path.GetDirectoryName(_solutionFileFullPath) :
                    SolutionDirectory;
        }

        private string GetPackagesFolderPath()
        {
            if (!String.IsNullOrEmpty(PackagesDirectory))
            {
                return PackagesDirectory;
            }

            // Packages folder needs to be inferred from SolutionFilePath or SolutionDirectory
            var effectiveSolutionDirectory = GetEffectiveSolutionDirectory();
            if(!String.IsNullOrEmpty(effectiveSolutionDirectory))
            {
                ReadSettings(effectiveSolutionDirectory);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(effectiveSolutionDirectory, Settings);
                if (!String.IsNullOrEmpty(packagesFolderPath))
                {
                    return packagesFolderPath;
                }
            }

            throw new InvalidOperationException(LocalizedResourceManager.GetString("RestoreCommandCannotDeterminePackagesFolder"));
        }

        private HashSet<PackageReference> GetInstalledPackageReferencesFromSolutionFile(string solutionFileFullPath)
        {
            ISolutionParser solutionParser;
            if (EnvironmentUtility.IsMonoRuntime)
            {
                solutionParser = new XBuildSolutionParser();
            }
            else
            {
                solutionParser = new MSBuildSolutionParser();
            }

            var installedPackageReferences = new HashSet<PackageReference>(new PackageReferenceComparer());
            IEnumerable<string> projectFiles = Enumerable.Empty<string>();
            try
            {
                projectFiles = solutionParser.GetAllProjectFileNames(solutionFileFullPath);
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                //if (ex.InnerException is InvalidProjectFileException)
                //{
                //    return GetPackageReferencesInDirectory(Path.GetDirectoryName(solutionFileFullPath));
                //}

                throw;
            }

            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    Console.WriteWarning(LocalizedResourceManager.GetString("RestoreCommandProjectNotFound"), projectFile);
                    continue;
                }

                string projectConfigFilePath = Path.Combine(
                    Path.GetDirectoryName(projectFile),
                    Constants.PackageReferenceFile);

                string projectName = Path.GetFileNameWithoutExtension(projectFile);

                CommandLineHelper.AddRange(installedPackageReferences, GetInstalledPackageReferences(projectConfigFilePath));
            }

            return installedPackageReferences;
        }

        private HashSet<PackageReference> GetInstalledPackageReferences(string projectConfigFilePath)
        {
            if (File.Exists(projectConfigFilePath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(projectConfigFilePath));
                return new HashSet<PackageReference>(reader.GetPackages(), new PackageReferenceComparer());
            }

            return new HashSet<PackageReference>(new PackageReferenceComparer());
        }
    }
}
