# ExternalResources — vendored NuGet packages

This folder is a **local NuGet folder feed**, wired up in the repository-root `nuget.config` as the
source `local-external`. It exists for exactly one package that is not available on nuget.org.

`nuget.config` also declares a `packageSourceMapping` entry restricting the `DonutCore` package ID to
this folder feed, so restore can never be redirected to a package of the same name published on
nuget.org. Do not remove that mapping.

---

## `DonutCore.1.0.1.nupkg`

| | |
|---|---|
| **Package ID / version** | `DonutCore` 1.0.1 |
| **SHA-256** | `1df5925f765215d5eb7e07be13fe15c6a09d6a9cc10f5be0ad6339f7c148331a` |
| **Size** | 29 059 bytes |
| **Authors / owners (nuspec)** | `n1xbyte, Odzhan, TheWover` |
| **Repository (nuspec)** | https://github.com/n1xbyte/donutCS |
| **Project URL (nuspec)** | https://github.com/TheWover/donut |
| **Target framework** | `netcoreapp2.1` (single asset: `lib/netcoreapp2.1/Donut.dll`, 51 200 bytes, timestamped 2021-04-11) |
| **Declared dependencies** | `CommandLineParser` 2.6.0, `PeNet` 1.1.1 |
| **Signed?** | **No.** The package carries no author or repository signature. |
| **On nuget.org?** | **No.** The `DonutCore` ID is unclaimed. |

Verify the vendored file at any time with:

```bash
sha256sum ExternalResources/DonutCore.1.0.1.nupkg
# 1df5925f765215d5eb7e07be13fe15c6a09d6a9cc10f5be0ad6339f7c148331a
```

The hash above was computed from the file as committed in this repository. It is a record of what we
ship, not an upstream-published checksum — there is no upstream publication to compare it against.

### Why it is vendored

`OffensivePipeline`'s `Donut` module calls the managed Donut shellcode generator in-process. That
generator is distributed only as this `.nupkg`, built from the C# port at `n1xbyte/donutCS`; it was
never published to nuget.org, so there is no remote source to restore it from. Committing the
`.nupkg` and pointing a folder feed at it is the only way to make `dotnet restore` reproducible.

### Known liabilities

This is the project's principal remaining supply-chain exposure, and it is deliberately **not**
addressed in this modernization pass:

- The assembly is a 2019-era `netcoreapp2.1` build from an unmaintained fork, last touched in 2021.
- It is unsigned, so authenticity rests entirely on the hash recorded above.
- It drags in `PeNet` 1.1.1, which itself pulls a stale `System.Security.Cryptography.Pkcs`
  asset. That asset is evicted by a transitive pin in `Directory.Packages.props`
  (`System.Security.Cryptography.Pkcs` 10.0.11) — do not remove that pin while `DonutCore` remains.
- It also declares `CommandLineParser` 2.6.0, which is unused by this project.

The tracked follow-up is to replace this in-process dependency with the maintained
[`TheWover/donut`](https://github.com/TheWover/donut) CLI, invoked as an external binary in the same
download-into-`Resources/` pattern that ConfuserEx already uses. That would remove the local feed
entirely.
