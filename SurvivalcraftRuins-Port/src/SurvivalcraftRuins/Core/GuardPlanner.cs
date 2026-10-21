namespace SurvivalcraftRuins.Core;

/// <summary>Where a group of guards waits, and how many of them there are.</summary>
public readonly record struct GuardPost(short X, short Y, short Z, int Count);

/// <summary>
/// Works out a structure's guard roster. The budget comes from the loot on
/// offer rather than from how many markers a template happens to carry, and it
/// is derived from the whole structure so the answer is the same no matter
/// which chunk gets written first.
/// </summary>
public static class GuardPlanner
{
    public static GuardPost[] Plan(StructureVariant variant, StructureData data)
    {
        var spec = variant.Guards;
        if (spec == null)
        {
            return Array.Empty<GuardPost>();
        }

        // Guards stand with the loot. Template ambush markers are only used
        // when a structure has no chests at all: markers imported from the
        // source structures sit wherever that build happened to put a spawner,
        // which scattered guards through rooms with nothing worth guarding.
        // The exception is a structure that deliberately places its own markers
        // (PostsAtMarkers) because the loot sits on top of a trap trigger.
        var anchors = new List<(short X, short Y, short Z)>();
        if (spec.PostsAtMarkers)
        {
            foreach (var marker in data.Ambushes)
            {
                anchors.Add((marker.X, marker.Y, marker.Z));
            }
        }
        if (anchors.Count == 0)
        {
            foreach (var chest in data.Chests)
            {
                anchors.Add((chest.X, chest.Y, chest.Z));
            }
        }
        if (anchors.Count == 0)
        {
            foreach (var marker in data.Ambushes)
            {
                anchors.Add((marker.X, marker.Y, marker.Z));
            }
        }
        if (anchors.Count == 0)
        {
            return Array.Empty<GuardPost>();
        }

        anchors.Sort((a, b) =>
        {
            int c = a.Y.CompareTo(b.Y);
            if (c != 0) return c;
            c = a.Z.CompareTo(b.Z);
            if (c != 0) return c;
            return a.X.CompareTo(b.X);
        });

        int total = spec.TotalFor(data.Chests.Count);
        int posts = Math.Min(anchors.Count, total);
        if (posts <= 0)
        {
            return Array.Empty<GuardPost>();
        }

        // Spread the budget evenly; the remainder goes to the lowest posts,
        // which are the ones deepest inside the structure.
        var result = new GuardPost[posts];
        int each = total / posts;
        int extra = total % posts;
        for (int i = 0; i < posts; i++)
        {
            result[i] = new GuardPost(anchors[i].X, anchors[i].Y, anchors[i].Z, each + (i < extra ? 1 : 0));
        }
        return result;
    }
}
