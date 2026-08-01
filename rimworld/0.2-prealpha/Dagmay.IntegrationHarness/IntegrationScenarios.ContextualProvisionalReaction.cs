using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Development;
using Dagmay.Core.Presentation;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private const string V44Session = "session-v44-integration";
        private const string V44EventId = "current-v44-integration";
        private const string V44UtteranceId = "utterance-v44-integration";
        private const long V44DisplayedTick = 250_000;

        private static ContextualAppraisalRequest V44ProposalRequest(string checkpointFingerprint)
        {
            var query = new DevelopmentalContextQuery(
                V41Save,
                V41World,
                V41Store,
                V41Owner,
                V41Lineage,
                V41Counterpart,
                V44DisplayedTick,
                V41Generation,
                new[] { checkpointFingerprint },
                DevelopmentalContextPurpose.Appraisal,
                new[] { "relationship", "social" },
                4);
            var bundle = (DevelopmentalContextBundle)V44InvokeInternal(
                typeof(DevelopmentalContextBundle),
                query,
                Array.Empty<DevelopmentalContextItem>(),
                new[] { new KeyValuePair<string, long>("selected_count", 0) });
            var materialization = new GroundedDevelopmentalContextRequest(
                query,
                bundle,
                new[] { V44EventId },
                V44DisplayedTick);
            var packet = (GroundedDevelopmentalContextPacket)V44InvokeInternal(
                typeof(GroundedDevelopmentalContextPacket),
                V41Owner,
                V41Lineage,
                V41Counterpart,
                DevelopmentalContextPurpose.Appraisal,
                materialization.Fingerprint,
                bundle.Fingerprint,
                Array.Empty<GroundedDevelopmentalContextItem>(),
                Array.Empty<GroundedDimensionSynthesis>(),
                Array.Empty<string>(),
                new[] { new KeyValuePair<string, long>("full_canonical_objects_retained", 0) });
            var seed = GroundedCurrentEventAppraisalSeed.Create(
                V41Owner,
                V41Lineage,
                V41Counterpart,
                V44EventId,
                new[] { V44EventId },
                V44DisplayedTick,
                V41Hash("event:" + V44EventId),
                V41Save,
                V41World,
                V41Store,
                V41Generation,
                checkpointFingerprint,
                "OWNER_PRIVATE",
                true,
                true,
                true,
                new[]
                {
                    new KeyValuePair<GroundedAppraisalFamily, int>(
                        GroundedAppraisalFamily.Gratitude,
                        7000)
                },
                new[] { new KeyValuePair<string, int>("affect.valence", 3000) },
                new[] { new KeyValuePair<string, int>("relationship.trust", 2000) },
                new[] { "RULE_DIRECT_EVENT" });
            return new ContextualAppraisalRequest(seed, materialization, packet);
        }

        private static ObservedDisplayReceipt V44ObservedDisplay()
        {
            var method = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted observed-display factory is unavailable.");
            return (ObservedDisplayReceipt)(method.Invoke(
                null,
                new object?[]
                {
                    V44Session,
                    V41Hash("release-attempt:" + V44UtteranceId),
                    V41Hash("display-receipt:" + V44UtteranceId),
                    V44UtteranceId,
                    "conversation-v44-integration",
                    V41Counterpart,
                    V41Owner,
                    new[] { V41Counterpart, V41Owner }.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    V41Hash("displayed-text:" + V44UtteranceId),
                    V44DisplayedTick,
                    V41Generation,
                    ObservedDisplayOutcome.OBSERVED_SUCCESS,
                    ObservedDisplayReceipt.SourceContractValue,
                    ObservedDisplayReceipt.SourceGateDigestValue
                }) ?? throw new HarnessAssertionException("Trusted observed-display factory returned null."));
        }

        private static object V44InvokeInternal(Type type, params object[] values)
        {
            var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate => candidate.GetParameters().Length == values.Length);
            try
            {
                return constructor.Invoke(values);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }
    }
}
