// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Reflection;
using System.Threading;

namespace NuGet.CommandLine
{
    public static class ExceptionUtility
    {
        public static Exception Unwrap(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            if (exception.InnerException == null)
            {
                return exception;
            }

            // Always return the inner exception from a target invocation exception
            if (exception is AggregateException ||
                exception is TargetInvocationException)
            {
                return exception.GetBaseException();
            }

            return exception;
        }

        public static void RethrowIfCritical(this Exception e)
        {
            if (e != null && IsCriticalException(e))
            {
                e.Rethrow();
            }
        }

        /// <summary>
        /// Determines if an exception is critical and should not be caught.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <returns>True if the exception should not be caught.</returns>
        public static bool IsCriticalException(this Exception e)
        {
            return e is StackOverflowException
                  || e is OutOfMemoryException
                  || e is ThreadAbortException
                  || e is ThreadInterruptedException
                  || e is AccessViolationException
                  || e is NullReferenceException;
        }

        /// <summary>
        /// Rethrows an exception, preserving its original call stack.
        /// </summary>
        /// <param name="e">The exception.</param>
        public static void Rethrow(this Exception e)
        {
            if (e != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
            }
        }
    }
}
