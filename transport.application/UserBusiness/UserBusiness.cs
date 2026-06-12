using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Authentication;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Users;
using Transport.Domain.Users.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.User;
using Transport.SharedKernel.Helpers;

namespace Transport.Business.UserBusiness;

public class UserBusiness : IUserBusiness
{
    private readonly IJwtService jwtService;
    private readonly IApplicationDbContext dbContext;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenProvider tokenProvider;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITenantContext tenantContext;

    public UserBusiness(
        IJwtService jwtService,
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        this.jwtService = jwtService;
        this.dbContext = dbContext;
        this.passwordHasher = passwordHasher;
        this.tokenProvider = tokenProvider;
        this.unitOfWork = unitOfWork;
        this.tenantContext = tenantContext;
    }

    public async Task<Result<LoginResponseDto>> Login(LoginDto login)
    {
        await EnsureAuthRolesAsync();

        var tenantId = tenantContext.TenantId;
        var normalizedEmail = login.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .Where(u => u.Email == normalizedEmail && u.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (user is null || string.IsNullOrWhiteSpace(user.Password) || !passwordHasher.Verify(login.Password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        if (user.Status != EntityStatusEnum.Active)
        {
            return Result.Failure<LoginResponseDto>(UserError.Inactive);
        }

        return await IssueTokensAsync(user, login.IpAddress);
    }

    public async Task<Result<LoginResponseDto>> RegisterClientAsync(ClientRegisterRequestDto request)
    {
        var clientRole = await EnsureAuthRolesAsync();
        var tenantId = tenantContext.TenantId;
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedDocument = request.DocumentNumber.Trim();

        var existingUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalizedEmail);

        if (existingUser is not null)
        {
            return Result.Failure<LoginResponseDto>(UserError.EmailAlreadyExists);
        }

        var existingCustomerByDocument = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DocumentNumber == normalizedDocument && c.Status != EntityStatusEnum.Deleted);

        if (existingCustomerByDocument is not null)
        {
            return Result.Failure<LoginResponseDto>(UserError.DocumentAlreadyExists);
        }

        var customerMatches = await FindCustomersByEmailAsync(normalizedEmail);
        if (customerMatches.Count > 1)
        {
            return Result.Failure<LoginResponseDto>(UserError.CustomerEmailConflict);
        }

        if (customerMatches.Count == 1 && await IsCustomerLinkedToAnotherUserAsync(customerMatches[0].CustomerId))
        {
            return Result.Failure<LoginResponseDto>(UserError.CustomerAlreadyLinked);
        }

        User? createdUser = null;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var customer = customerMatches.SingleOrDefault();

            if (customer is null)
            {
                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = normalizedEmail,
                    DocumentNumber = normalizedDocument,
                    Phone1 = request.Phone1.Trim(),
                    Phone2 = NormalizeOptional(request.Phone2)
                };

                dbContext.Customers.Add(customer);
                await dbContext.SaveChangesWithOutboxAsync();
            }
            else
            {
                customer.FirstName = request.FirstName.Trim();
                customer.LastName = request.LastName.Trim();
                customer.DocumentNumber = normalizedDocument;
                customer.Phone1 = request.Phone1.Trim();
                customer.Phone2 = NormalizeOptional(request.Phone2);
                customer.Email = normalizedEmail;
                customer.Status = EntityStatusEnum.Active;

                dbContext.Customers.Update(customer);
                await dbContext.SaveChangesWithOutboxAsync();
            }

            createdUser = new User
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                Password = passwordHasher.Hash(request.Password),
                RoleId = clientRole.RoleId,
                CustomerId = customer.CustomerId,
                Status = EntityStatusEnum.Active
            };

