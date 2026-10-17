# SCIE-Port

Clean-room migration workspace for **SCIE Industrial Era** to Survivalcraft NET
2.4 `Net_x25.01.17`.

The original repository is used as a read-only input at the pinned commit
recorded in `UPSTREAM-BASELINE.md` and is excluded from version control. The
user has stated that explicit permission was obtained for the ported vehicle
assets. The public license-text caveat and attribution are recorded in
`THIRD-PARTY-NOTICES.md`.

The current milestone contains a SCNET-native loader, one creative-only test
material, and authorized previews of all four SCIE transport entities:
SteamBoat, Airship, Train and Carriage. Version 0.5.0 also adds the required
rail block and uses x25-native entity, mount, body and input interfaces. It is
an early single-player runtime probe, not a completed SCIE port; steam fuel,
carriage coupling/storage and multiplayer authority are still pending.

```powershell
.\scripts\Fetch-Upstream.ps1
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
```

See `docs/TEST-RESULTS.md` before interpreting any compatibility claim.

Agents taking over development should start with `docs/AGENT-HANDOFF.md`.
