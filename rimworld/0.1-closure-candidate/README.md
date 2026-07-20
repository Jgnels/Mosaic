# Dagmay

> Research project for persistent artificial individuals in simulated environments.

Project owner or new implementation agent: begin with [START_HERE.md](START_HERE.md).

## Status

**Current phase:** Version 0.1M Failure Isolation passed in the deterministic closure harness. The frozen 0.1J.3 cognitive baseline is unchanged; 0.1K social certification and 0.1L live persistence torture have passed in RimWorld.

- **Designed:** Version 0.1 cognition remains frozen at 0.1J.3; 0.1K–0.1M add release qualification only.
- **Implemented:** active-store social certification, persistence torture, and deterministic failure isolation are present with one-command evidence runners.
- **Statically verified:** `tools/static_verify.py` passes across 74 C# files and all 10 PowerShell scripts parse.
- **Compiled:** Core, Providers, Tests, IntegrationHarness, and the RimWorld adapter compile on Windows; the adapter builds with zero warnings and zero errors against RimWorld 1.6.4871 assemblies.
- **Tested in isolation:** 41/41 contract tests and all six integration scenarios pass. The integration harness records 603 assertions, including 238 persistence-torture and 103 failure-isolation assertions.
- **Tested in RimWorld:** 0.1K social certification returned active-store `Status=PASS`; 0.1L preserved identity/history and a pending queue through eight loads across two processes. 0.1M reuses those live outage/shutdown and prior checkpoint-recovery observations while keeping destructive fault injection isolated.
The project targets RimWorld 1.6 on Windows with all DLC, including Odyssey. The current owner test installation is 1.6.4871. Version 0.1K remains Observer-only, freezes the 0.1 cognitive feature set, and adds no pawn-control behavior.

## Mission

Dagmay explores whether a software individual can remain coherent through time when it has bounded perception, durable memory, relationships, beliefs, values, needs, emotions, goals, reflection, and eventually constrained agency in a simulated world.

The project is an engineering and research effort. It does not claim to create or detect consciousness, sentience, suffering, or human-equivalent emotion.

## Foundational invariant

**An individual is defined by the continuity of its identity through time, not by the specific language model that generates its thoughts.**

Operationally, continuity lives in a single authorized lineage of versioned identity state, memories, relationships, commitments, and causally recorded changes. A model is a replaceable cognitive instrument that may propose interpretations or updates; it is not the individual and may never be the sole store of identity.

## Why the name matters

Dagmay is named after the sacred handwoven abaca textile of the Mandaya people of Davao Oriental, Mindanao. The project uses weaving as an architectural metaphor: experiences and memories are threads; recurring values, relationships, fears, and themes become motifs; reflection integrates them into an evolving pattern.

This metaphor belongs to the project, not as a claim about Mandaya belief or technology. The name must be presented with cultural context, credible sources, and care. Before a broad public release, the wording, visual identity, and use of the name should receive informed cultural review. Dagmay must not be reduced to an exotic decorative theme.

## Version 0.1: Observer

Version 0.1 will:

- create stable identity records for colonists from grounded RimWorld data;
- observe a deliberately small set of meaningful events from bounded perspectives;
- store factual events separately from subjective memories and beliefs;
- queue reflection safely and continue recording while offline;
- use validated, auditable state mutations;
- expose ordinary pawn and colony views plus a clearly separated Observer Mode;
- archive deaths without erasing history; and
- make **no changes to pawn behavior**.

The complete acceptance criteria are in [docs/03_Roadmap.md](docs/03_Roadmap.md).

## Repository layout

```text
Dagmay/
├── README.md
├── START_HERE.md                  # plain-language owner/local-agent entry point
├── AGENTS.md                      # durable rules for humans and coding agents
├── .codex/agents/                 # project-scoped local Codex agent definition
├── Directory.Build.props          # shared strict C# build settings
├── Dagmay.sln                     # solution entry point
├── Dagmay.Core/                   # game- and provider-independent individual
├── Dagmay.Providers/              # Google AI Studio first; local providers later
├── Dagmay.RimWorld/               # Verse/Harmony adapter, persistence bridge, UI
├── Dagmay.Tests/                  # fast unit and contract tests
├── Dagmay.IntegrationHarness/     # deterministic end-to-end autonomous scenarios
├── docs/                          # living design and decision record
├── prompts/                       # provider-neutral, versioned task instructions
├── schemas/                       # versioned structured-output contracts
└── tools/                         # packaging, schema inspection, diagnostics
```

Dependency direction:

