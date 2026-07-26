#!/usr/bin/env python3
"""Static safety and repository checks that do not require the .NET SDK."""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

try:
    import tomllib
except ModuleNotFoundError:  # Python 3.9/3.10 compatibility for the optional verifier.
    tomllib = None  # type: ignore[assignment]


ROOT = Path(__file__).resolve().parents[1]

REQUIRED_FILES = [
    "README.md",
    "START_HERE.md",
    "AGENTS.md",
    ".codex/agents/dagmay-implementer.toml",
    "Directory.Build.props",
    "global.json",
    "Dagmay.sln",
    "Dagmay.Core/Dagmay.Core.csproj",
    "Dagmay.Providers/Dagmay.Providers.csproj",
    "Dagmay.RimWorld/Dagmay.RimWorld.csproj",
    "Dagmay.RimWorld/Package/About/About.xml",
    "Dagmay.Tests/Dagmay.Tests.csproj",
    "Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj",
    "Dagmay.Core/Dialogue/DialogueAdmissionOutbox.cs",
    "Dagmay.Core/Dialogue/DialogueAdmissionOutboxCodec.cs",
    "Dagmay.Core/Dialogue/SpeechBubblePresentation.cs",
    "Dagmay.Core/Relationships/RelationshipEvidenceProjection.cs",
    "Dagmay.RimWorld/Dialogue/MosaicDialogueLogEntry.cs",
    "Dagmay.RimWorld/Dialogue/OfflineRimWorldDialoguePipeline.cs",
    "Dagmay.RimWorld/Dialogue/RimWorldDialogueGameComponent.cs",
    "Dagmay.RimWorld/Dialogue/RimWorldDialoguePresentationPolicy.cs",
    "Dagmay.RimWorld/Dialogue/RimWorldSocialDialogueTrigger.cs",
    "Dagmay.RimWorld/Dialogue/RimWorldSpeechBubblePresenter.cs",
    "Dagmay.RimWorld/Package/Defs/MosaicDialogueDefs.xml",
    "Dagmay.Tests/DialogueAdmissionOutboxContractTests.cs",
    "Dagmay.Tests/SpeechBubblePresentationContractTests.cs",
    "Dagmay.Tests/OfflineRimWorldDialoguePathContractTests.cs",
    "Dagmay.Tests/StorytellingEvidenceSpineFixtureTests.cs",
    "../../docs/ADR_DIALOGUE_RECOVERABLE_OUTBOX_20260726.md",
    "../../docs/ADR_SPEECH_BUBBLE_PRESENTATION_20260726.md",
    "../../docs/ADR_OFFLINE_RIMWORLD_DIALOGUE_PATH_20260726.md",
    "docs/22_V0.1F_Development_Automation.md",
    "docs/23_V0.1G_Ordinary_Mind_View.md",
    "docs/24_V0.1H_Experience_Consolidation.md",
    "docs/25_V0.1I_Salience_Admission.md",
    "docs/28_Automated_Integration_Harness.md",
    "docs/26_V0.1J_Social_Relationships.md",
    "docs/29_V0.1K_Social_Path_Certification.md",
    "docs/30_V0.1_Closure_Plan.md",
    "docs/31_V0.1L_Persistence_Torture.md",
    "docs/32_V0.1M_Failure_Isolation.md",
    "tools/check-social-certification.ps1",
    "tools/collect-logs.ps1",
    "tools/gate3-preflight.ps1",
    "tools/new-deterministic-package.ps1",
    "tools/package-firewall.ps1",
    "tools/package-firewall-fixtures.json",
    "tools/test-package-firewall.ps1",
    "tools/dev-loop.ps1",
    "tools/install.ps1",
    "tools/run-integration-harness.ps1",
    "tools/run-persistence-torture.ps1",
    "tools/run-failure-isolation.ps1",
    "tools/run-gate3-offline-soak.ps1",
    "tools/package-source.ps1",
]

CORE_FORBIDDEN = [
    "using Verse",
    "using RimWorld",
    "using UnityEngine",
    "using HarmonyLib",
    "GoogleAiStudio",
]

OBSERVER_FORBIDDEN = [
    "HarmonyPatch",
    ".StartJob(",
    ".TryTakeOrderedJob(",
    ".TakeOrderedJob(",
    ".SetPriority(",
    ".StartPath(",
    ".TryStartAttack(",
    ".Drafted =",
]

SECRET_PATTERNS = {
    "OpenAI-style secret": re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b"),
    "Google-style secret": re.compile(r"\bAIza[A-Za-z0-9_-]{30,}\b"),
}


