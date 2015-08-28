// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Client;
using NuGet.CommandLine.Commands;
using NuGet.CommandLine.Common;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGet.CommandLine
{
    public class Program
    {
        private const string NuGetExtensionsKey = "NUGET_EXTENSIONS_PATH";
        private static readonly string ExtensionsDirectoryRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Commands");

        [Import]
        public HelpCommand HelpCommand { get; set; }

        [ImportMany(AllowRecomposition = true)]
        public IEnumerable<ICommand> Commands { get; set; }

        [Import]
        public ICommandManager Manager { get; set; }

        /// <summary>
        /// Flag meant for unit tests that prevents command line extensions from being loaded.
        /// </summary>
        public static bool IgnoreExtensions { get; set; }

        public static int Main(string[] args)
        {
            WaitForAttach(ref args);

            // This is to avoid applying weak event pattern usage, which breaks under Mono or restricted environments, e.g. Windows Azure Web Sites.
            //EnvironmentUtility.SetRunningFromCommandLine();

            // set output encoding to UTF8 if -utf8 is specified
            var oldOutputEncoding = System.Console.OutputEncoding;
            if (args.Any(arg => String.Equals(arg, "-utf8", StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Where(arg => !String.Equals(arg, "-utf8", StringComparison.OrdinalIgnoreCase)).ToArray();
                SetConsoleOutputEncoding(System.Text.Encoding.UTF8);
            }

            var console = new Common.Console();
            //var fileSystem = new PhysicalFileSystem(Directory.GetCurrentDirectory());

            Func<Exception, string> getErrorMessage = e => e.Message;

            try
            {
                // Remove NuGet.exe.old
                RemoveOldFile();

                // Import Dependencies  
                var p = new Program();
                p.Initialize(console);

                // Add commands to the manager
                foreach (ICommand cmd in p.Commands)
                {
                    p.Manager.RegisterCommand(cmd);
                }

                CommandLineParser parser = new CommandLineParser(p.Manager);

                // Parse the command
                ICommand command = parser.ParseCommandLine(args) ?? p.HelpCommand;

                // Fallback on the help command if we failed to parse a valid command
                if (command == null || !ArgumentCountValid(command))
                {
                    // Get the command name and add it to the argument list of the help command
                    string commandName = command.CommandAttribute.CommandName;

                    // Print invalid command then show help
                    console.WriteLine(LocalizedResourceManager.GetString("InvalidArguments"), commandName);

                    p.HelpCommand.ViewHelpForCommand(commandName);
                }
                else
                {
                    SetConsoleInteractivity(console, command as Command);

                    // When we're detailed, get the whole exception including the stack
                    // This is useful for debugging errors.
                    if (console.Verbosity == Verbosity.Detailed)
                    {
                        getErrorMessage = e => e.ToString();
                    }

                    command.Execute().Wait();
                }
            }
            catch (AggregateException exception)
            {
                string message;
                Exception unwrappedEx = ExceptionUtility.Unwrap(exception);
                if (unwrappedEx == exception)
                {
                    // If the AggregateException contains more than one InnerException, it cannot be unwrapped. In which case, simply print out individual error messages
                    message = String.Join(Environment.NewLine, exception.InnerExceptions.Select(getErrorMessage).Distinct(StringComparer.CurrentCulture));
                }
                else
                {

                    message = getErrorMessage(ExceptionUtility.Unwrap(exception));
                }
                console.WriteError(message);
                return 1;
            }
            catch (Exception e)
            {
                console.WriteError(getErrorMessage(ExceptionUtility.Unwrap(e)));
                return 1;
            }
            finally
            {
                //TODO: add OptimizedZipPackage.PurgeCache() here later
                SetConsoleOutputEncoding(oldOutputEncoding);
            }

            return 0;
        }

        [Conditional("DEBUG")]
        internal static void WaitForAttach(ref string[] args)
        {
            if (args.Length > 0 && (String.Equals(args[0], "dbg", StringComparison.OrdinalIgnoreCase) || String.Equals(args[0], "debug", StringComparison.OrdinalIgnoreCase)))
            {
                args = args.Skip(1).ToArray();
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch();
                }
            }
        }

        private static void SetConsoleOutputEncoding(System.Text.Encoding encoding)
        {
            try
            {
                System.Console.OutputEncoding = encoding;
            }
            catch (IOException)
            {
            }
        }

        private void Initialize(IConsole console)
        {
            var assemblies = new List<string>();
            assemblies.Add(GetType().Assembly.Location);
            assemblies.AddRange(Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"));

            if (!IgnoreExtensions)
            {
                AddExtensionsToCatalog(assemblies, console);
            }

            var catalog = CreateCatalog(assemblies, console);
            var container = new CompositionContainer(catalog);
            container.ComposeExportedValue<IConsole>(console);
            container.ComposeParts(this);
        }


        /// <summary>
        /// Creates a catalog for the assemblies
        /// </summary>
        /// <param name="assemblyFiles">the assemblies to include in the catalog</param>
        /// <returns>the catalog containing parts from the assemblies</returns>
        public static ComposablePartCatalog CreateCatalog(IReadOnlyList<string> assemblyFiles, IConsole console)
        {
            AggregateCatalog result = new AggregateCatalog();
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filename in assemblyFiles)
            {
                // skip files we've already seen
                if (!nameSet.Add(filename))
                {
                    continue;
                }
                var assembly = GetOrLoad(filename, console);
                if (assembly != null)
                {
                    var assemblyCatalog = CreateCatalog(assembly, console);
                    if (assemblyCatalog != null)
                    {
                        result.Catalogs.Add(assemblyCatalog);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a catalog for the given assembly
        /// </summary>
        /// <param name="assembly">the assembly</param>
        /// <returns>the catalog containing parts from the assembly</returns>
        private static ComposablePartCatalog CreateCatalog(Assembly assembly, IConsole console)
        {
            IEnumerable<Type> types;

            try
            {
                ComposablePartCatalog result = new AssemblyCatalog(assembly);

                if (result == null)
                {
                    return null;
                }
                // Trigger the loading of the assembly.  This will catch on class of error where
                // other supporting assemblies are missing.
                result.Any();
                return result;
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
                console.WriteWarning(e.Message);
            }

            // In the event there was a problem press on and create a catalog with all types we could
            // load.  Note however that we have to go further and try to get the imports/exports as doing
            // that will completely exercise the MEF loading pass.  Any types that can load all their imports
            // and exports are added to a catalog and included in the container.
            var loadableTypes = new List<Type>();
            foreach (var type in types.Where(t => t != null))
            {
                try
                {
                    foreach (var tempCatalog in new TypeCatalog(type))
                    {
                        // Trigger loading exceptions now rather than later.
                        var dummy1 = tempCatalog.ExportDefinitions.Any();
                        var dummy2 = tempCatalog.ImportDefinitions.Any();
                    }
                    loadableTypes.Add(type);
                }
                catch (Exception e)
                {
                    e.RethrowIfCritical();
                    // In most cases this will be irrelevant problems but it is possible that there is a real problem.
                    console.WriteWarning(e.Message);
                }
            }

            return new TypeCatalog(loadableTypes);
        }

        private static Assembly GetOrLoad(string assembly, IConsole console)
        {
            try
            {
                AssemblyName name = AssemblyName.GetAssemblyName(assembly);
                name.CodeBase = assembly;
                return Assembly.Load(name);
            }
            catch (Exception e)
            {
                e.RethrowIfCritical();
                console.WriteWarning(e.Message);
            }
            return null;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want to block the exe from usage if anything failed")]
        internal static void RemoveOldFile()
        {
            string oldFile = typeof(Program).Assembly.Location + ".old";
            try
            {
                if (File.Exists(oldFile))
                {
                    File.Delete(oldFile);
                }
            }
            catch
            {
                // We don't want to block the exe from usage if anything failed
            }
        }

        public static bool ArgumentCountValid(ICommand command)
        {
            CommandAttribute attribute = command.CommandAttribute;
            return command.Arguments.Count >= attribute.MinArgs &&
                   command.Arguments.Count <= attribute.MaxArgs;
        }

        private static void AddExtensionsToCatalog(List<string> assemblies, IConsole console)
        {
            IEnumerable<string> directories = new[] { ExtensionsDirectoryRoot };

            var customExtensions = Environment.GetEnvironmentVariable(NuGetExtensionsKey);
            if (!String.IsNullOrEmpty(customExtensions))
            {
                // Add all directories from the environment variable if available.
                directories = directories.Concat(customExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            IEnumerable<string> files;
            foreach (var directory in directories)
            {
                if (Directory.Exists(directory))
                {
                    files = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories);
                    RegisterExtensions(assemblies, files, console);
                }
            }

            // Ideally we want to look for all files. However, using MEF to identify imports results in assemblies being loaded and locked by our App Domain
            // which could be slow, might affect people's build systems and among other things breaks our build. 
            // Consequently, we'll use a convention - only binaries ending in the name Extensions would be loaded. 
            var nugetDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            files = Directory.EnumerateFiles(nugetDirectory, "*Extensions.dll");
            RegisterExtensions(assemblies, files, console);
        }

        private static void RegisterExtensions(List<string> assemblies, IEnumerable<string> enumerateFiles, IConsole console)
        {
            foreach (var item in enumerateFiles)
            {
                try
                {
                    assemblies.Add(item);
                }
                catch (BadImageFormatException ex)
                {
                    // Ignore if the dll wasn't a valid assembly
                    console.WriteWarning(ex.Message);
                }
                catch (FileLoadException ex)
                {
                    // Ignore if we couldn't load the assembly.
                    console.WriteWarning(ex.Message);
                }
            }
        }

        private static void SetConsoleInteractivity(IConsole console, Command command)
        {
            // Global environment variable to prevent the exe for prompting for credentials
            string globalSwitch = Environment.GetEnvironmentVariable("NUGET_EXE_NO_PROMPT");

            // When running from inside VS, no input is available to our executable locking up VS.
            // VS sets up a couple of environment variables one of which is named VisualStudioVersion. 
            // Every time this is setup, we will just fail.
            // TODO: Remove this in next iteration. This is meant for short-term backwards compat.
            string vsSwitch = Environment.GetEnvironmentVariable("VisualStudioVersion");

            console.IsNonInteractive = !String.IsNullOrEmpty(globalSwitch) ||
                                       !String.IsNullOrEmpty(vsSwitch) ||
                                       (command != null && command.NonInteractive);

            string forceInteractive = Environment.GetEnvironmentVariable("FORCE_NUGET_EXE_INTERACTIVE");
            if (!String.IsNullOrEmpty(forceInteractive))
            {
                console.IsNonInteractive = false;
            }

            if (command != null)
            {
                console.Verbosity = command.Verbosity;
            }
        }
    }
}
