using System;
using System.Collections.Generic;
using Dagmay.Core.Appraisal;

namespace Dagmay.RimWorld.Perception
{
    /// <summary>
    /// Pure, inert bridge from the PR #10 read-only projection and its already-admitted
    /// factual payload into typed Mosaic Core social evidence. It has no Pawn, Map,
    /// Def, job, provider, persistence, or canonical mutation access.
    /// </summary>
    public static class GroundedSocialSemanticProjectionAdapter
    {
        public static GroundedSocialSemanticEvidence? TryProject(
            ReadOnlyRimWorldEventProjection projection,
            long sourceAdmissionPosition,
            string observationBatchId,
            IDictionary<string, string> factualPayload)
        {
            if (projection is null || factualPayload is null) return null;
            try
            {
                return new GroundedSocialSemanticEvidence(
                    projection.Envelope,
                    sourceAdmissionPosition,
                    observationBatchId,
                    factualPayload);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
