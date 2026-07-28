# Public releases

LMU Overlay ships as a portable, self-contained Windows x64 ZIP. It does not
need a system-wide .NET installation and does not install drivers, injectors,
hooks, services, or game files.

## Create a local package

From the repository root:

```powershell
./scripts/package-release.ps1 -Version 0.1.0
```

The command creates:

- `artifacts/LMU-Overlay-0.1.0-win-x64.zip`;
- `artifacts/LMU-Overlay-0.1.0-win-x64.zip.sha256`.

The archive contains the executable, runtime dependencies, quick start, security
policy, and EAC safety model. Build outputs under `artifacts/` are ignored by Git.

## Publish on GitHub

Push a semantic version tag such as `v0.1.0`. The Release workflow builds and
runs every test suite, creates the portable package and checksum, then attaches
both files to a GitHub Release. A manually dispatched workflow builds an artifact
without publishing a release.

Code signing is intentionally not claimed. A future release may add Authenticode
after the project has a protected signing identity and secret-management policy.

## User verification

After downloading both files, users can verify the archive:

```powershell
(Get-FileHash .\LMU-Overlay-0.1.0-win-x64.zip -Algorithm SHA256).Hash
```

The value must match the hexadecimal value in the `.sha256` file.
