using transport.application.Authorization;

namespace transport.infraestructure.Authorization;
internal sealed class PermissionProvider: IPermissionService
{
    public Task<HashSet<string>> GetPermissionsForUserAsync(Guid userId)
    {
        // TODO: Here you'll implement your logic to fetch permissions.
        //HashSet<string> permissionsSet = [];

        //return Task.FromResult(permissionsSet);

        return Task.FromResult(new HashSet<string> { "reserves.read", "users.view" });
    }
}
