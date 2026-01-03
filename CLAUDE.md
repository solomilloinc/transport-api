# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Test, and Run Commands

### Building the Solution
```bash
dotnet build transport-api.sln
```

### Running Tests
```bash
# Run all tests
dotnet test Transport.Tests/Transport.Tests.csproj

# Run a specific test class
dotnet test Transport.Tests/Transport.Tests.csproj --filter "FullyQualifiedName~UserBusinessTests"

# Run a specific test method
dotnet test Transport.Tests/Transport.Tests.csproj --filter "FullyQualifiedName~UserBusinessTests.Login_ShouldFail_WhenInvalidCredentials"
```

### Running Locally
```bash
# From the transport.api directory
cd transport.api
func start
```

### Database Migrations
```bash
# Add a new migration
dotnet ef migrations add MigrationName --project transport.infraestructure --startup-project transport.api

# Update the database
dotnet ef database update --project transport.infraestructure --startup-project transport.api

# Remove last migration (if not applied)
dotnet ef migrations remove --project transport.infraestructure --startup-project transport.api
```

## Architecture Overview

### Clean Architecture with DDD

This is an **Azure Functions-based API** following Clean Architecture principles with Domain-Driven Design:

**Layer Dependencies:**
```
API (Azure Functions) → Business → Infrastructure → Domain
                                  ↘               ↗
                                    SharedKernel
```

**Key Projects:**
- **transport.domain**: Domain entities, enums, domain events, and error definitions. No dependencies on other layers.
- **transport.common (SharedKernel)**: Shared DTOs, Result<T> pattern, configuration options, and base classes.
- **transport.application (Business)**: Business logic in `*Business` classes, validation, interfaces for infrastructure services.
- **transport.infraestructure**: EF Core DbContext, authentication/authorization implementations, payment gateway, email sender, outbox dispatcher.
- **transport.api**: Azure Functions HTTP triggers, middleware (auth, exception handling), OpenAPI configuration.

### Core Patterns

**Result Pattern for Error Handling:**
- Business logic returns `Result<T>` or `Result<T, TError>` instead of throwing exceptions for business errors.
- Use `Error.Validation()`, `Error.NotFound()`, `Error.Conflict()` for domain errors.
- Middleware handles unexpected exceptions; business layer never throws for expected failures.

**Unit of Work Pattern:**
- Wrap multi-step operations in `IUnitOfWork.ExecuteInTransactionAsync()` for ACID compliance.
- Ensures all changes commit atomically or rollback on failure.

**Outbox Pattern for Reliable Messaging:**
- Domain entities raise events via `Raise(IDomainEvent)` inherited from `Entity` base class.
- Events stored in `OutboxMessage` table during `SaveChangesWithOutboxAsync()`.
- `OutboxDispatcher` asynchronously publishes unprocessed messages to Azure Service Bus.
- Guarantees eventual consistency between database and messaging.

**Optimistic Concurrency:**
- `Reserve` entity uses SQL Server `RowVersion` (timestamp) for optimistic locking.
- EF Core automatically handles concurrency conflicts via `DbUpdateConcurrencyException`.

**Slot Locking:**
- `ReserveSlotLock` prevents overbooking by temporarily locking seats during reservation flow.
- Locks expire after configured timeout (`ReserveOption.SlotLockTimeoutMinutes`).
- Supports both single and round-trip reservations.

## Domain Model

**Key Entities and Relationships:**

**Customer** (client who books transportation):
- Has many `Passenger` records
- Subscribes to `Service` via `ServiceCustomer`
- Has `CustomerAccountTransaction` for payment tracking
- Can create `ReserveSlotLock` to hold seats

**Reserve** (booking for a trip):
- Belongs to one `Service` (route)
- Has one `ServiceSchedule` (departure time)
- Assigned to `Vehicle` and optionally `Driver`
- Contains multiple `Passenger` records
- Has one `ReservePayment` for payment tracking
- Protected by `RowVersion` for concurrency

**Service** (route/line):
- Has origin and destination `City`
- Assigned to a `Vehicle`
- Has many `ServiceSchedule` entries (departure times)
- Has `ReservePrices` varying by date/customer type
- Customers subscribe via `ServiceCustomer`

**Passenger** (individual traveler):
- Belongs to one `Reserve`
- Optionally linked to registered `Customer`
- Has pickup/dropoff `Direction`
- Individual pricing and status

**Status Flow:**
- Reserve: Pending → Confirmed → InProgress → Completed (or Cancelled)
- Payment: Pending → Confirmed (or Failed)
- Passenger: Confirmed (or Cancelled)

## Authentication and Authorization

**JWT-based Authentication:**
- Login returns JWT token + refresh token via `ITokenProvider`.
- Refresh tokens stored in database with rotation support.
- Tokens include claims: NameIdentifier (userId), Email, Role.

**Authorization Middleware:**
- `AuthorizationMiddleware` validates JWT on all requests except swagger/webhooks/timers.
- Sets `IUserContext` (scoped service) with current user info.
- `[FunctionAuthorize(["Admin", "User"])]` attribute for role-based access.
- `[AllowAnonymous]` bypasses checks.

**Permission System:**
- `IPermissionService` for fine-grained authorization.
- `ClaimBuilder` dynamically constructs claims from roles/permissions.

