using System;
using System.Text;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Bounded stop-and-wait upload. No terrain or game API is used here.</summary>
    internal sealed class BlueprintTransfer
    {
        internal const int ChunkBytes = 4096, MaxBytes = 2 * 1024 * 1024, MaxUploads = 4;
        internal const double LeaseSeconds = 180;
        internal readonly Guid Request;
        internal readonly double Expires;
        private readonly byte[] bytes;
        internal int Received { get; private set; }
        internal int Length { get { return bytes.Length; } }
        internal BlueprintTransfer(Guid request, int length, double now)
        {
            if (request == Guid.Empty || length <= 0 || length > MaxBytes) throw new ArgumentException("蓝图大小无效。");
            Request = request; bytes = new byte[length]; Expires = now + LeaseSeconds;
        }
        internal bool Append(int offset, byte[] part, double now)
        {
            if (now > Expires || part == null || part.Length == 0 || part.Length > ChunkBytes ||
                offset != Received || part.Length > bytes.Length - Received) return false;
            Buffer.BlockCopy(part, 0, bytes, Received, part.Length); Received += part.Length; return true;
        }
        internal string Finish(double now)
        {
            if (now > Expires || Received != bytes.Length) throw new ArgumentException("蓝图传输未完成或已过期。");
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        internal static bool Newer(uint current, uint previous) { return current != previous && unchecked((int)(current - previous)) > 0; }
        internal static bool ValidOrigin(int x, int y, int z) { return x >= -1000000 && x <= 1000000 && z >= -1000000 && z <= 1000000 && y >= 0 && y < 256; }
    }
}
