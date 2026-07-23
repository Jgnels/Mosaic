using System;
using System.Collections.Generic;

namespace Dagmay.IntegrationHarness
{
    internal sealed class HarnessScenarioResult
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public long DurationMilliseconds { get; set; }
        public List<string> Assertions { get; set; } = new List<string>();
        public Dictionary<string, long> Metrics { get; set; } = new Dictionary<string, long>(StringComparer.Ordinal);
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Failure { get; set; } = string.Empty;
    }

    internal sealed class HarnessReport
    {
        public string Schema { get; set; } = "dagmay.integration-harness.v1";
        public string DagmayVersion { get; set; } = "0.1K";
        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }
        public int ScenarioCount { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public List<HarnessScenarioResult> Scenarios { get; set; } = new List<HarnessScenarioResult>();
    }
}
