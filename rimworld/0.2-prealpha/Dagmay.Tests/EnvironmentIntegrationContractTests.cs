using System.Collections.Generic;
using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;
using Dagmay.Core.Experience;

namespace Dagmay.Tests
{
    internal static class EnvironmentIntegrationContractTests
    {
        public static void ContainmentAndUnresolvedBindingsPreserveIndividualIdentity()
        {
            var id = IndividualId.New();
            var binding = new EnvironmentBinding(id, EnvironmentBindingState.Bound, "Thing_Human123", 100);
            binding = binding.Transition(EnvironmentBindingState.TemporarilyContained, "VehicleRoleHandler:driver", 110);
            TestAssert.Equal(id, binding.IndividualId, "Containment must preserve IndividualId.");
            TestAssert.True(binding.IsTemporarilyUnresolved, "Contained pawn remains temporary binding state.");
            binding = binding.Transition(EnvironmentBindingState.UnresolvedEnvironmentBinding, null, 120);
            TestAssert.Equal(id, binding.IndividualId, "Unresolved reference must not erase identity.");
            binding = binding.Transition(EnvironmentBindingState.WorldPawn, "WorldPawn:Thing_Human123", 130);
            binding = binding.Transition(EnvironmentBindingState.Bound, "Thing_Human456", 140);
            TestAssert.Equal(id, binding.IndividualId, "Rebinding a live environment object must preserve enrolled identity.");
        }

        public static void ConfirmedDestructionCannotSilentlyRebind()
        {
            var id = IndividualId.New();
            var binding = new EnvironmentBinding(id, EnvironmentBindingState.Bound, "Thing_Human123", 100)
                .Transition(EnvironmentBindingState.DestroyedConfirmed, null, 200);
            TestAssert.Throws<System.InvalidOperationException>(
                () => binding.Transition(EnvironmentBindingState.Bound, "Thing_Human999", 210),
                "Confirmed destruction must require explicit recovery rather than silent rebind.");
        }

        public static void ExternalEventProposalStoresPortableDataInsteadOfModObjects()
        {
            var participant = IndividualId.New();
            var proposal = new ExternalEventProposal(
                "Orion.Hospitality",
                "hospitality.guest.departed",
                EvidenceDomain.Group,
                500,
                new[] { participant },
                "A guest departed after a positive visit.",
                new Dictionary<string, string> { ["outcome"] = "positive", ["role"] = "guest" });

            TestAssert.Equal("Orion.Hospitality", proposal.SourceModId, "Proposal must retain source-mod provenance.");
            TestAssert.Equal(EvidenceDomain.Group, proposal.Domain, "Proposal must retain evidence domain.");
            TestAssert.Equal(participant, proposal.ParticipantIds[0], "Proposal must retain Mosaic participant IDs.");
            TestAssert.Equal("positive", proposal.Attributes["outcome"], "Proposal attributes must be portable strings.");
        }
    }
}
