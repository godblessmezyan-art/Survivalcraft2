using System;
namespace SCIEPort.Features.Home
{
    /// <summary>Bounded single-cell commit with a captured previous signature and conditional undo.</summary>
    internal static class HomeCellTransaction
    {
        internal enum Result { Applied, Conflict, Rejected, RolledBack, RollbackFailed }
        internal static Result Apply(Func<int> read, Action<int> write, int before, int after, out Exception error)
        {
            error = null;
            try
            {
                if (read() != before) return Result.Conflict;
                write(after);
                int actual = read();
                return actual == after ? Result.Applied : actual == before ? Result.Rejected : Result.Conflict;
            }
            catch (Exception failure)
            {
                error = failure;
                try
                {
                    int actual = read();
                    if (actual == before) return Result.RolledBack;
                    if (actual != after) return Result.Conflict; // Foreign replacement is never overwritten.
                    write(before);
                    return read() == before ? Result.RolledBack : Result.RollbackFailed;
                }
                catch (Exception undo) { error = new AggregateException(failure, undo); return Result.RollbackFailed; }
            }
        }
    }
}
