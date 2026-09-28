namespace ELearning.Domain.Common;

/// <summary>Entity có khóa GUID; Id được EF Core sinh tuần tự phía client (docs/10-bay-ky-thuat.md mục 10).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
