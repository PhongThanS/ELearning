using ELearning.Domain.Common;

namespace ELearning.Domain.Questions;

public sealed class QuestionCategory : Entity, IHasRowVersion
{
    private QuestionCategory()
    {
    }

    public QuestionCategory(string code, string name, Guid createdBy, DateTime createdAt, Guid? parentId = null)
    {
        ParentId = parentId;
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code.Trim();
        Name = name.Trim();
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Chuyên đề cha (chỉ 2 cấp): null = chuyên đề gốc, có thể có hoặc không có chuyên đề con.
    /// Chuyên đề con không có con; kiểm tra ở CategoryService.
    /// </summary>
    public Guid? ParentId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, bool isActive, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        IsActive = isActive;
        UpdatedAt = now;
    }

    public void MoveTo(Guid? parentId, DateTime now)
    {
        if (parentId == Id)
        {
            throw new InvalidOperationException("A category cannot be its own parent.");
        }

        ParentId = parentId;
        UpdatedAt = now;
    }
}
