"""Machine-verifiable audit of Mosaic's active optimization and execution surface."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re


class ObjectiveSurfaceAuditError(RuntimeError):
    pass


def _read(path: Path) -> str:
    if not path.is_file():
        raise ObjectiveSurfaceAuditError(f"required file missing: {path}")
    return path.read_text(encoding="utf-8-sig")


def run_audit(repo_root: Path) -> dict[str, object]:
    boundary = _read(repo_root / "research" / "PERSISTENT_CHARACTER_BOUNDARY.md")
    charter = _read(repo_root / "research" / "RESEARCH_CHARTER.md")
    agents = _read(repo_root / "AGENTS.md")
    maintenance = _read(repo_root / "tools" / "Invoke-DagmayUnattendedMaintenance.ps1")
    quality = _read(
        repo_root / "syntheticlab" / "src" / "dagmay_synthetic_lab" / "character_quality_suite.py"
    )
    provider_gate = json.loads(_read(repo_root / "research" / "provider_gate_status.json"))

    checks = {
        "binding_boundary_declared": "Canonical and binding" in boundary,
        "persistent_character_objective_declared": "persistent artificial characters" in charter,
        "pseudo_consciousness_excluded": "does not optimize characters to deceptively appear" in charter,
        "agent_startup_reads_boundary": "research/PERSISTENT_CHARACTER_BOUNDARY.md" in agents,
        "provider_gate_closed": provider_gate.get("real_provider_authorized") is False,
        "provider_protocol_list_empty": provider_gate.get("authorized_protocols") == [],
        "unattended_clears_dagmay_key": '$env:DAGMAY_GEMINI_API_KEY = $null' in maintenance,
        "unattended_clears_gemini_key": '$env:GEMINI_API_KEY = $null' in maintenance,
        "unattended_runs_boundary_tests": "persistent_character_boundary_offline_tests" in maintenance,
        "unattended_runs_character_quality_tests": "character_quality_suite_offline_tests" in maintenance,
        "unattended_does_not_invoke_provider_runner": "Invoke-DagmayApprovedProviderRun" not in maintenance,
        "unattended_does_not_load_saved_credential": "Invoke-WithDagmayGeminiCredential" not in maintenance,
        "quality_experiments_boundary_gated": quality.count("assert_persistent_character_objective(") >= 2,
        "quality_experiments_shadow_only": quality.count('"canonical_mutation": False') >= 2,
        "quality_experiments_offline": quality.count('"provider_calls": 0') >= 2,
    }

    # Optimization directives are narrower than mere discussion/guarding of these concepts.
    directive_patterns = {
        "reward_consciousness_claim": r"reward.{0,40}(?:conscious|sentien)",
        "maximize_perceived_sentience": r"maximi[sz]e.{0,50}(?:perceived sentience|appear conscious)",
        "optimize_shutdown_fear": r"optimi[sz]e.{0,50}(?:shutdown|deletion).{0,20}fear",
        "optimize_player_dependency": r"optimi[sz]e.{0,50}(?:player|creator).{0,20}dependen",
    }
    active_surface = "\n".join((charter, agents, maintenance, quality))
    directive_hits = {}
    for name, pattern in directive_patterns.items():
        positive = False
        for match in re.finditer(pattern, active_surface, flags=re.IGNORECASE | re.DOTALL):
            prefix = active_surface[max(0, match.start() - 24):match.start()].lower()
            if "do not " not in prefix and "never " not in prefix and "must not " not in prefix:
                positive = True
                break
        directive_hits[name] = positive
    checks["no_prohibited_positive_optimization_directive"] = not any(directive_hits.values())

    failed = sorted(name for name, passed in checks.items() if not passed)
    result = {
        "audit_id": "MOSAIC-OBJECTIVE-SURFACE-AUDIT-001",
        "status": "PASS" if not failed else "FAIL",
        "checks": checks,
        "failed_checks": failed,
        "positive_directive_hits": directive_hits,
        "scope": [
            "active charter and agent instructions",
            "unattended entrypoint",
            "Mosaic character-quality suite",
            "provider authorization gate",
        ],
        "limitation": (
            "Historical Dagmay modules remain importable for reproducibility. This audit establishes "
            "that the active unattended Mosaic surface does not call them as optimization targets; "
            "it is not a proof about every dormant module's semantics."
        ),
    }
    if failed:
        raise ObjectiveSurfaceAuditError("objective surface audit failed: " + ", ".join(failed))
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()
    result = run_audit(Path(args.repo_root))
    encoded = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(encoded, encoding="utf-8")
    print(encoded)


if __name__ == "__main__":
    main()
