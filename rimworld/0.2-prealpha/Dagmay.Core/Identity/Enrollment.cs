using System;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Identity
{
    public sealed class EnvironmentEntityReference : IEquatable<EnvironmentEntityReference>
    {
        public EnvironmentEntityReference(string environment, string externalId, string displayName)
        {
            Environment = ContractGuard.Text(environment, nameof(environment), 128);
            ExternalId = ContractGuard.Text(externalId, nameof(externalId), 512);
            DisplayName = ContractGuard.Text(displayName, nameof(displayName), 256);
        }

        public string Environment { get; }
        public string ExternalId { get; }
        public string DisplayName { get; }

        public bool Equals(EnvironmentEntityReference? other)
        {
            return other is not null
                && string.Equals(Environment, other.Environment, StringComparison.Ordinal)
                && string.Equals(ExternalId, other.ExternalId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as EnvironmentEntityReference);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Environment) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(ExternalId);
            }
        }
    }

    public enum EnrollmentLevel
    {
        WorldActor,
        KnownPerson,
        EnrolledIndividual
    }

    public sealed class EnrollmentRecord
    {
        public EnrollmentRecord(
            EnvironmentEntityReference entity,
            EnrollmentLevel level,
            IndividualId? individualId)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Level = level;

            if (level == EnrollmentLevel.EnrolledIndividual && !individualId.HasValue)
            {
                throw new ArgumentException("An enrolled individual requires a Dagmay individual ID.", nameof(individualId));
            }

            if (level != EnrollmentLevel.EnrolledIndividual && individualId.HasValue)
            {
                throw new ArgumentException("Only enrolled individuals may carry a Dagmay individual ID.", nameof(individualId));
            }

            IndividualId = individualId;
        }

        public EnvironmentEntityReference Entity { get; }
        public EnrollmentLevel Level { get; }
        public IndividualId? IndividualId { get; }
    }
}