def is_build_output(path: Path) -> bool:
    try:
        repository_parts = path.resolve().relative_to(ROOT.resolve()).parts
    except ValueError:
        return False
    return any(part in {"bin", "obj", "artifacts"} for part in repository_parts)


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def strip_csharp_literals_and_comments(source: str) -> str:
    output: list[str] = []
    index = 0
    state = "code"
    while index < len(source):
        current = source[index]
        following = source[index + 1] if index + 1 < len(source) else ""

        if state == "code":
            if current == "/" and following == "/":
                state = "line_comment"
                output.extend("  ")
                index += 2
                continue
            if current == "/" and following == "*":
                state = "block_comment"
                output.extend("  ")
                index += 2
                continue
            if current == '"':
                state = "string"
                output.append(" ")
                index += 1
                continue
            if current == "'":
                state = "character"
                output.append(" ")
                index += 1
                continue
            output.append(current)
            index += 1
            continue

        if state == "line_comment":
            if current == "\n":
                state = "code"
                output.append("\n")
            else:
                output.append(" ")
            index += 1
            continue

        if state == "block_comment":
            if current == "*" and following == "/":
                state = "code"
                output.extend("  ")
                index += 2
            else:
                output.append("\n" if current == "\n" else " ")
                index += 1
            continue

        if state in {"string", "character"}:
            delimiter = '"' if state == "string" else "'"
            if current == "\\":
                output.extend("  ")
                index += 2
                continue
            if current == delimiter:
                state = "code"
            output.append("\n" if current == "\n" else " ")
            index += 1

    return "".join(output)


def verify_required_files(errors: list[str]) -> None:
    for relative in REQUIRED_FILES:
        path = ROOT / relative
        if not path.is_file() or path.stat().st_size == 0:
            fail(errors, f"Missing or empty required file: {relative}")


def verify_structured_files(errors: list[str]) -> None:
    for path in ROOT.rglob("*.csproj"):
        try:
            project = ET.parse(path)
        except ET.ParseError as exc:
            fail(errors, f"Invalid project XML {path.relative_to(ROOT)}: {exc}")
            continue

        for compile_item in project.findall(".//Compile"):
            include = compile_item.get("Include", "")
            if not include or any(marker in include for marker in ("*", "?", "$(")):
                continue
            included_path = (path.parent / include.replace("\\", os.sep)).resolve()
            if not included_path.is_file():
                fail(
                    errors,
                    f"Project compile source is missing: {path.relative_to(ROOT)} -> {include}",
                )

    try:
        ET.parse(ROOT / "Dagmay.RimWorld/Package/About/About.xml")
    except ET.ParseError as exc:
        fail(errors, f"Invalid RimWorld About.xml: {exc}")

    for path in ROOT.rglob("*.json"):
        if is_build_output(path):
            continue
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            fail(errors, f"Invalid JSON {path.relative_to(ROOT)}: {exc}")

    agent_path = ROOT / ".codex/agents/dagmay-implementer.toml"
    if tomllib is not None:
        for path in ROOT.rglob("*.toml"):
            if is_build_output(path):
                continue
            try:
                tomllib.loads(path.read_text(encoding="utf-8"))
            except (OSError, tomllib.TOMLDecodeError) as exc:
                fail(errors, f"Invalid TOML {path.relative_to(ROOT)}: {exc}")

        try:
            agent = tomllib.loads(agent_path.read_text(encoding="utf-8"))
            for field in ("name", "description", "developer_instructions"):
                if not isinstance(agent.get(field), str) or not agent[field].strip():
                    fail(errors, f"Custom Codex agent is missing required field: {field}")
        except (OSError, tomllib.TOMLDecodeError):
            pass
    else:
        agent_source = agent_path.read_text(encoding="utf-8")
        for field in ("name", "description", "developer_instructions"):
            if not re.search(rf"(?m)^{field}\s*=\s*", agent_source):
                fail(errors, f"Custom Codex agent is missing required field: {field}")


def verify_rimworld_references(errors: list[str]) -> None:
    project = (ROOT / "Dagmay.RimWorld/Dagmay.RimWorld.csproj").read_text(encoding="utf-8")
    required_references = (
        "Assembly-CSharp",
        "UnityEngine.IMGUIModule",
        "UnityEngine.TextRenderingModule",
    )
    for reference in required_references:
        if f'<Reference Include="{reference}"' not in project:
            fail(errors, f"Missing required RimWorld/Unity project reference: {reference}")


