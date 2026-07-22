using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;
using Dagmay.Core.Experience;
using Dagmay.Core.Integration;

namespace Dagmay.Tests
{
    internal static class ExternalIntegrationContractTests
    {
        public static void OptionalCapabilityDoesNotRequireHardDependency()
        {
            var capability = new ExternalCapability(
                "OskarPotocki.VanillaFactionsExpanded.Core",
                "vanilla-expanded-framework",
                available: false,
                sourceVersion: "1.6");

            TestAssert.False(capability.Available,
                "Missing optional integration must be representable without failing Mosaic startup.");
            TestAssert.Equal("vanilla-expanded-framework", capability.CapabilityId,
                "Capability identity must remain portable and string-based.");
        }

        public static void ExternalEventAdmissionRejectsUnknownSourceAndUnsupportedKind()
        {
            var participant = IndividualId.New();
            var policy = new AllowListedExternalEventAdmissionPolicy(
                new[] { "Orion.Hospitality" },
                new[] { "hospitality.guest.departed" });

            var unknownSource = new ExternalEventProposal(
                "Unknown.Mod",
                "hospitality.guest.departed",
                EvidenceDomain.Group,
                100,
                new[] { participant },
                "Unknown mod emitted a guest departure.");

            var unsupportedKind = new ExternalEventProposal(
                "Orion.Hospitality",
                "hospitality.internal.cache_refresh",
                EvidenceDomain.Group,
                101,
                new[] { participant },
                "Internal implementation detail.");

            TestAssert.Equal(
                ExternalEventAdmissionStatus.RejectedUnknownSource,
                policy.Evaluate(unknownSource).Status,
                "Unknown mod sources must not silently become canonical Mosaic evidence.");

            TestAssert.Equal(
                ExternalEventAdmissionStatus.RejectedUnsupportedEventKind,
                policy.Evaluate(unsupportedKind).Status,
                "Implementation-detail event kinds must not silently become canonical Mosaic evidence.");
        }

        public static void ExternalEventAdmissionAcceptsPortableStoryEvent()
        {
            var participant = IndividualId.New();
            var policy = new AllowListedExternalEventAdmissionPolicy(
                new[] { "Orion.Hospitality" },
                new[] { "hospitality.guest.departed" });

            var proposal = new ExternalEventProposal(
                "Orion.Hospitality",
                "hospitality.guest.departed",
                EvidenceDomain.Group,
                500,
                new[] { participant },
                "A recurring guest left the colony after a positive visit.");

            var result = policy.Evaluate(proposal);

            TestAssert.True(result.Accepted,
                "Known story-significant external event must be admissible through deterministic policy.");
        }
    }
}
