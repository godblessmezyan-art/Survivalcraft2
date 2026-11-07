using System;
using System.Collections.Generic;
using SCIEPort.Features.Home;
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    private static void Main()
    {
        Check(HomeRules.Facing(0,1)==0 && HomeRules.Facing(1,0)==1 && HomeRules.Facing(0,-1)==2 && HomeRules.Facing(-1,0)==3,"authored negative-Z fronts face the player in all cardinal orientations");
        var models = new HashSet<string>();
        for (int id = HomeRules.First; id <= HomeRules.Last; id++)
        {
            var variants = new HashSet<int>();
            for (int data = 0; data < 16384; data++)
            {
                int clean = HomeRules.Normalize(id, data), item = HomeRules.ItemData(id, data);
                Check(HomeRules.Normalize(id, clean) == clean, "idempotent untrusted DATA normalization");
                Check(HomeRules.Rotation(clean) == (data & 3), "rotation survives normalization");
                Check(HomeRules.Variant(clean) < HomeRules.VariantCount(id), "variant always in authored range");
                Check(HomeRules.Rotation(item) == 0 && !HomeRules.IsOpen(item), "drops never retain placement/open state");
                if (HomeRules.IsTall(id))
                {
                    int upper = HomeRules.UpperData(id, clean);
                    Check(HomeRules.Pair(id, clean, HomeRules.Upper, upper), "upper kind/state/rotation roundtrip");
                    Check(!HomeRules.Pair(id, clean, HomeRules.Upper, upper ^ 64), "foreign paired kind rejected");
                    Check(!HomeRules.Pair(id, clean, 1, upper), "foreign upper contents rejected");
                }
                variants.Add(item); models.Add(HomeRules.Model(id, clean));
            }
            Check(variants.Count == HomeRules.VariantCount(id), "creative/drop variant closure");
        }
        Check(models.Count == 16, "all sixteen runtime mesh variants are addressable");
        Check(!HomeRules.IsStorage(HomeRules.Sink) && !HomeRules.IsStorage(HomeRules.Upper), "decorative and upper cells never create inventories");
        int cell = 0, writes = 0; Exception error;
        Func<int> read = () => cell;
        Action<int> write = value => { writes++; cell = value; };
        Check(HomeCellTransaction.Apply(read, write, 0, 963, out error) == HomeCellTransaction.Result.Applied && cell == 963 && writes == 1, "air-only commit");
        cell = 10; writes = 0;
        Check(HomeCellTransaction.Apply(read, write, 0, 963, out error) == HomeCellTransaction.Result.Conflict && cell == 10 && writes == 0, "occupied/stale/cancelled plan never writes");
        cell = 0;
        Check(HomeCellTransaction.Apply(read, v => { }, 0, 963, out error) == HomeCellTransaction.Result.Rejected && cell == 0, "permission veto is detected");
        cell = 0; writes = 0;
        Check(HomeCellTransaction.Apply(read, v => { writes++; cell = v; if (v == 963) throw new Exception("after-write failure"); }, 0, 963, out error) == HomeCellTransaction.Result.RolledBack && cell == 0 && writes == 2 && error != null, "partial commit captures undo before mutation");
        cell = 0;
        Check(HomeCellTransaction.Apply(read, v => { if (v == 0) throw new Exception("undo failed"); cell = v; throw new Exception("commit failed"); }, 0, 963, out error) == HomeCellTransaction.Result.RollbackFailed && error is AggregateException, "undo failure is distinguishable for batch halt");
        cell = 0; writes = 0;
        Check(HomeCellTransaction.Apply(read, v => { writes++; cell = 7; throw new Exception("foreign replacement"); }, 0, 963, out error) == HomeCellTransaction.Result.Conflict && cell == 7 && writes == 1, "undo cannot erase foreign replacement");
        Console.WriteLine("Home DATA, two-cell ownership and conditional rollback: " + checks + " checks passed.");
    }
}
