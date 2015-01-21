using System.Collections.Generic;
using System.Reflection;
using System;

namespace NuGet.CommandLine
{   
    internal class XBuildSolutionParser : ISolutionParser
    {
        private static MethodInfo GetAllProjectFileNamesMethod()
        {
#pragma warning disable 618
            var assembly = typeof(Microsoft.Build.BuildEngine.Engine).Assembly;
#pragma warning restore 618
            var solutionParserType = assembly.GetType("Mono.XBuild.CommandLine.SolutionParser");
            if (solutionParserType == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("Error_CannotGetXBuildSolutionParser"));
            }

            var methodInfo = solutionParserType.GetMethod(
                "GetAllProjectFileNames", 
                new Type[] {typeof(string) });
            if (methodInfo == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("Error_CannotGetGetAllProjectFileNamesMethod"));
            }

            return methodInfo;
        }

        /// <summary>
        /// Returns the list of project files in the solution file.
        /// </summary>
        /// <param name="solutionFile">The name of the solution file.</param>
        /// <returns>The list of project files in the solution file.</returns>
        public IEnumerable<string> GetAllProjectFileNames(string solutionFile)
        {
            var getAllProjectFileNamesMethod = GetAllProjectFileNamesMethod();
            var names = (IEnumerable<string>)getAllProjectFileNamesMethod.Invoke(
                null, new object[] { solutionFile });
            return names;
        }
    }
}
