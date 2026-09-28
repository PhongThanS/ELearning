using ELearning.Domain.Common;

namespace ELearning.Domain.Identity;

/// <summary>Nhóm người dùng, dùng để gán đề thi (D-10).</summary>
public sealed class UserGroup : Entity, IHasRowVersion
{
    private readonly List<UserGroupMember> _members = [];

    private UserGroup()
    {
    }

    public UserGroup(string code, string name, string? description, Guid createdBy, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code.Trim();
        Name = name.Trim();
        Description = description;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<UserGroupMember> Members => _members;
}

public sealed class UserGroupMember
{
    private UserGroupMember()
    {
    }

    public UserGroupMember(Guid groupId, Guid userId, DateTime addedAt)
    {
        GroupId = groupId;
        UserId = userId;
        AddedAt = addedAt;
    }

    public Guid GroupId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime AddedAt { get; private set; }
}
