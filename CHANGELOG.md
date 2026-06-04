# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-06-04

Maintenance / revival release. No changes to the offensive workflow or YAML
templates; focus is on bringing the project onto a supported, buildable platform.

### Changed
- Upgraded target framework from **.NET 6.0** (end of life) to **.NET 8.0 (LTS)**.
- Updated all NuGet dependencies to current versions:
  - `LibGit2Sharp` 0.26.2 → 0.31.0
  - `McMaster.Extensions.CommandLineUtils` 4.0.2 → 4.1.1
  - `Microsoft.Build` 17.4.0 → 17.11.4
  - `System.Configuration.ConfigurationManager` 7.0.0 → 8.0.1
  - Migrated from the unmaintained `YamlDotNet.NetCore` 1.0.0 to `YamlDotNet` 18.0.0
- Modernised the obsolete `WebClient` download path to `HttpClient`.
- Updated `GitHelpers` for the new LibGit2Sharp `CloneOptions.FetchOptions` API.

### Added
- `nuget.config` so the bundled `DonutCore` package resolves from `ExternalResources/`
  for reproducible restores (no manual NuGet feed setup required).
- GitHub Actions CI (`build`) and release (`release`) workflows.
- Dependabot configuration for NuGet and GitHub Actions.
- Community health files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`,
  issue/PR templates, `.editorconfig`, and this changelog.

## [2.0.0] - 2023

- Almost complete code rewrite.
- Cloning from private repositories (authentication via GitHub authToken).
- Copy a local folder instead of cloning from a remote repository.
- New module to generate shellcodes with Donut.
- New modules to randomise application GUIDs and AssemblyInfo.
- 60 new tools added.
