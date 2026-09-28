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

    public void Update(string name, string? description, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
    }

    /// <summary>Thêm các thành viên chưa có; trả về số người được thêm.</summary>
    public int AddMembers(IEnumerable<Guid> userIds, DateTime now)
    {
        var added = 0;
        foreach (var userId in userIds.Distinct().Where(id => _members.TrueForAll(m => m.UserId != id)))
        {
            _members.Add(new UserGroupMember(Id, userId, now));
            added++;
        }

        return added;
    }

    public bool RemoveMember(Guid userId) => _members.RemoveAll(m => m.UserId == userId) > 0;
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
