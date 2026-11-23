using Engine;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>World-seeded passage graph shared by all slices of a three-level pyramid.</summary>
    internal sealed class PyramidMaze
    {
        internal const int Width = 8, Count = Width * Width, Levels = 3, Spacing = 6;
        internal static readonly Point3 Display = new Point3(61, -10, 64);
        internal static readonly Point3 Entrance = new Point3(37, 1, 12);
        private static readonly int[] Starts = { 3, 6 * Width + 7, Width };
        private static readonly int[] Ends = { Count - 1, 0, Count - 1 };
        private readonly byte[][] passages = new byte[Levels][];
        internal readonly int Seed;

        internal PyramidMaze(int seed)
        {
            Seed = seed;
            var random = new Game.Random(seed);
            for (int level = 0; level < Levels; level++)
                passages[level] = Generate(random, Starts[level], Ends[level]);
        }

        internal static Point3 Room(int level, int cell) => new Point3(
            19 + Spacing * (cell % Width), -6 * level, 20 + Spacing * (cell / Width));

        internal bool East(int level, int cell) => (passages[level][cell] & 1) != 0;
        internal bool South(int level, int cell) => (passages[level][cell] & 2) != 0;

        private static byte[] Generate(Game.Random random, int start, int end)
        {
            byte[] best = null;
            int length = 0;
            var distances = new int[Count];
            var queue = new int[Count];
            // Let different sections of the pyramid wind in different ways while favoring long routes.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                byte[] candidate = Carve(random, start);
                int distance = Distance(candidate, start, end, distances, queue);
                if (distance <= length) continue;
                best = candidate;
                length = distance;
            }
            // Add a few loops while protecting most of the distance between stairways.
            for (int attempt = 0, loops = 0; attempt < 48 && loops < 3; attempt++)
            {
                int cell = random.Int(0, Count - 1), direction = random.Int(0, 1);
                int neighbor = Neighbor(cell, direction);
                int bit = 1 << direction;
                if (neighbor < 0 || (best[cell] & bit) != 0) continue;
                Link(best, cell, neighbor, direction);
                if (Distance(best, start, end, distances, queue) >= (length * 9 + 9) / 10) loops++;
                else
                {
                    best[cell] &= (byte)~bit;
                    best[neighbor] &= (byte)~(1 << (direction + 2));
                }
            }
            return best;
        }

        private static byte[] Carve(Game.Random random, int start)
        {
            var graph = new byte[Count];
            var visited = new bool[Count];
            var stack = new int[Count];
            var directions = new int[4];
            visited[start] = true;
            stack[0] = start;
            for (int top = 0; top >= 0;)
            {
                int cell = stack[top], choices = 0;
                for (int direction = 0; direction < 4; direction++)
                {
                    int neighbor = Neighbor(cell, direction);
                    if (neighbor >= 0 && !visited[neighbor]) directions[choices++] = direction;
                }
                if (choices == 0)
                {
                    top--;
                    continue;
                }
                int next = directions[random.Int(0, choices - 1)];
                int target = Neighbor(cell, next);
                Link(graph, cell, target, next);
                visited[target] = true;
                stack[++top] = target;
            }
            return graph;
        }

        private static int Distance(byte[] graph, int start, int end, int[] distances, int[] queue)
        {
            for (int i = 0; i < Count; i++) distances[i] = -1;
            distances[start] = 0;
            queue[0] = start;
            for (int head = 0, tail = 1; head < tail; head++)
            {
                int cell = queue[head];
                if (cell == end) return distances[cell];
                for (int direction = 0; direction < 4; direction++)
                {
                    if ((graph[cell] & (1 << direction)) == 0) continue;
                    int neighbor = Neighbor(cell, direction);
                    if (distances[neighbor] >= 0) continue;
                    distances[neighbor] = distances[cell] + 1;
                    queue[tail++] = neighbor;
                }
            }
            return -1;
        }

        private static void Link(byte[] graph, int cell, int neighbor, int direction)
        {
            graph[cell] |= (byte)(1 << direction);
            graph[neighbor] |= (byte)(1 << ((direction + 2) % 4));
        }

        private static int Neighbor(int cell, int direction)
        {
            switch (direction)
            {
                case 0: return cell % Width < Width - 1 ? cell + 1 : -1;
                case 1: return cell / Width < Width - 1 ? cell + Width : -1;
                case 2: return cell % Width > 0 ? cell - 1 : -1;
                default: return cell / Width > 0 ? cell - Width : -1;
            }
        }
    }
}
