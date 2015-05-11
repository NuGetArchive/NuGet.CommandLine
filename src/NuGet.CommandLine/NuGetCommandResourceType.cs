// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Resources;

namespace NuGet.CommandLine
{
    internal class NuGetCommandResourceType
    {
        private static readonly ResourceManager resourceMan = new ResourceManager("NuGet.CommandLine.NuGetCommandResourceType", typeof(NuGetCommandResourceType).Assembly);

        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        internal static ResourceManager ResourceManager
        {
            get
            {
                return resourceMan;
            }
        }
    }
}
