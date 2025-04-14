namespace Transport.Business.Authorization;

public interface IPermissionService
{
    Task<HashSet<string>> GetPermissionsForUserAsync(Guid userId);
}
