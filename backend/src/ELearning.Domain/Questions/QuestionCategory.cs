using ELearning.Domain.Common;

namespace ELearning.Domain.Questions;

public sealed class QuestionCategory : Entity, IHasRowVersion
{
    private QuestionCategory()
    {
    }

    public QuestionCategory(string code, string name, Guid createdBy, DateTime createdAt)
    {
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
}
