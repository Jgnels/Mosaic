using System;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Interaction
{
    public enum InteractionOriginKind
    {
        ExternalOperator,
        EmbodiedIndividual,
        EnvironmentSystem
    }

    public sealed class InteractionOrigin
    {
        public InteractionOrigin(
            InteractionOriginKind kind,
            EnvironmentEntityReference? embodiedEntity,
            IndividualId? embodiedIndividualId,
            string environmentLabel)
        {
            if (kind == InteractionOriginKind.EmbodiedIndividual && embodiedEntity is null)
            {
                throw new ArgumentException("An embodied interaction requires an environment actor.", nameof(embodiedEntity));
            }

            if (kind != InteractionOriginKind.EmbodiedIndividual
                && (embodiedEntity is not null || embodiedIndividualId.HasValue))
            {
                throw new ArgumentException("External and system interactions cannot impersonate an individual.", nameof(embodiedIndividualId));
            }

            Kind = kind;
            EmbodiedEntity = embodiedEntity;
            EmbodiedIndividualId = embodiedIndividualId;
            EnvironmentLabel = ContractGuard.Text(environmentLabel, nameof(environmentLabel), 256);
        }

        public InteractionOriginKind Kind { get; }
        public EnvironmentEntityReference? EmbodiedEntity { get; }
        public IndividualId? EmbodiedIndividualId { get; }
        public string EnvironmentLabel { get; }

        public static InteractionOrigin ExternalOperator(string environmentLabel)
        {
            return new InteractionOrigin(InteractionOriginKind.ExternalOperator, null, null, environmentLabel);
        }

        public static InteractionOrigin Embodied(
            EnvironmentEntityReference entity,
            IndividualId? individualId,
            string environmentLabel)
        {
            return new InteractionOrigin(InteractionOriginKind.EmbodiedIndividual, entity, individualId, environmentLabel);
        }
    }
}
