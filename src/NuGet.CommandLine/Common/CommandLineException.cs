// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace NuGet.CommandLine
{
    [Serializable]
    public class CommandLineException : Exception
    {
        public CommandLineException()
        {
        }

        public CommandLineException(string message)
            : base(message)
        {
        }

        public CommandLineException(string format, params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public CommandLineException(string message, Exception exception)
            : base(message, exception)
        {
        }

        protected CommandLineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
