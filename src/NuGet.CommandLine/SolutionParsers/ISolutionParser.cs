using System.Collections.Generic;

namespace NuGet.CommandLine
{
    internal interface ISolutionParser
    {
        IEnumerable<string> GetAllProjectFileNames(string solutionFile);
    }
}
