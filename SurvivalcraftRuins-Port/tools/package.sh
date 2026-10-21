#!/usr/bin/env bash
set -euo pipefail

echo "The upstream dual-runtime packager is intentionally disabled in this target-specific port." >&2
echo "Build SCNET x25.01.17 with scripts/Build-NetMod.ps1 against the real Survivalcraft.exe." >&2
echo "The original SCAPI design and source history remain documented, but .scmod and .netmod binaries must not be mixed." >&2
exit 2
