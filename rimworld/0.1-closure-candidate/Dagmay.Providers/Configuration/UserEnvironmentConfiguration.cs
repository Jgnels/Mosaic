using System;
using System.Security;

namespace Dagmay.Providers.Configuration
{
    /// <summary>
    /// Reads a Windows user's current persisted environment configuration before
    /// falling back to the environment inherited by the running process.
    /// </summary>
    public static class UserEnvironmentConfiguration
    {
        public static string? Read(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("An environment-variable name is required.", nameof(variableName));
            }

            string? userValue = null;
            try
            {
                userValue = Environment.GetEnvironmentVariable(
                    variableName.Trim(),
                    EnvironmentVariableTarget.User);
            }
            catch (NotSupportedException)
            {
                // Some non-Windows runtimes do not expose a user environment store.
            }
            catch (SecurityException)
            {
                // A restricted host may deny access; the inherited process value remains usable.
            }

            return Select(userValue, Environment.GetEnvironmentVariable(variableName.Trim()));
        }

        public static string? Select(string? userValue, string? processValue)
        {
            if (!string.IsNullOrWhiteSpace(userValue)) return userValue!.Trim();
            return string.IsNullOrWhiteSpace(processValue) ? null : processValue!.Trim();
        }
    }
}
