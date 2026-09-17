# breakbox-security

The security repo of BreakBox, by Max-Anton Horvat (Complex Software Systems, S6). Signature `0x4D414836` = MAH6.

This is the security engineer's home. It holds:

- **BreakBox Studio** — my offline toolkit: **Peek** (inspect an assembly with Mono.Cecil), **Keygen** (reverse the Level 2 serial), and **Flip** (patch a check). It never touches the server.
- **The scan set** — the security scans (secret, dependency, code, SonarQube quality gate) that every BreakBox repo runs. This repo is meant to own the shared reusable workflow the others call.
- Later: the **attack-monitoring** design (the Level-9 board) and the threat research.

## Why it depends on packages, not the other repos

Studio needs `BreakBox.Core` (the contracts) and its tests need `BreakBox.Generator` (to make targets to crack). Those live in **breakbox-api**. Rather than a cross-repo project reference, they are consumed as **versioned NuGet packages** — one source of truth, and something I can sign, attach provenance to, and produce an SBOM for. That is the supply-chain (SLSA) way, and it is the point of splitting security out.

## Building it (local dev)

The packages come from a **local feed** (`.local-nuget/`, git-ignored). Pack them from the api repo:

```bash
dotnet pack ../breakbox/backend/BreakBox.Core/BreakBox.Core.csproj -c Release -o .local-nuget
dotnet pack ../breakbox/backend/BreakBox.Generator/BreakBox.Generator.csproj -c Release -o .local-nuget
dotnet test BreakBox.Security.sln -c Release
```

## The expert step (CI)

On GitHub, the build/test/CodeQL jobs pull `BreakBox.Core` / `BreakBox.Generator` from **GitHub Packages** (published by breakbox-api), with signing + provenance. They stay gated (`PACKAGES_READY=true` + a `PACKAGES_TOKEN`) until that is set up. `gitleaks` runs on every push regardless.
