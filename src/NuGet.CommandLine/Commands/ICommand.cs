// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    [InheritedExport]
    public interface ICommand
    {
        CommandAttribute CommandAttribute { get; }

        IList<string> Arguments { get; }

        Task Execute();

        /// <summary>
        /// Returns a value indicating whether the specified option should be included in 
        /// the output of the help command.
        /// </summary>
        /// <param name="optionName">The name of the option.</param>
        /// <returns>True if the option should be included in the output of the help command;
        /// otherwise, false.</returns>
        bool IncludedInHelp(string optionName);
    }
}