def verify_release_version(errors: list[str]) -> None:
    expected = "0.2-prealpha"
    required_markers = {
        "Dagmay.RimWorld/Bootstrap/DagmayBuildInfo.cs": f'Version = "{expected}"',
        "Dagmay.RimWorld/Package/About/About.xml": f"Version {expected}",
        "tools/build.ps1": f'$DagmayVersion = "{expected}"',
        "tools/install.ps1": f'$DagmayVersion = "{expected}"',
        "tools/package-source.ps1": f'$DagmayVersion = "{expected}"',
    }
    for relative, marker in required_markers.items():
        source = (ROOT / relative).read_text(encoding="utf-8")
        if marker not in source:
            fail(errors, f"Release version marker is not aligned in {relative}: expected {expected}")


def verify_csharp_shape(errors: list[str]) -> None:
    for path in ROOT.rglob("*.cs"):
        if is_build_output(path):
            continue
        source = path.read_text(encoding="utf-8")
        stripped = strip_csharp_literals_and_comments(source)
        balance = 0
        for character in stripped:
            if character == "{":
                balance += 1
            elif character == "}":
                balance -= 1
            if balance < 0:
                break
        if balance != 0:
            fail(errors, f"Unbalanced C# braces: {path.relative_to(ROOT)}")


def verify_boundaries(errors: list[str]) -> None:
    core_files = [path for path in (ROOT / "Dagmay.Core").rglob("*.cs") if not is_build_output(path)]
    for path in core_files:
        source = path.read_text(encoding="utf-8")
        for token in CORE_FORBIDDEN:
            if token in source:
                fail(errors, f"Core dependency boundary violation in {path.relative_to(ROOT)}: {token}")

    rimworld_files = [path for path in (ROOT / "Dagmay.RimWorld").rglob("*.cs") if not is_build_output(path)]
    for path in rimworld_files:
        source = path.read_text(encoding="utf-8")
        for token in OBSERVER_FORBIDDEN:
            if token in source:
                fail(errors, f"Observer-only boundary violation in {path.relative_to(ROOT)}: {token}")

    offline_dialogue = (
        ROOT / "Dagmay.RimWorld/Dialogue/OfflineRimWorldDialoguePipeline.cs"
    ).read_text(encoding="utf-8")
    if "new DeterministicFakeProvider(" not in offline_dialogue:
        fail(errors, "Offline RimWorld dialogue path does not construct the deterministic fake provider.")
    for token in ("GoogleAiStudioProvider", "RimWorldReflectionProviderSelection", "FromEnvironment("):
        if token in offline_dialogue:
            fail(errors, f"Offline RimWorld dialogue path exposes prohibited provider selection: {token}")

    if not any("AllowsPawnControl = false" in path.read_text(encoding="utf-8") for path in rimworld_files):
        fail(errors, "Observer-only guard is missing or does not explicitly deny pawn control.")

    component_path = ROOT / "Dagmay.RimWorld/Persistence/DagmayIdentityGameComponent.cs"
    component = strip_csharp_literals_and_comments(component_path.read_text(encoding="utf-8"))
    checkpoint_guard_requirements = {
        "_reflectionCheckpointGate.BeginLoadedSession();":
            "LoadedGame does not begin the post-load reflection checkpoint guard.",
        "_reflectionCheckpointGate.AllowsSidecarPersistence(":
            "Reflection persistence does not consult the post-load checkpoint guard.",
        "_reflectionCheckpointGate.CompleteRimWorldSaveCheckpoint();":
            "A successful RimWorld save does not release the post-load reflection checkpoint guard.",
    }
    for token, diagnostic in checkpoint_guard_requirements.items():
        if token not in component:
            fail(errors, diagnostic)

    observer_method = re.search(
        r"public\s+ObserverSystemSnapshot\s+CreateObserverSnapshot\s*\(\s*\)\s*\{(?P<body>.*?)\n\s*\}",
        component,
        re.DOTALL,
    )
    if observer_method is None:
        fail(errors, "CreateObserverSnapshot could not be inspected for read purity.")
    else:
        observer_forbidden = (
            "RefreshRuntimeSettings(",
            "SynchronizeColonists(",
            "PersistIfAllowed(",
            "PersistReflectionIfDirty(",
            "TryDispatchReflection(",
        )
        for token in observer_forbidden:
            if token in observer_method.group("body"):
                fail(errors, f"Observer snapshot read-purity violation: {token}")

    synchronize_method = re.search(
        r"private\s+bool\s+SynchronizeColonists\s*\(\s*\)\s*\{(?P<body>.*?)\n\s*\}",
        component,
        re.DOTALL,
    )
    if synchronize_method is None:
        fail(errors, "SynchronizeColonists could not be inspected for fail-closed identity safety.")
    elif "if (!_writesEnabled) return false;" not in synchronize_method.group("body"):
        fail(errors, "Read-only identity storage does not block colonist synchronization.")

    synchronize_pawn_method = re.search(
        r"private\s+bool\s+SynchronizePawn\s*\(\s*Pawn\s+pawn\s*,\s*bool\s+forceEnrollment\s*\)\s*\{(?P<body>.*?)\n\s*\}",
        component,
        re.DOTALL,
    )
    if synchronize_pawn_method is None:
        fail(errors, "SynchronizePawn could not be inspected for fail-closed identity safety.")
    elif "if (!_writesEnabled) return false;" not in synchronize_pawn_method.group("body"):
        fail(errors, "Read-only identity storage does not block individual creation or mutation.")


