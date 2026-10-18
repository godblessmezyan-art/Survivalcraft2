using System.Collections.Generic;
using Game;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Reads a rectangular region of the LIVE world (between two absolute corner
    /// cells) and turns it into a pure, terrain-independent <see cref="Blueprint"/>
    /// that can be saved as JSON, rotated and pasted. This is the read side of the
    /// blueprint bridge - it only reads, never writes, so a copy can never damage the
    /// source structure.
    ///
    /// Fidelity policy (schema 1): only vanilla blocks in
    /// <see cref="VanillaBlockPalette.SupportedPlainCubes"/> are captured. Every other
    /// non-air cell is classified and SKIPPED with a per-type count so the caller can
    /// tell the user exactly what will not be reproduced (stairs, slabs, oriented
    /// logs, plants, snow, fluids, entity-bearing blocks). We never write a guessed or
    /// degraded block (AGENTS.md: no garbage), and we never touch fluids/plants at all.
    /// </summary>
    internal static class BlueprintCopier
    {
        public sealed class Result
        {
            public Blueprint Blueprint;
            public string Name;
            public int Scanned;
            public int Copied;
            public int SkippedAir;
            public int SkippedNotReady;
            public int SkippedUnknown;
            public int SkippedUnsupported;   // non-cube / fluid / plant / unverified block
            public readonly Dictionary<string, int> UnsupportedByType = new Dictionary<string, int>(System.StringComparer.Ordinal);
            public string Failure;            // non-null when the selection is illegal (cap/bounds)

            public bool HasFailure { get { return !string.IsNullOrEmpty(Failure); } }
            public bool HasUnsupported { get { return SkippedUnsupported > 0; } }

            /// <summary>A concise, user-facing summary of what was captured and skipped.</summary>
            public string Describe()
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("已捕获 ").Append(Copied).Append(" 个可复制方块（空气 ").Append(SkippedAir)
                  .Append("，未就绪 ").Append(SkippedNotReady)
                  .Append("，未知 ").Append(SkippedUnknown)
                  .Append("，暂不支持 ").Append(SkippedUnsupported).Append("）。");
                if (UnsupportedByType.Count > 0)
                {
                    sb.Append(" 跳过的方块类型：");
                    int shown = 0;
                    foreach (KeyValuePair<string, int> kv in UnsupportedByType)
                    {
                        if (shown >= 6) { sb.Append("…"); break; }
                        if (shown > 0) sb.Append("、");
                        sb.Append(kv.Key).Append('x').Append(kv.Value);
                        shown++;
                    }
                    sb.Append('。');
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Build a blueprint from the inclusive box spanned by two absolute corners.
        /// Returns a Result whose Blueprint is null on a hard failure (illegal extent)
        /// or when nothing supported was captured (Copied == 0).
        /// </summary>
        public static Result Copy(GameTerrainGateway gateway, CellPos cornerA, CellPos cornerB, string name)
        {
            var result = new Result { Name = name ?? "copied_structure" };

            if (gateway == null)
            {
                result.Failure = "地形尚未就绪，无法复制。";
                return result;
            }

            int minX = cornerA.X < cornerB.X ? cornerA.X : cornerB.X;
            int maxX = cornerA.X > cornerB.X ? cornerA.X : cornerB.X;
            int minY = cornerA.Y < cornerB.Y ? cornerA.Y : cornerB.Y;
            int maxY = cornerA.Y > cornerB.Y ? cornerA.Y : cornerB.Y;
            int minZ = cornerA.Z < cornerB.Z ? cornerA.Z : cornerB.Z;
            int maxZ = cornerA.Z > cornerB.Z ? cornerA.Z : cornerB.Z;

            int dx = maxX - minX + 1;
            int dy = maxY - minY + 1;
            int dz = maxZ - minZ + 1;

            if (dx > BuildSafetyLimits.MaxSelectionAxisLength ||
                dy > BuildSafetyLimits.MaxSelectionAxisLength ||
                dz > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                result.Failure = "选区单轴长度 " + dx + "x" + dy + "x" + dz +
                    " 超过上限 " + BuildSafetyLimits.MaxSelectionAxisLength + "，请缩小选区。";
                return result;
            }
            if (dy > WorldBounds.MaxYExclusive)
            {
                result.Failure = "选区高度超出世界范围。";
                return result;
            }

            var builder = new BlueprintBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var abs = new CellPos(x, y, z);
                        var relative = new CellPos(x - minX, y - minY, z - minZ);
                        result.Scanned++;

                        int value;
                        if (!gateway.TryGetCellValue(abs, out value))
                        {
                            result.SkippedNotReady++;
                            continue;
                        }

                        int contents = Terrain.ExtractContents(value);
                        if (contents == 0)
                        {
                            result.SkippedAir++;
                            continue;
                        }

                        string typeName = GameTerrainGateway.TypeNameForContents(contents);
                        if (typeName == null)
                        {
                            result.SkippedUnknown++;
                            continue;
                        }

                        if (VanillaBlockPalette.SupportedPlainCubes.Contains(typeName))
                        {
                            builder.Set(relative, typeName); // concrete vanilla key the palette can rebuild
                            result.Copied++;
                        }
                        else
                        {
                            // Fluid / plant / snow / stair / slab / oriented / entity block:
                            // classify, count and skip. Never copy something we cannot reproduce.
                            result.SkippedUnsupported++;
                            int prev;
                            result.UnsupportedByType.TryGetValue(typeName, out prev);
                            result.UnsupportedByType[typeName] = prev + 1;
                        }

                        if (builder.Count > BuildSafetyLimits.MaxBlocksPerOperation)
                        {
                            result.Failure = "选区内可复制方块已超过每事务上限 " +
                                BuildSafetyLimits.MaxBlocksPerOperation + "，请缩小选区。";
                            result.Blueprint = null;
                            return result;
                        }
                    }
                }
            }

            if (result.Copied == 0)
            {
                result.Blueprint = null;
                return result;
            }

            result.Blueprint = builder.ToBlueprint(result.Name, BlueprintJson.CurrentSchemaVersion);
            return result;
        }
    }
}