```text
Dagmay.RimWorld ──────> Dagmay.Core
       │
       └──────────────> Dagmay.Providers ──────> Dagmay.Core

Dagmay.Tests ───────> Core + Providers
Dagmay.IntegrationHarness ───────> Core + Providers
```

`Dagmay.Core` must not reference RimWorld, Unity, Harmony, or a provider SDK. Environment adapters translate world-specific data into Dagmay contracts. Providers implement interfaces owned by the core.

## Documentation map

- [Foundational Principles](docs/00_Foundational_Principles.md)
- [Project Vision](docs/01_Project_Vision.md)
- [System Architecture](docs/02_System_Architecture.md)
- [Roadmap and v0.1 acceptance criteria](docs/03_Roadmap.md)
- [Ethics and Observer Mode](docs/04_Ethics_and_Observer_Mode.md)
- [Memory Model](docs/05_Memory_Model.md)
- [RimWorld Adapter](docs/06_RimWorld_Adapter.md)
- [Model Provider Interface](docs/07_Model_Provider_Interface.md)
- [Testing Strategy](docs/08_Testing_Strategy.md)
- [Open Questions](docs/09_Open_Questions.md)
- [Decision Log](docs/10_Decision_Log.md)
- [Development Guide](docs/11_Development_Guide.md)
- [Runtime Budgeting](docs/12_Runtime_Budgeting.md)
- [Implemented Data Contracts](docs/13_Data_Contracts.md)
- [AI Collaboration Guide](docs/14_AI_Collaboration_Guide.md)
- [Verification Record](docs/15_Verification_Record.md)
- [Version 0.1B Beginner Test Guide](docs/16_V0.1B_Test_Guide.md)
- [Version 0.1C Beginner Test Guide](docs/17_V0.1C_Test_Guide.md)
- [External Mod Reference Review](docs/18_External_Mod_Reference_Review.md)
- [Version 0.1D Beginner Test Guide](docs/19_V0.1D_Test_Guide.md)
- [Version 0.1D.1 Configuration Hotfix Test Guide](docs/20_V0.1D.1_Hotfix_Test_Guide.md)
- [Version 0.1E Restricted Observer Test Guide](docs/21_V0.1E_Test_Guide.md)
- [Version 0.1F Development Automation and Local Codex Guide](docs/22_V0.1F_Development_Automation.md)
- [Version 0.1G Ordinary Mind View](docs/23_V0.1G_Ordinary_Mind_View.md)
- [Version 0.1H Experience Consolidation](docs/24_V0.1H_Experience_Consolidation.md)
- [Version 0.1I Salience & Reflection Admission](docs/25_V0.1I_Salience_Admission.md)
- [Version 0.1J Social Experience & Relationship Foundations](docs/26_V0.1J_Social_Relationships.md)
- [Version 0.1J.3 Checkpoint Recovery](docs/27_V0.1J3_Checkpoint_Recovery.md)
- [Automated Integration Harness](docs/28_Automated_Integration_Harness.md)
- [Version 0.1K Social Path Certification](docs/29_V0.1K_Social_Path_Certification.md)
- [RimWorld 0.1 Closure Plan](docs/30_V0.1_Closure_Plan.md)
- [Version 0.1L Persistence Torture Harness](docs/31_V0.1L_Persistence_Torture.md)
- [Version 0.1M Failure Isolation](docs/32_V0.1M_Failure_Isolation.md)
- [Glossary](docs/Glossary.md)

## Verification commands

Without a .NET SDK:

```text
python tools/static_verify.py
```

On the Windows RimWorld computer after installing the documented prerequisites:

```powershell
.\tools\dev-loop.ps1
```

The PowerShell command runs static checks when Python is available, compiles Core and providers, executes the dependency-free contract tests, runs the deterministic integration harness, builds against the locally installed RimWorld assemblies, creates a mod archive and SHA-256, and retains complete, structured, and integration-test evidence. Add `-Install` for guarded backup/install/rollback and `-Launch` to start RimWorld only after a successful install. See `START_HERE.md` for the beginner path.

## Development rule

Every claim about project maturity must use one of these labels: **Designed**, **Implemented**, **Compiled**, **Tested in isolation**, or **Tested in RimWorld**. Later stages never follow automatically from earlier ones.

## 0.1K closure rule

0.1K begins release qualification. New SyntheticLab research mechanisms are not imported
into RimWorld 0.1 during closure unless they are required to repair a release-blocking
defect. The immediate RimWorld gate is a real social-path certification PASS, followed by
persistence torture, failure isolation, and soak testing.
