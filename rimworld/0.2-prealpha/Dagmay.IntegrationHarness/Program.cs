using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dagmay.IntegrationHarness
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var options = HarnessOptions.Parse(args);
            var started = DateTimeOffset.UtcNow;
            var report = new HarnessReport { StartedUtc = started };
            var scenarios = IntegrationScenarios.All()
                .Where(value => options.ScenarioNames.Count == 0 || options.ScenarioNames.Contains(value.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (scenarios.Count == 0)
            {
                Console.Error.WriteLine("No integration scenarios matched the requested filter.");
                return 2;
            }

            foreach (var scenario in scenarios)
            {
                var result = new HarnessScenarioResult { Name = scenario.Name };
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var execution = await scenario.Run().ConfigureAwait(false);
                    result.Passed = true;
                    result.Assertions.AddRange(execution.Assertions);
                    foreach (var pair in execution.Metrics) result.Metrics[pair.Key] = pair.Value;
                    Console.WriteLine($"PASS {scenario.Name} ({result.Assertions.Count} assertions)");
                }
                catch (Exception exception)
                {
                    result.Passed = false;
                    result.Failure = exception.ToString();
                    Console.Error.WriteLine($"FAIL {scenario.Name}: {exception.Message}");
                }
                finally
                {
                    stopwatch.Stop();
                    result.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
                    report.Scenarios.Add(result);
                }

                if (!result.Passed && options.FailFast) break;
            }

            report.FinishedUtc = DateTimeOffset.UtcNow;
            report.ScenarioCount = report.Scenarios.Count;
            report.PassedCount = report.Scenarios.Count(value => value.Passed);
            report.FailedCount = report.ScenarioCount - report.PassedCount;

            var outputPath = Path.GetFullPath(options.OutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);

            Console.WriteLine($"Integration report: {outputPath}");
            Console.WriteLine($"Executed {report.ScenarioCount} scenarios; {report.FailedCount} failed.");
            return report.FailedCount == 0 ? 0 : 1;
        }
    }

    internal sealed class HarnessOptions
    {
        public string OutputPath { get; private set; } = Path.Combine("artifacts", "Dagmay-integration-latest.json");
        public bool FailFast { get; private set; }
        public List<string> ScenarioNames { get; } = new List<string>();

        public static HarnessOptions Parse(string[] args)
        {
            var options = new HarnessOptions();
            for (var index = 0; index < args.Length; index++)
            {
                var value = args[index];
                if (string.Equals(value, "--output", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length) throw new ArgumentException("--output requires a path.");
                    options.OutputPath = args[++index];
                }
                else if (string.Equals(value, "--scenario", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length) throw new ArgumentException("--scenario requires a scenario name.");
                    options.ScenarioNames.Add(args[++index]);
                }
                else if (string.Equals(value, "--fail-fast", StringComparison.OrdinalIgnoreCase))
                {
                    options.FailFast = true;
                }
                else
                {
                    throw new ArgumentException("Unknown integration-harness argument: " + value);
                }
            }

            return options;
        }
    }
}