            dbContext.Users.Add(createdUser);
            await dbContext.SaveChangesWithOutboxAsync();
        }, IsolationLevel.ReadCommitted);

        var user = await LoadUserGraphAsync(createdUser!.UserId);
        return await IssueTokensAsync(user!, request.IpAddress);
    }

    public async Task<Result<LoginResponseDto>> LoginWithGoogleAsync(GoogleAuthenticatedUserDto googleUser)
    {
        var clientRole = await EnsureAuthRolesAsync();

        if (!googleUser.EmailVerified)
        {
            return Result.Failure<LoginResponseDto>(UserError.GoogleEmailNotVerified);
        }

        var tenantId = tenantContext.TenantId;
        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        var existingUser = await dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalizedEmail);

        if (existingUser is not null)
        {
            if (GetAppRole(existingUser.RoleId, existingUser.Role?.Name) != RoleEnum.Client)
            {
                return Result.Failure<LoginResponseDto>(UserError.GoogleRestrictedRole);
            }

            if (existingUser.Status != EntityStatusEnum.Active)
            {
                return Result.Failure<LoginResponseDto>(UserError.Inactive);
            }

            if (!existingUser.CustomerId.HasValue)
            {
                return Result.Failure<LoginResponseDto>(UserError.ClientProfileMissing);
            }

            return await IssueTokensAsync(existingUser, googleUser.IpAddress);
        }

        var matchingCustomers = await FindCustomersByEmailAsync(normalizedEmail);
        if (matchingCustomers.Count > 1)
        {
            return Result.Failure<LoginResponseDto>(UserError.CustomerEmailConflict);
        }

        if (matchingCustomers.Count == 1 && await IsCustomerLinkedToAnotherUserAsync(matchingCustomers[0].CustomerId))
        {
            return Result.Failure<LoginResponseDto>(UserError.CustomerAlreadyLinked);
        }

        User? createdUser = null;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var customer = matchingCustomers.SingleOrDefault();

            if (customer is null)
            {
                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = SafeGoogleName(googleUser.FirstName, "Cliente"),
                    LastName = SafeGoogleName(googleUser.LastName, "Google"),
                    Email = normalizedEmail,
                    DocumentNumber = string.Empty,
                    Phone1 = string.Empty,
                    Phone2 = null,
                    Status = EntityStatusEnum.Active
                };

                dbContext.Customers.Add(customer);
                await dbContext.SaveChangesWithOutboxAsync();
            }

            createdUser = new User
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                Password = passwordHasher.Hash(Guid.NewGuid().ToString("N")),
                RoleId = clientRole.RoleId,
                CustomerId = customer.CustomerId,
                Status = EntityStatusEnum.Active
            };

            dbContext.Users.Add(createdUser);
            await dbContext.SaveChangesWithOutboxAsync();
        }, IsolationLevel.ReadCommitted);

        var user = await LoadUserGraphAsync(createdUser!.UserId);
        return await IssueTokensAsync(user!, googleUser.IpAddress);
    }

    public async Task<Result<CurrentUserProfileDto>> GetCurrentProfileAsync(int userId)
    {
        var user = await LoadUserGraphAsync(userId);
        if (user is null)
        {
            return Result.Failure<CurrentUserProfileDto>(UserError.ProfileNotAvailable);
        }

        return Result.Success(MapCurrentProfile(user));
    }

    public async Task<Result<CurrentUserProfileDto>> CompleteClientProfileAsync(int userId, ClientProfileCompleteRequestDto request)
    {
        var user = await dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == tenantContext.TenantId);

        if (user is null)
        {
            return Result.Failure<CurrentUserProfileDto>(UserError.NotFound);
        }

        if (user.Customer is null)
        {
            return Result.Failure<CurrentUserProfileDto>(UserError.ClientProfileMissing);
        }

        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var normalizedDocument = request.DocumentNumber.Trim();

        var emailConflict = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(c =>
                c.TenantId == tenantContext.TenantId &&
                c.CustomerId != user.Customer.CustomerId &&
                c.Email == normalizedEmail &&
                c.Status != EntityStatusEnum.Deleted);

        if (emailConflict)
        {
            return Result.Failure<CurrentUserProfileDto>(CustomerError.EmailAlreadyExists);
        }

        var documentConflict = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(c =>
                c.TenantId == tenantContext.TenantId &&
                c.CustomerId != user.Customer.CustomerId &&
                c.DocumentNumber == normalizedDocument &&
                c.Status != EntityStatusEnum.Deleted);

        if (documentConflict)
        {
            return Result.Failure<CurrentUserProfileDto>(UserError.DocumentAlreadyExists);
        }

        user.Customer.FirstName = request.FirstName.Trim();
        user.Customer.LastName = request.LastName.Trim();
        user.Customer.DocumentNumber = normalizedDocument;
        user.Customer.Phone1 = request.Phone1.Trim();
        user.Customer.Phone2 = NormalizeOptional(request.Phone2);
        user.Customer.Email = normalizedEmail;
        user.Customer.Status = EntityStatusEnum.Active;

        dbContext.Customers.Update(user.Customer);
        await dbContext.SaveChangesWithOutboxAsync();

        return Result.Success(MapCurrentProfile(user));
    }

    public async Task<Result<int>> CreateOperativeAsync(UserCreateRequestDto request)
    {
        await EnsureAuthRolesAsync();

        if (!TryParseRole(request.Role, out var role))
        {
            return Result.Failure<int>(UserError.InvalidRole);
        }

        if (role != RoleEnum.User)
        {
            return Result.Failure<int>(UserError.InvalidOperativeRole);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.TenantId == tenantContext.TenantId && u.Email == email);

        if (existingUser)
        {
            return Result.Failure<int>(UserError.EmailAlreadyExists);
        }

        var user = new User
        {
            TenantId = tenantContext.TenantId,
            Email = email,
            Password = passwordHasher.Hash(request.Password),
            RoleId = (int)role,
            Status = EntityStatusEnum.Active
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesWithOutboxAsync();

        return Result.Success(user.UserId);
    }

    public async Task<Result<bool>> UpdateOperativeAsync(int userId, UserUpdateRequestDto request)
    {
        await EnsureAuthRolesAsync();

        if (!TryParseRole(request.Role, out var role))
        {
            return Result.Failure<bool>(UserError.InvalidRole);
        }

        if (role != RoleEnum.User)
        {
            return Result.Failure<bool>(UserError.InvalidOperativeRole);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == tenantContext.TenantId);

        if (user is null)
        {
            return Result.Failure<bool>(UserError.NotFound);
        }

        if (user.RoleId != (int)RoleEnum.User)
        {
            return Result.Failure<bool>(UserError.InvalidOperativeRole);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var emailInUse = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.TenantId == tenantContext.TenantId && u.UserId != userId && u.Email == email);

        if (emailInUse)
        {
            return Result.Failure<bool>(UserError.EmailAlreadyExists);
        }

        user.Email = email;
        user.Status = request.Status;
        dbContext.Users.Update(user);
        await dbContext.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<UserReportItemDto>>> GetOperativeUsersReportAsync(PagedReportRequestDto<UserReportFilterRequestDto> request)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantContext.TenantId && u.RoleId == (int)RoleEnum.User);

        if (!string.IsNullOrWhiteSpace(request.Filters?.Search))
        {
            var term = request.Filters.Search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Filters?.Email))
        {
            query = query.Where(u => u.Email.Contains(request.Filters.Email));
        }

        if (request.Filters?.Status is not null)
        {
            query = query.Where(u => u.Status == request.Filters.Status);
        }

        var sortMappings = new Dictionary<string, Expression<Func<User, object>>>
        {
            ["email"] = u => u.Email,
            ["status"] = u => u.Status,
            ["createddate"] = u => u.UserId
        };

        var pagedResult = await query.ToPagedReportAsync<UserReportItemDto, User, UserReportFilterRequestDto>(
            request,
            selector: u => new UserReportItemDto(
                u.UserId,
                u.Email,
                GetAppRole(u.RoleId, null).ToString(),
                u.Status,
                u.CustomerId,
                DateTime.MinValue),
            sortMappings: sortMappings);

        return Result.Success(pagedResult);
    }

    public async Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string refreshToken, string ipAddress)
    {
        var storedRefreshToken = await tokenProvider.GetRefreshTokenByHashAsync(refreshToken);

        if (storedRefreshToken == null)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.RefreshNotFound);

        if (storedRefreshToken.RevokedAt != null)
        {
            await tokenProvider.RevokeAllUserTokensAsync(storedRefreshToken.UserId, ipAddress);
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.TokenReused);
        }

        if (storedRefreshToken.IsExpired)
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.TokenExpired);

        var user = await LoadUserGraphAsync(storedRefreshToken.UserId);
        if (user is null || user.Status != EntityStatusEnum.Active)
        {
            await tokenProvider.RevokeAllUserTokensAsync(storedRefreshToken.UserId, ipAddress);
            return Result.Failure<RefreshTokenResponseDto>(RefreshTokenError.RefreshNotFound);
        }

        var newAccessToken = jwtService.BuildToken(BuildClaims(user));

        var newRefreshToken = tokenProvider.GenerateRefreshToken();
        var hashedNewToken = TokenHasher.HashToken(newRefreshToken);

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await tokenProvider.SaveRefreshTokenAsync(newRefreshToken, user.UserId, ipAddress);
            await tokenProvider.RevokeRefreshTokenAsync(refreshToken, ipAddress, replacedByToken: hashedNewToken);
        });

        return new RefreshTokenResponseDto(newAccessToken, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken, string ipAddress)
    {
        await tokenProvider.RevokeRefreshTokenAsync(refreshToken, ipAddress);
    }

    public async Task RevokeAllSessionsAsync(int userId, string ipAddress)
    {
        await tokenProvider.RevokeAllUserTokensAsync(userId, ipAddress);
    }

    private async Task<LoginResponseDto> IssueTokensAsync(User user, string? ipAddress)
    {
        var accessToken = jwtService.BuildToken(BuildClaims(user));
        var refreshToken = tokenProvider.GenerateRefreshToken();

        await tokenProvider.SaveRefreshTokenAsync(refreshToken, user.UserId, ipAddress);

        return new LoginResponseDto(accessToken, refreshToken);
    }

    private ICollection<System.Security.Claims.Claim> BuildClaims(User user)
    {
        var customer = user.Customer;
        var appRole = GetAppRole(user.RoleId, user.Role?.Name);
        var name = customer is null
            ? user.Email
            : $"{customer.FirstName} {customer.LastName}".Trim();

        return ClaimBuilder.Create()
            .SetEmail(user.Email)
            .SetName(string.IsNullOrWhiteSpace(name) ? user.Email : name)
            .SetRole(appRole.ToString())
            .SetId(user.UserId.ToString())
            .SetTenantId(user.TenantId)
            .SetCustomerId(user.CustomerId)
            .SetNeedsProfileCompletion(appRole == RoleEnum.Client && customer is not null && NeedsProfileCompletion(customer))
            .Build();
    }

    private async Task<User?> LoadUserGraphAsync(int userId)
    {
        return await dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == tenantContext.TenantId);
    }

    private async Task<List<Customer>> FindCustomersByEmailAsync(string email)
    {
        return await dbContext.Customers
            .Where(c =>
                c.TenantId == tenantContext.TenantId &&
                c.Email == email &&
                c.Status != EntityStatusEnum.Deleted)
            .ToListAsync();
    }

    private async Task<bool> IsCustomerLinkedToAnotherUserAsync(int customerId)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.TenantId == tenantContext.TenantId && u.CustomerId == customerId);
    }

    private static bool NeedsProfileCompletion(Customer customer)
    {
        return string.IsNullOrWhiteSpace(customer.DocumentNumber)
            || string.IsNullOrWhiteSpace(customer.Phone1);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SafeGoogleName(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static bool TryParseRole(string role, out RoleEnum parsedRole)
    {
        return Enum.TryParse(role, true, out parsedRole);
    }

    private RoleEnum GetAppRole(int roleId, string? roleName)
    {
        if (roleId == (int)RoleEnum.Admin)
            return RoleEnum.Admin;

        if (roleId == (int)RoleEnum.User)
            return RoleEnum.User;

        if (roleId == (int)RoleEnum.SuperAdmin)
            return RoleEnum.SuperAdmin;

        if (string.Equals(roleName, "Cliente", StringComparison.OrdinalIgnoreCase))
            return RoleEnum.Client;

        return RoleEnum.User;
    }

    private async Task<Role> EnsureAuthRolesAsync()
    {
        var roleByIdTwo = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.RoleId == (int)RoleEnum.User);

        if (roleByIdTwo is not null && !string.Equals(roleByIdTwo.Name, "Operativo", StringComparison.OrdinalIgnoreCase))
        {
            roleByIdTwo.Name = "Operativo";
            dbContext.Entry(roleByIdTwo).State = EntityState.Modified;
            await dbContext.SaveChangesWithOutboxAsync();
        }

        var clientRole = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == "Cliente" && r.RoleId != (int)RoleEnum.User);

        if (clientRole is not null)
        {
            return clientRole;
        }

        clientRole = new Role
        {
            Name = "Cliente",
            CreatedBy = "System",
            CreatedDate = DateTime.UtcNow
        };

        dbContext.Entry(clientRole).State = EntityState.Added;
        await dbContext.SaveChangesWithOutboxAsync();

        return clientRole;
    }

    private CurrentUserProfileDto MapCurrentProfile(User user)
    {
        var appRole = GetAppRole(user.RoleId, user.Role?.Name);

        return new CurrentUserProfileDto(
            user.UserId,
            user.CustomerId,
            user.Email,
            appRole.ToString(),
            user.Status,
            appRole == RoleEnum.Client && user.Customer is not null && NeedsProfileCompletion(user.Customer),
            user.Customer?.FirstName,
            user.Customer?.LastName,
            user.Customer?.DocumentNumber,
            user.Customer?.Phone1,
            user.Customer?.Phone2);
    }
}
