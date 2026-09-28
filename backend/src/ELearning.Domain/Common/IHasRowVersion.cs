namespace ELearning.Domain.Common;

/// <summary>Entity có optimistic concurrency bằng cột ROWVERSION.</summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; }
}
