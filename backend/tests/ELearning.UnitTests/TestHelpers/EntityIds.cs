using ELearning.Domain.Common;

namespace ELearning.UnitTests.TestHelpers;

/// <summary>Ngoài EF, Id của entity chưa được sinh; test gán Id để mô phỏng entity đã lưu.</summary>
internal static class EntityIds
{
    public static T WithId<T>(this T entity, Guid? id = null)
        where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id ?? Guid.NewGuid());
        return entity;
    }
}
