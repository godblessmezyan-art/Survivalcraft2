using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;

namespace SCIEPort.Features.Home
{
    public sealed partial class SubsystemHomeFurniture
    {
        private readonly HashSet<Point3> footprintBusy = new HashSet<Point3>();
        private int Signature(Point3 p) => Terrain.ReplaceLight(Value(p), 0);
        private static int PartValue(int id, int rotation, int part)
            => Terrain.MakeBlockValue(HomeRules.PartId(id), 0, (rotation & 3) | (part << 2));
        private bool TryFootprintRoot(Point3 point, int value, out Point3 root)
        {
            root = point;
            int id = Terrain.ExtractContents(value), data = Terrain.ExtractData(value), part = (data >> 2) & 3;
            if (!HomeRules.IsFootprintPart(id) || part == 0 || data != ((part << 2) | (data & 3))) return false;
            Point3 delta = HomeExtensionGeometry.Offset(data & 3, part);
            long x = (long)point.X - delta.X, z = (long)point.Z - delta.Z;
            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue) return false;
            root = new Point3((int)x, point.Y, (int)z);
            if (!Ready(root)) return false;
            int actual = Value(root);
            return Terrain.ExtractContents(actual) == HomeRules.PartRootId(id) && Terrain.ExtractData(actual) == (data & 3);
        }
        private void HaltFootprints(Exception error)
        {
            placementHalted = true; added.Clear(); waiting.Clear(); placers.Clear();
            Log.Error("[SCIEPort:Home] Footprint placement halted after undo failure; retained cells require inspection: " + error);
        }
        private void InitializeFootprint(Point3 root, int rootValue, ComponentMiner miner)
        {
            if (!footprintBusy.Add(root)) return;
            int id = Terrain.ExtractContents(rootValue), rotation = HomeRules.Rotation(Terrain.ExtractData(rootValue));
            var cells = new Point3[3]; var before = new int[3]; var after = new int[3];
            var written = new List<int>(3); bool applied = false, halted = false;
            try
            {
                // Capture the entire bounded air-only plan before the first proxy write.
                for (int i = 0; i < 3; i++)
                {
                    if (!HomeExtensionGeometry.Cell(root, rotation, i + 1, out cells[i]) || !Ready(cells[i])) return;
                    before[i] = Signature(cells[i]); after[i] = PartValue(id, rotation, i + 1);
                    if (before[i] != after[i] && Terrain.ExtractContents(before[i]) != 0) return;
                }
                for (int i = 0; i < 3; i++)
                {
                    if (Signature(root) != Terrain.ReplaceLight(rootValue, 0)) return;
                    if (before[i] == after[i]) continue;
                    int index = i; Exception error;
                    var result = HomeCellTransaction.Apply(() => Signature(cells[index]), v => Change(cells[index], v, miner), before[i], after[i], out error);
                    if (result == HomeCellTransaction.Result.Applied) { written.Add(i); continue; }
                    if (result == HomeCellTransaction.Result.RollbackFailed) { halted = true; HaltFootprints(error); }
                    else if (error != null) Log.Error("[SCIEPort:Home] Footprint write interrupted: " + error);
                    return;
                }
                applied = true;
            }
            catch (Exception error) { Log.Error("[SCIEPort:Home] Footprint initialization interrupted: " + error); }
            finally
            {
                if (!applied)
                {
                    for (int n = written.Count - 1; n >= 0; n--)
                    {
                        int i = written[n];
                        try
                        {
                            if (Signature(cells[i]) != after[i]) continue; // Preserve any foreign replacement.
                            Change(cells[i], before[i], miner);
                            if (Signature(cells[i]) != before[i]) { halted = true; HaltFootprints(new InvalidOperationException("Footprint rollback was rejected.")); }
                        }
                        catch (Exception error) { halted = true; HaltFootprints(error); }
                    }
                }
                footprintBusy.Remove(root);
                if (!applied && !halted)
                {
                    try { if (Ready(root) && Signature(root) == Terrain.ReplaceLight(rootValue, 0)) RemoveRoot(root, miner); }
                    catch (Exception error) { HaltFootprints(error); }
                }
            }
        }
        private void RemoveFootprintRootFromPart(Point3 part, int oldValue)
        {
            Point3 root;
            if (TryFootprintRoot(part, oldValue, out root) && !footprintBusy.Contains(root)) RemoveRoot(root, null);
        }
        private void RemoveFootprintParts(Point3 root, int oldValue)
        {
            if (!footprintBusy.Add(root)) return;
            try
            {
                int id = Terrain.ExtractContents(oldValue), rotation = HomeRules.Rotation(Terrain.ExtractData(oldValue));
                for (int i = 1; i < 4; i++)
                {
                    Point3 p;
                    if (!HomeExtensionGeometry.Cell(root, rotation, i, out p) || !Ready(p) || Signature(p) != PartValue(id, rotation, i)) continue;
                    // Structural cleanup of verified owned occupancy after authoritative removal.
                    SubsystemTerrain.ChangeCellNet(p.X, p.Y, p.Z, 0);
                    CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(p.X, p.Y, p.Z, 0));
                }
            }
            finally { footprintBusy.Remove(root); }
        }
        private void CheckFootprint(Point3 root, int value)
        {
            if (waiting.Contains(root) || footprintBusy.Contains(root)) return;
            int id = Terrain.ExtractContents(value), rotation = HomeRules.Rotation(Terrain.ExtractData(value));
            for (int i = 1; i < 4; i++)
            {
                Point3 p;
                if (!HomeExtensionGeometry.Cell(root, rotation, i, out p) || !Ready(p)) return;
                if (Signature(p) != PartValue(id, rotation, i)) { RemoveRoot(root, null); return; }
            }
        }
        private bool InteractionBelongsTo(Point3 root, int id, Point3 hit)
        {
            if (hit.X == root.X && hit.Y == root.Y && hit.Z == root.Z) return true;
            if (HomeRules.IsTall(id)) return hit.X == root.X && hit.Y == root.Y + 1 && hit.Z == root.Z;
            Point3 actual;
            return HomeRules.HasFootprint(id) && Ready(hit) && TryFootprintRoot(hit, Value(hit), out actual)
                && actual.X == root.X && actual.Y == root.Y && actual.Z == root.Z;
        }
        private void TrySleep(ComponentPlayer player, Point3 root, int data, Client reply)
        {
            // Use native Sleep/CanSleep and its existing packet; no teleport, private
            // sleep-state editing, new protocol, or blanket bypass of wetness/shelter.
            var body = player.ComponentBody; Vector3 position = body.Position;
            bool standingOnThisBed = false;
            if (body.StandingOnValue.HasValue && (Terrain.ExtractContents(body.StandingOnValue.Value) == HomeRules.Bed || Terrain.ExtractContents(body.StandingOnValue.Value) == HomeRules.BedPart)
                && position.Y >= root.Y + .5f && position.Y <= root.Y + 1.15f)
            {
                for (int i = 0; i < 4; i++)
                {
                    Point3 p;
                    if (HomeExtensionGeometry.Cell(root, data & 3, i, out p) && position.X >= p.X && position.X < (double)p.X + 1
                        && position.Z >= p.Z && position.Z < (double)p.Z + 1) { standingOnThisBed = true; break; }
                }
            }
            string reason = "请站到床上";
            if (standingOnThisBed && player.ComponentSleep.CanSleep(out reason)) { player.ComponentSleep.Sleep(true); return; }
            if (reply != null) CommonLib.Net.QueuePackage(new ComponentSleepPackage(player.ComponentSleep, ComponentSleepPackage.EventType.Sleep, false, false, reason) { To = reply });
            else if (player.PlayerData.IsMainPlayer) player.ComponentGui.DisplaySmallMessage(reason, Color.White, false, false);
        }
    }
}
