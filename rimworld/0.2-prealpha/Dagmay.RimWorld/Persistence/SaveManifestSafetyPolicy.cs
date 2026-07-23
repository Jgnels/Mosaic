using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagmay.RimWorld.Persistence
{
    internal enum SaveManifestDisposition
    {
        InitializeNewStore,
        LoadExistingStore,
        FailClosed
    }

    internal sealed class SaveManifestDecision
    {
        public SaveManifestDecision(
            SaveManifestDisposition disposition,
            Guid? storeId,
            string diagnostic)
        {
            Disposition = disposition;
            StoreId = storeId;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public SaveManifestDisposition Disposition { get; }
        public Guid? StoreId { get; }
        public string Diagnostic { get; }
    }

    internal static class SaveManifestSafetyPolicy
    {
        public const string InvalidStoreFileStem = "invalid-manifest";

        public static SaveManifestDecision Evaluate(
            string? rawStoreId,
            long identityGeneration,
            long reflectionGeneration,
            long experiencePosition,
            string? experienceLastHash,
            IReadOnlyList<string>? externalIds,
            IReadOnlyList<string>? individualIds,
            IReadOnlyList<string>? pausedExternalIds)
        {
            externalIds = externalIds ?? Array.Empty<string>();
            individualIds = individualIds ?? Array.Empty<string>();
            pausedExternalIds = pausedExternalIds ?? Array.Empty<string>();
            experienceLastHash = experienceLastHash ?? string.Empty;

            if (identityGeneration < 0 || reflectionGeneration < 0 || experiencePosition < 0)
            {
                return Fail("The RimWorld save contains a negative Mosaic checkpoint value.");
            }

            if (!ExperienceCheckpointShapeIsValid(experiencePosition, experienceLastHash))
            {
                return Fail("The RimWorld save contains an inconsistent Mosaic experience checkpoint.");
            }

            if (!IdentityManifestShapeIsValid(externalIds, individualIds, out var identityDiagnostic))
            {
                return Fail(identityDiagnostic);
            }

            if (pausedExternalIds.Any(string.IsNullOrWhiteSpace)
                || pausedExternalIds.Distinct(StringComparer.Ordinal).Count() != pausedExternalIds.Count)
            {
                return Fail("The RimWorld save contains an invalid or duplicate paused-identity mapping.");
            }
            var externalIdSet = new HashSet<string>(externalIds, StringComparer.Ordinal);
            if (pausedExternalIds.Any(value => !externalIdSet.Contains(value)))
            {
                return Fail("The RimWorld save pauses an external identity that is absent from its identity mapping.");
            }

            var hasCheckpointEvidence = identityGeneration != 0
                || reflectionGeneration != 0
                || experiencePosition != 0
                || experienceLastHash.Length != 0
                || externalIds.Count != 0
                || individualIds.Count != 0
                || pausedExternalIds.Count != 0;

            if (string.IsNullOrEmpty(rawStoreId))
            {
                return hasCheckpointEvidence
                    ? Fail("The RimWorld save contains Mosaic checkpoint evidence but no valid store ID.")
                    : new SaveManifestDecision(
                        SaveManifestDisposition.InitializeNewStore,
                        null,
                        "No Mosaic manifest is present; a new store may be initialized.");
            }

            if (!Guid.TryParse(rawStoreId, out var storeId) || storeId == Guid.Empty)
            {
                return Fail("The RimWorld save contains an invalid Mosaic store ID.");
            }

            return new SaveManifestDecision(
                SaveManifestDisposition.LoadExistingStore,
                storeId,
                "The Mosaic save manifest is structurally valid.");
        }

        public static string SafeStoreFileStem(string? rawStoreId)
        {
            return Guid.TryParse(rawStoreId, out var storeId) && storeId != Guid.Empty
                ? storeId.ToString("N")
                : InvalidStoreFileStem;
        }

        private static bool ExperienceCheckpointShapeIsValid(long position, string lastHash)
        {
            if (position == 0) return lastHash.Length == 0;
            if (lastHash.Length != 64) return false;
            foreach (var value in lastHash)
            {
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))) return false;
            }

            return true;
        }

        private static bool IdentityManifestShapeIsValid(
            IReadOnlyList<string> externalIds,
            IReadOnlyList<string> individualIds,
            out string diagnostic)
        {
            if (externalIds.Count != individualIds.Count)
            {
                diagnostic = "The RimWorld save contains unequal Mosaic identity-mapping lists.";
                return false;
            }

            if (externalIds.Any(string.IsNullOrWhiteSpace)
                || externalIds.Distinct(StringComparer.Ordinal).Count() != externalIds.Count)
            {
                diagnostic = "The RimWorld save contains an invalid or duplicate external identity mapping.";
                return false;
            }

            var parsedIndividualIds = new HashSet<Guid>();
            foreach (var value in individualIds)
            {
                if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty || !parsedIndividualIds.Add(parsed))
                {
                    diagnostic = "The RimWorld save contains an invalid or duplicate individual identity mapping.";
                    return false;
                }
            }

            diagnostic = string.Empty;
            return true;
        }

        private static SaveManifestDecision Fail(string diagnostic)
        {
            return new SaveManifestDecision(SaveManifestDisposition.FailClosed, null, diagnostic);
        }
    }
}
