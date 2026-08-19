# Security Policy

## About this project

OffensivePipeline is an **offensive security / red-team automation tool** intended
for use by authorised security professionals in sanctioned engagements, research,
and training. Use it only against systems you own or are explicitly authorised to
test. The maintainers are not responsible for misuse.

## Supported versions

Only the **latest tagged release** is supported. Fixes are shipped forward; there are no
maintenance branches for older versions.

| Version | Supported |
| ------- | --------- |
| Latest release | :white_check_mark: |
| Anything older | :x: |

The latest release requires the **.NET 10 runtime**, or you can use the self-contained
archive, which bundles it.

## Responsible use

Only use OffensivePipeline, and the tools it builds, against systems for which you hold
**written authorisation**. The maintainers will not assist with, advise on, or accept
contributions aimed at unauthorised targets, and will close such requests without discussion.

## Reporting a vulnerability

If you find a security issue in OffensivePipeline itself (for example, a flaw that
could harm the operator running it), please report it privately:

- Use GitHub's **"Report a vulnerability"** button under the repository's *Security* tab
  ([direct link](https://github.com/poppopjmp/offensivepipeline/security/advisories/new)).

There is **no email channel** for security reports; private advisories are the only
supported route. Please do not open public issues containing working exploit details.
We aim to acknowledge reports within 7 days.

## Threat model

The relevant attacker here is someone who can influence what an operator's machine downloads,
builds and executes — because that is precisely what this tool does on their behalf.

### In scope

- **Arbitrary code execution triggered by a crafted `Tools/*.yml` template.** Template fields
  (`solutionPath`, `toolArguments`, `gitLink`) are interpolated into `cmd.exe` command lines
  and into generated `.bat` and `.crproj` files.
- **Path traversal via `solutionPath`** or any other template-supplied path, escaping the
  `Git/` or `Output/` working directories.
- **Credential leakage**: `authUser` / `authToken` values from a template appearing in
  `log.txt`, in console output, or in anything written under `Output/`.
- **Unverified runtime downloads.** `nuget.exe` and the ConfuserEx CLI are downloaded and then
  executed. The ConfuserEx archive ships with a SHA-256 pinned in `appsettings.json` and is
  verified before use; a way to bypass or weaken that check is a vulnerability.
  `NugetSha256` ships **empty**, so `nuget.exe` is currently downloaded *without* integrity
  verification — the run prints a warning saying so. Pinning it is tracked as an open hardening
  task; operators who want the check today can set `NugetSha256` themselves (see the README).
- **Supply-chain integrity of the vendored `DonutCore` package.** See
  [`ExternalResources/README.md`](ExternalResources/README.md) for its provenance, recorded
  checksum and known liabilities.
- **Release integrity** — anything that would let a third party substitute a release artifact.

### Out of scope

- The offensive capability of the tools OffensivePipeline builds. That is the point of the
  project.
- AV/EDR evasion efficacy, or the fact that obfuscated output is detected.
- Vulnerabilities in the third-party tool repositories that templates point at. Report those
  upstream to the tool's own maintainers.
- The fact that running this tool requires disabling antivirus on the build host.

## Verifying a release

Every release ships `SHA256SUMS.txt` and a build-provenance attestation produced by the
`release` workflow:

```bash
sha256sum -c SHA256SUMS.txt
gh attestation verify OffensivePipeline-<tag>-win-x64.zip --repo poppopjmp/offensivepipeline
```

A successful `gh attestation verify` is the supported way to confirm an archive was built by
this repository's release workflow from the tag it claims.

## Supply-chain posture

- Every GitHub Actions step is pinned to a full commit SHA, not a mutable tag.
- The only job holding `contents: write` is the release job, and it creates the release with
  the preinstalled `gh` CLI rather than a third-party action.
- `nuget.config` uses `packageSourceMapping` to pin the `DonutCore` ID to the local folder
  feed. That ID is unclaimed on nuget.org, so without the mapping restore could be redirected
  to a squatted package.
- NuGet auditing runs at `low` severity across the whole transitive graph and fails CI builds.
  `dependency-review` blocks pull requests that would introduce a high-severity advisory, and
  CodeQL analyses the C# on every pull request and weekly.

### Known outstanding liability

`DonutCore.1.0.1.nupkg` is vendored, unsigned, built for `netcoreapp2.1`, and comes from an
unmaintained fork. It also drags in `CommandLineParser` 2.6.0 and `PeNet` 1.1.1. It cannot be
removed without replacing the in-process shellcode generator; the tracked follow-up is to
invoke the maintained [`TheWover/donut`](https://github.com/TheWover/donut) CLI as an external
binary, in the same download-into-`Resources/` pattern ConfuserEx already uses.
