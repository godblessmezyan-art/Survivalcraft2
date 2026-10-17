namespace BuilderAssistant.Core.BuildEngine
{
    /// <summary>Lifecycle of one staged building operation.</summary>
    internal enum BuildTaskState
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Undone
    }
}
