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
            SourceVersion = ContractGuard.OptionalText(sourceVersion, nameof(sourceVersion), 128);
            ApiVersion = ContractGuard.OptionalText(apiVersion, nameof(apiVersion), 128);
        }

        public string SourceModId { get; }
        public string CapabilityId { get; }
        public bool Available { get; }
        public string? SourceVersion { get; }
        public string? ApiVersion { get; }

    }
}
