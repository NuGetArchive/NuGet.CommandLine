using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.CommandLine
{
    internal class MSBuildSolutionParser : ISolutionParser
    {
        public IEnumerable<string> GetAllProjectFileNames(string solutionFile)
        {
            var solution = new Solution(solutionFile);
            var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionFile));

            return solution.Projects.Where(p => !p.IsSolutionFolder)
                .Select(p => Path.Combine(solutionDirectory, p.RelativePath));
        }
    }
}
