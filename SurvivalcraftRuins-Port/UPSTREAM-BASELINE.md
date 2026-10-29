# Upstream baseline

- Imported repository: <https://github.com/godblessmezyan-art/SurvivalcraftRuins>
- Imported branch: `main`
- Imported commit: `53425779dc499c6f08b94f4b973f329217f92765`
- Commit author/subject: `zh667` — `v1.0.0: MC temples, guards budgeted by loot, and old-world support`
- Repository relationship: the imported repository identifies itself as a fork of
  <https://github.com/zh667/SurvivalcraftRuins>.
- License: Mulan Permissive Software License v2 (`LICENSE`)
- Imported `LICENSE` SHA-256:
  `5D2BED72B295522B941633247C6F63DEE7A3223650635EDB55D9156F866DC659`

The complete upstream source, 13 JSON structure templates, loot configuration,
tests, documentation, author metadata and license were retained. The SCNET port
does not include upstream binaries, game binaries, APKs, PDBs, content packs or
player saves.

The upstream Bash dual-runtime packager is intentionally disabled in this
target-specific tree because it emitted unverified SCNET version numbers and a
`net10.0` binary. Its development intent remains visible in Git history; the
SCNET artifact is produced only by `scripts/Build-NetMod.ps1`. No `.scmod` is
renamed or repackaged as `.netmod`.
