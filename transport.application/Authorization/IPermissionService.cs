using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace transport.application.Authorization;

public interface IPermissionService
{
    Task<HashSet<string>> GetPermissionsForUserAsync(Guid userId);
}
