# ADR 0010 — Centralized Package Management

**Status:** Accepted
**Date:** 2026-06-10

## Context

Ten projects in the solution. Some packages (EF Core, OpenTelemetry) appear
in multiple projects. Inconsistent versions cause confusing build warnings
(MSB3277, NU1605) and runtime mismatches that take attendees off the lab path.

## Decision

Use .NET's **Centralized Package Management** (CPM). All `PackageVersion`
entries live in `Directory.Packages.props` at the solution root. Individual
`.csproj` files use `<PackageReference Include="..." />` with no `Version`
attribute.

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="PackageName" Version="CurrentPackageVersion" />

<!-- src/TravelBooking.Infrastructure/TravelBooking.Infrastructure.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore" />
```

Also enabled: `CentralPackageTransitivePinningEnabled` so transitive
dependencies follow the same pins (eliminates surprise version unification).

## Consequences

- ✅ One file to audit for security updates, license review, or upgrades.
- ✅ `dotnet add package` updates the central file automatically (.NET 8+).
- ✅ Cuts transitive-version warnings to near zero.
- ⚠️ Attendees who copy a `.csproj` snippet from the lab document and add
  `Version="..."` get a build error. Lab calls out the convention.

## Alternatives considered

- **Per-project versions** — the .NET default, but produces drift across ten
  projects.
- **`Directory.Build.props`** — wrong tool; that file targets MSBuild
  properties, not package versions.
