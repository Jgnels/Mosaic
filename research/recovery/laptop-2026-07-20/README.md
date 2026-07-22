# Laptop Recovery — 2026-07-20

## Outcome

The emergency rescue successfully preserved the current RimWorld closure source, installed mod,
RimWorld saves and LocalLow runtime state, experiment archives through SyntheticLab v31, historical
projects, handoffs, and collaboration records. Only secret-safe, non-runtime material needed for
canonical project continuity is committed here. The full raw rescue remains local and ignored.

## Source inputs

| Artifact | SHA-256 | Disposition |
|---|---|---|
| `Dagmay.zip` | `C8E651130B8958859776271C2267AA0BF8B525EE767949EBA329D78EB9BEC77F` | Full raw copy retained locally; newest source selectively imported |
| `Dagmay_Emergency_Laptop_Rescue.zip` | `AF5312C45AB182903B0C0795DE99C334A388C504C765FE59CF9862329315531C` | Full raw copy retained locally; runtime data not committed |
| `test from laptop.pdf` | `50A8A1CC6EB7EE11D9C82E3AAC75C95F1CA393754F0C8BFEAC18ED1F9534E371` | Committed as contextual evidence |
| rescue conversation text | `76E368D8446C1D71D8FBD95A2581CD38C0205739CD877782F504C114C704D6AC` | Committed as contextual evidence |

## Recovered runtime state

The raw emergency archive contains RimWorld saves including `Nelson.rws` and backups, five
experience journals, identity/reflection stores and backups, installed assemblies, LocalLow logs
and configuration, and numerous historical source packages. These are private runtime/identity data
and are deliberately excluded from Git.

Do not restore `HourTest` by pairing it with the later 116-record external journal. The recovered
conversation records that the later journal belongs to the Nelson continuation; the older HourTest
save correctly entered read-only mode when presented with it.

## Canonical source decision

The newest source is imported at `rimworld/0.1-closure-candidate/`. It self-identifies as 0.1K but
includes the later 0.1L/0.1M test harnesses and read-only scheduler correction. Exact 0.1RC status is
not separately proven and remains unassigned.

The rescued `Dagmay_SyntheticLab_v31.0.zip` has SHA-256
`8B7DC45AD967279E8FD74EFFF3D89DC4A23636B32034A448FE9D0AF7F5F09007`, matching the previously
recorded canonical v31 package. The repository's `syntheticlab/` has since advanced with Mosaic
boundary and persistent-character work, so the recovered v31 tree was preserved in the raw archive
rather than copied over newer canonical source.

## Verification evidence

The recovered successful Windows build record reports all stages complete against RimWorld
1.6.4871. Preserved result artifacts report:

- 41/41 contract tests passed (recorded in the build log);
- 6/6 integration scenarios passed, 603 assertions;
- persistence torture passed, 238 assertions;
- failure isolation passed, 103 assertions;
- RimWorld assembly and package build succeeded with zero reported warnings/errors; and
- live social-path certification passed after a real save/reload, according to the recovered release
  status and conversation evidence.

The imported tree subsequently completed a fresh desktop development loop on 2026-07-20:

- 75-file static verification passed;
- 42/42 contract tests passed;
- 6/6 integration scenarios and 603 assertions passed;
- Core, Providers, and the RimWorld assembly compiled with zero warnings/errors; and
- a validated package was produced with SHA-256
  `F32E33D7432E2A564433C7B881874BBAD3982B45DD3C3704C6414D9850CEECB2`.

The mod was not installed or launched. No new live RimWorld or long-session-soak claim is made.

## Privacy and secrets

The imported source passed its built-in secret scan. An additional pattern scan found no embedded
Google API credential. The only secret-like literal is the intentional
`fixture-secret-never-log` test value. Exact raw archives remain in the ignored local recovery
directory and must not be pushed.
