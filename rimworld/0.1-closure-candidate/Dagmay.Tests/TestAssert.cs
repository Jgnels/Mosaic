using System;
using System.Collections.Generic;

namespace Dagmay.Tests
{
    internal static class TestAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
            }
        }

        public static void Approximately(double expected, double actual, double tolerance, string message)
        {
            if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException($"{message} Expected approximately: {expected}; actual: {actual}.");
            }
        }

        public static TException Throws<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            throw new InvalidOperationException(message);
        }
    }
}
