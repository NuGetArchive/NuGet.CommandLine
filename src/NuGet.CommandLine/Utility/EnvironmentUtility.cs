// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGet.CommandLine
{
    public static class EnvironmentUtility
    {
        private static bool _runningFromCommandLine;
        private static readonly bool _isMonoRuntime = Type.GetType("Mono.Runtime") != null;

        public static bool IsMonoRuntime
        {
            get
            {
                return _isMonoRuntime;
            }
        }

        public static bool RunningFromCommandLine
        {
            get
            {
                return _runningFromCommandLine;
            }
        }

        public static bool IsNet45Installed
        {
            get
            {
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Microsoft.Win32.RegistryView.Registry32))
                {
                    using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                    {
                        if (key == null)
                        {
                            return false;
                        }

                        object releaseKey = key.GetValue("Release");
                        return releaseKey is int && (int)releaseKey >= 378389;
                    }
                }
            }
        }

        // this will be called from nuget.exe
        public static void SetRunningFromCommandLine()
        {
            _runningFromCommandLine = true;
        }
    }
}
