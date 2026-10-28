# BreakBox Security (Studio) — CLAUDE.md

BreakBox Studio: my local reverse-engineering tools. Written by Max-Anton Horvat, HBO-i S6 "Complex
Software Systems". Signature 0x4D414836 = "MAH6". Canonical rules for this repo: read it before building.

## Stack
- **.NET 9**, C#. Solution: `BreakBox.Security.sln`.
- `BreakBox.Studio`: an **Avalonia** desktop app (`WinExe`). Uses **Mono.Cecil** for IL inspection and
  rewriting (the Flip tool that patches `ldc.i4.0` -> `ldc.i4.1`). References `BreakBox.Core`.
- `BreakBox.Studio.Tests`: **xUnit** + **coverlet** coverage (`coverlet.runsettings`).
- Local-only. This repo is never deployed; it is the attacker-side toolkit run against my own targets.

## Architecture rules (do not break these)
- The tools only ever operate on **my own or explicitly authorised targets**. No third-party binaries, no real
  malware, no distribution of weaponised tooling. This is a learning lab and stays one.
- IL rewriting goes through Mono.Cecil on a copy; keep the original input untouched so a run is repeatable.
- Logic that can be tested without the UI lives outside the Avalonia view layer, so it is unit-testable (SOLID:
  keep the patching/analysis core separate from the window).

## Engineering standards (non-negotiable, every change)
1. **KISS / YAGNI / SOLID.** Simplest thing that works; build only what the task needs; keep the analysis/patch
   core small and independent of the UI.
2. **Strict TDD, red-green-refactor:** write ONE failing test, run it and see it FAIL, write the minimum code to
   pass, run it GREEN, then refactor. Test-after is not allowed.
3. **No "done" without real proof:** run the tests and the build and paste the ACTUAL pass/fail output. Never
   "should pass".
4. **No refactor outside the task scope.**

## Verify (run before declaring any task finished)
```
dotnet build
dotnet test                                   # add --settings coverlet.runsettings for coverage
```
Both must be green, with the real output shown. `/verify` runs this for you. CI runs the same on every PR.

**Restore prerequisite:** this repo consumes `BreakBox.Core` and `BreakBox.Generator` from **GitHub Packages**
(published by breakbox-api), per `nuget.config`. A local `dotnet build`/`restore` needs that source added
once with a `read:packages` PAT:
`dotnet nuget add source https://nuget.pkg.github.com/AntonDesigns/index.json -n github -u <user> -p <PAT>`.
Without it, restore fails with `NU1101: Unable to find package BreakBox.Generator`. CI adds the source with
`PACKAGES_TOKEN`, so it builds there regardless.

## Conventions
- Every source file starts with the author/signature header comment, as the existing files do.
- Comments explain WHY in plain first-person, matching the surrounding files. No em dashes.
