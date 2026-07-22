using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dagmay.Core.Contracts
{
    internal static class ContractGuard
    {
        public static string Text(string value, string parameterName, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }

        public static double UnitInterval(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between 0 and 1.");
            }

            return value;
        }

        public static IReadOnlyList<T> List<T>(IEnumerable<T> values, string parameterName)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            return new ReadOnlyCollection<T>(new List<T>(values));
        }

        public static IReadOnlyDictionary<string, string> Dictionary(
            IDictionary<string, string> values,
            string parameterName)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values, StringComparer.Ordinal));
        }
    }
}