## Payment Integration

**MercadoPago Gateway:**
- Interface: `IMercadoPagoPaymentGateway`
- Methods: `CreatePreferenceAsync()`, `CreatePaymentAsync()`, `GetPaymentAsync()`
- Validates payment amounts match expected totals including markup.
- Stores payment status in `ReservePayment` with outbox events for reliability.

**Configuration:**
- `MpIntegrationOption` holds API credentials and webhook URLs.

## Concurrency and Locking

**Preventing Double Bookings:**
1. Create `ReserveSlotLock` before attempting reservation.
2. Lock checks available capacity on `Service` + `ServiceSchedule`.
3. Lock expires after `ReserveOption.SlotLockTimeoutMinutes`.
4. Reservation commits within transaction, releasing lock.
5. `Reserve.RowVersion` prevents race conditions during final commit.

**Transaction Isolation:**
- Default: `IsolationLevel.ReadCommitted`
- Use higher isolation if needed (e.g., `Serializable` for critical sections).

## Testing Patterns

**Base Class: `TestBase`**
- Provides mock DbSet helpers:
  - `GetMockDbSetWithIdentity<T>()` - auto-incrementing IDs on Add
  - `GetQueryableMockDbSet<T>()` - queryable with async enumeration support
- `SetupSaveChangesWithOutboxAsync()` - mocks outbox persistence
- `GetRaisedEvent<TEntity, TEvent>()` - extracts domain events for assertions

**Test Structure:**
```csharp
public class YourBusinessTests : TestBase
{
    [Fact]
    public async Task MethodName_Should_ExpectedBehavior_When_Condition()
    {
        // Arrange
        var contextMock = new Mock<IApplicationDbContext>();
        contextMock.Setup(x => x.YourEntities)
            .Returns(GetQueryableMockDbSet(testData).Object);

        var business = new YourBusiness(contextMock.Object, ...);

        // Act
        var result = await business.YourMethod(...);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
```

**Test Dependencies:**
- xUnit, Moq, FluentAssertions
- EF Core InMemory (for integration tests if needed)

## Adding New Features

**Step-by-Step:**

1. **Define Domain Entity** in `transport.domain/YourFeature/YourEntity.cs`:
   - Inherit from `Entity` base class
   - Add domain logic and validation
   - Raise domain events via `Raise(new YourEvent(...))`

2. **Create Error Definitions** in `YourEntityError.cs`:
   ```csharp
   public static class YourEntityError
   {
       public static readonly Error NotFound = Error.NotFound("YourEntity.NotFound", "Entity not found");
       public static readonly Error Invalid = Error.Validation("YourEntity.Invalid", "Invalid data");
   }
   ```

3. **Add Business Interface** in `transport.domain/YourFeature/Abstraction/IYourBusiness.cs`

4. **Implement Business Logic** in `transport.application/YourBusiness/YourBusiness.cs`:
   - Inject `IApplicationDbContext` and `IUnitOfWork`
   - Return `Result<T>` from all methods
   - Wrap operations in transactions when needed

5. **Add FluentValidation Validators** in `transport.application/YourBusiness/Validation/`

6. **Configure EF Core** in `transport.infraestructure/Database/`:
   - Add `DbSet<YourEntity>` to `ApplicationDbContext`
   - Create `YourEntityConfiguration : IEntityTypeConfiguration<YourEntity>`
   - Apply configuration in `OnModelCreating`

7. **Create Migration**:
   ```bash
   dotnet ef migrations add AddYourEntity --project transport.infraestructure --startup-project transport.api
   ```

8. **Add Azure Function** in `transport.api/Functions/YourFunction.cs`:
   - Inherit from `FunctionBase`
   - Add `[OpenApiOperation]` and `[OpenApiResponseWithBody]` attributes
   - Inject `IYourBusiness` via constructor
   - Return results using `result.Match()` for proper HTTP responses

9. **Register Services** in `DependencyInjection.cs`:
   - Business layer: `services.AddScoped<IYourBusiness, YourBusiness>()`
   - Infrastructure layer: configure repositories/external services

10. **Write Tests** in `Transport.Tests/YourBusinessTests.cs`:
    - Extend `TestBase`
    - Mock `IApplicationDbContext` with test data
    - Assert using FluentAssertions

## Important Notes

**No Repository Pattern:**
- Direct use of `IApplicationDbContext.DbSet<T>()` instead of repository interfaces.
- EF Core DbContext already implements repository and unit of work patterns.

**Async/Await Throughout:**
- All database operations are async.
- All business methods return `Task<Result<T>>`.

**No Global Query Filters:**
- Soft deletes handled via status fields (e.g., `ReserveStatusEnum.Cancelled`).

**Auditing:**
- Entities implementing `IAuditable` automatically track `CreatedBy`, `CreatedDate`, `UpdatedBy`, `UpdatedDate`.
- `ApplicationDbContext.SaveChangesAsync()` populates these via `IUserContext`.

**Configuration Pattern:**
- Use Options pattern: `IOptions<YourOption>` bound from `appsettings.json` or environment variables.
- Register in `Program.cs` with `services.AddOptions<YourOption>()`.

**OpenAPI Documentation:**
- All HTTP functions must have `[OpenApiOperation]` and response attributes.
- Configured in `Program.cs` with API metadata.
