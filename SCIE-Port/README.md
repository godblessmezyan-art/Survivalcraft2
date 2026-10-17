# SCIE-Port

Clean-room migration workspace for **SCIE Industrial Era** to Survivalcraft NET
2.4 `Net_x25.01.17`.

The original repository is used as a read-only research input at the pinned
commit recorded in `UPSTREAM-BASELINE.md`. It is intentionally excluded from
version control. Because that repository does not contain a license, this
project does not copy or redistribute its source code, models, textures, audio,
translations, or compiled dependencies.

The current milestone contains a SCNET-native loader, one creative-only test
material, and an authorized SteamBoat preview. Version 0.3.0 packages the SCIE
SteamBoat model and texture from the pinned upstream commit and uses SCNET's
native boat/mount components. The permission basis and remaining publication
record requirement are documented in `THIRD-PARTY-NOTICES.md`. It is an early
single-player runtime probe, not a completed SCIE port.

```powershell
.\scripts\Fetch-Upstream.ps1
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
```

See `docs/TEST-RESULTS.md` before interpreting any compatibility claim.