def verify_readme_links(errors: list[str]) -> None:
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", readme):
        if "://" in target or target.startswith("#"):
            continue
        clean_target = target.split("#", 1)[0]
        if clean_target and not (ROOT / clean_target).exists():
            fail(errors, f"Broken README link: {target}")


def verify_no_secrets(errors: list[str]) -> None:
    text_extensions = {".cs", ".md", ".xml", ".json", ".toml", ".props", ".csproj", ".ps1", ".sh", ".py", ".txt"}
    for path in ROOT.rglob("*"):
        if is_build_output(path) or not path.is_file() or path.suffix.lower() not in text_extensions:
            continue
        source = path.read_text(encoding="utf-8", errors="replace")
        for label, pattern in SECRET_PATTERNS.items():
            if pattern.search(source):
                fail(errors, f"Possible {label} in {path.relative_to(ROOT)}")


def verify_no_binaries(errors: list[str]) -> None:
    unexpected = [
        path
        for path in ROOT.rglob("*.dll")
        if not is_build_output(path)
        and "Package/Assemblies" not in path.as_posix()
    ]
    for path in unexpected:
        fail(errors, f"Unexpected checked-in binary: {path.relative_to(ROOT)}")


def find_git() -> str | None:
    discovered = shutil.which("git")
    if discovered:
        return discovered

    candidates = [
        Path(os.environ.get("ProgramFiles", "")) / "Git/cmd/git.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs/Git/cmd/git.exe",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return str(candidate)
    return None


def verify_git_source_tracking(errors: list[str]) -> None:
    git = find_git()
    if git is None:
        return

    repository = subprocess.run(
        [git, "-C", str(ROOT), "rev-parse", "--is-inside-work-tree"],
        capture_output=True,
        check=False,
        text=True,
    )
    if repository.returncode != 0 or repository.stdout.strip() != "true":
        return

    inventory = subprocess.run(
        [git, "-C", str(ROOT), "ls-files", "-z", "--", "."],
        capture_output=True,
        check=False,
    )
    if inventory.returncode != 0:
        fail(errors, "Git source inventory could not be read.")
        return

    tracked = {
        value.decode("utf-8", errors="surrogateescape").replace("\\", "/")
        for value in inventory.stdout.split(b"\0")
        if value
    }
    local_sources = {
        path.relative_to(ROOT).as_posix()
        for path in ROOT.rglob("*.cs")
        if not is_build_output(path)
    }

    for relative in sorted(local_sources - tracked):
        fail(errors, f"C# source exists locally but is not tracked by Git: {relative}")

    for relative in sorted(
        value for value in tracked if value.lower().endswith(".cs")
    ):
        if not (ROOT / relative).is_file():
            fail(errors, f"Tracked C# source is missing from the working tree: {relative}")


def main() -> int:
    errors: list[str] = []
    verify_required_files(errors)
    verify_structured_files(errors)
    verify_rimworld_references(errors)
    verify_release_version(errors)
    verify_csharp_shape(errors)
    verify_boundaries(errors)
    verify_readme_links(errors)
    verify_no_secrets(errors)
    verify_no_binaries(errors)
    verify_git_source_tracking(errors)

    if errors:
        for error in errors:
            print(f"FAIL {error}", file=sys.stderr)
        print(f"Static verification failed with {len(errors)} issue(s).", file=sys.stderr)
        return 1

    cs_count = sum(1 for path in ROOT.rglob("*.cs") if not is_build_output(path))
    print(f"PASS static verification ({cs_count} C# files checked).")
    print("NOTE static verification is not compilation or a RimWorld test.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
