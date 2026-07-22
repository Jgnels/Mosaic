using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Experience
{
    public enum ExternalEventAdmissionStatus
    {
        Accepted = 0,
        RejectedUnknownSource = 1,
        RejectedUnavailableCapability = 2,
        RejectedMissingParticipants = 3,
        RejectedUnsupportedEventKind = 4
    }

    public sealed class ExternalEventAdmissionResult
    {
        public ExternalEventAdmissionResult(
            ExternalEventAdmissionStatus status,
            string explanation)
        {
            Status = status;
            Explanation = ContractGuard.Text(explanation, nameof(explanation), 1024);
        }

        public ExternalEventAdmissionStatus Status { get; }
        public string Explanation { get; }
        public bool Accepted => Status == ExternalEventAdmissionStatus.Accepted;
    }

    public interface IExternalEventAdmissionPolicy
    {
        ExternalEventAdmissionResult Evaluate(ExternalEventProposal proposal);
    }

    /// <summary>
    /// Small deterministic allow-list policy. Adapters may disappear later;
    /// already-admitted Mosaic evidence remains durable because it contains no
    /// third-party runtime objects.
    /// </summary>
    public sealed class AllowListedExternalEventAdmissionPolicy : IExternalEventAdmissionPolicy
    {
        private readonly HashSet<string> _allowedSources;
        private readonly HashSet<string> _allowedEventKinds;

        public AllowListedExternalEventAdmissionPolicy(
            IEnumerable<string> allowedSources,
            IEnumerable<string> allowedEventKinds)
        {
            _allowedSources = CopySet(allowedSources, nameof(allowedSources));
            _allowedEventKinds = CopySet(allowedEventKinds, nameof(allowedEventKinds));
        }

        public ExternalEventAdmissionResult Evaluate(ExternalEventProposal proposal)
        {
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));

            if (!_allowedSources.Contains(proposal.SourceModId))
                return new ExternalEventAdmissionResult(
                    ExternalEventAdmissionStatus.RejectedUnknownSource,
                    $"Source mod '{proposal.SourceModId}' is not allow-listed.");

            if (!_allowedEventKinds.Contains(proposal.EventKind))
                return new ExternalEventAdmissionResult(
                    ExternalEventAdmissionStatus.RejectedUnsupportedEventKind,
                    $"Event kind '{proposal.EventKind}' is not allow-listed.");

            if (proposal.ParticipantIds.Count == 0)
                return new ExternalEventAdmissionResult(
                    ExternalEventAdmissionStatus.RejectedMissingParticipants,
                    "External event proposal must identify at least one enrolled Mosaic participant.");

            return new ExternalEventAdmissionResult(
                ExternalEventAdmissionStatus.Accepted,
                "External event proposal passed deterministic admission policy.");
        }

        private static HashSet<string> CopySet(IEnumerable<string> values, string parameterName)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
                result.Add(ContractGuard.Text(value, parameterName, 256));
            return result;
        }
    }
}
