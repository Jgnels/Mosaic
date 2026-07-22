using System;
using System.Collections.Generic;

namespace Dagmay.IntegrationHarness
{
    internal sealed class HarnessAssertionException : Exception
    {
        public HarnessAssertionException(string message)
            : base(message)
        {
        }
    }

    internal sealed class HarnessAssert
    {
        private readonly List<string> _assertions = new List<string>();

        public IReadOnlyList<string> Assertions => _assertions;

        public void True(bool condition, string message)
        {
            if (!condition) throw new HarnessAssertionException(message);
            _assertions.Add(message);
        }

        public void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new HarnessAssertionException(
                    $"{message} Expected={expected}; Actual={actual}.");
            }

            _assertions.Add(message);
        }
    }
}
