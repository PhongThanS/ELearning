namespace ELearning.Application.Common.Abstractions;

public interface IRegistrationSettingService
{
    Task<bool> IsSelfRegistrationAllowedAsync(CancellationToken ct = default);

    Task<bool> SetSelfRegistrationAllowedAsync(bool allow, Guid? adminId, CancellationToken ct = default);
}
