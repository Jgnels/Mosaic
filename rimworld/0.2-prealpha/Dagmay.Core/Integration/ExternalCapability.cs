using System;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Integration
{
    /// <summary>
    /// Describes an optional external integration without storing or requiring
    /// live third-party CLR types in durable Mosaic state.
    /// </summary>
    public sealed class ExternalCapability
    {
        public ExternalCapability(
            string sourceModId,
            string capabilityId,
            bool available,
            string? sourceVersion = null,
            string? apiVersion = null)
        {
            SourceModId = ContractGuard.Text(sourceModId, nameof(sourceModId), 256);
            CapabilityId = ContractGuard.Text(capabilityId, nameof(capabilityId), 256);
            Available = available;
            SourceVersion = Normalize(sourceVersion, 128, nameof(sourceVersion));
            ApiVersion = Normalize(apiVersion, 128, nameof(apiVersion));
        }

        public string SourceModId { get; }
        public string CapabilityId { get; }
        public bool Available { get; }
        public string? SourceVersion { get; }
        public string? ApiVersion { get; }

        private static string? Normalize(string? value, int maxLength, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return ContractGuard.Text(value, parameterName, maxLength);
        }
    }
}
