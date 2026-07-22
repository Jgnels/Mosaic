using System;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Actions
{
    public enum ActionOriginKind
    {
        GameNative = 0,
        ExternalModExecution = 1,
        MosaicCommitment = 2,
        PlayerCommand = 3
    }

    public sealed class ActionAttribution
    {
        public ActionAttribution(
            ActionOriginKind origin,
            string actionKind,
            string? sourceModId = null,
            string? mosaicDecisionId = null)
        {
            Origin = origin;
            ActionKind = ContractGuard.Text(actionKind, nameof(actionKind), 256);
            SourceModId = ContractGuard.OptionalText(sourceModId, nameof(sourceModId), 256);
            MosaicDecisionId = ContractGuard.OptionalText(mosaicDecisionId, nameof(mosaicDecisionId), 256);

            if (origin == ActionOriginKind.ExternalModExecution && SourceModId is null)
                throw new ArgumentException("External-mod actions require source mod provenance.", nameof(sourceModId));

            if (origin == ActionOriginKind.MosaicCommitment && MosaicDecisionId is null)
                throw new ArgumentException("Mosaic-authored actions require a Mosaic decision or goal identifier.", nameof(mosaicDecisionId));
        }

        public ActionOriginKind Origin { get; }
        public string ActionKind { get; }
        public string? SourceModId { get; }
        public string? MosaicDecisionId { get; }
        public bool WasAuthoredByMosaic => Origin == ActionOriginKind.MosaicCommitment;
    }
}
