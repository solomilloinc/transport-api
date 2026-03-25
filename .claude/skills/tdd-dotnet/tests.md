# Good and Bad Tests (.NET / C# / xUnit)

## Good Tests

**Integration-style**: Test through real interfaces, not mocks of internal parts.

```csharp
// GOOD: Tests observable behavior
public class CheckoutTests
{
    [Fact]
    public async Task User_can_checkout_with_valid_cart()
    {
        // Arrange
        var cart = new Cart();
        cart.Add(Products.Widget);
        var paymentMethod = new CreditCard("4111111111111111");

        // Act
        var result = await _checkoutService.Checkout(cart, paymentMethod);

        // Assert
        Assert.Equal(OrderStatus.Confirmed, result.Status);
    }
}
```

With **FluentAssertions**:

```csharp
[Fact]
public async Task User_can_checkout_with_valid_cart()
{
    var cart = new Cart();
    cart.Add(Products.Widget);

    var result = await _checkoutService.Checkout(cart, new CreditCard("4111111111111111"));

    result.Status.Should().Be(OrderStatus.Confirmed);
}
```

Characteristics:

- Tests behavior users/callers care about
- Uses public API only
- Survives internal refactors
- Describes WHAT, not HOW
- One logical assertion per test
- Method name reads like a specification

## Bad Tests

**Implementation-detail tests**: Coupled to internal structure.

```csharp
// BAD: Tests implementation details
[Fact]
public async Task Checkout_calls_payment_service_process()
{
    var mockPayment = new Mock<IPaymentService>();
    var service = new CheckoutService(mockPayment.Object);

    await service.Checkout(cart, payment);

    mockPayment.Verify(p => p.Process(cart.Total), Times.Once);
}
```

Red flags:

- Mocking internal collaborators
- Testing private methods (via reflection or `[InternalsVisibleTo]`)
- Asserting on call counts/order with `Times.Once`, `Times.Exactly`
- Test breaks when refactoring without behavior change
- Test name describes HOW not WHAT
- Verifying through external means instead of interface

```csharp
// BAD: Bypasses interface to verify
[Fact]
public async Task CreateUser_saves_to_database()
{
    await _userService.CreateUser(new CreateUserRequest("Alice"));

    using var conn = new SqlConnection(_connectionString);
    var row = await conn.QuerySingleAsync("SELECT * FROM Users WHERE Name = @Name", new { Name = "Alice" });
    Assert.NotNull(row);
}

// GOOD: Verifies through interface
[Fact]
public async Task CreateUser_makes_user_retrievable()
{
    var user = await _userService.CreateUser(new CreateUserRequest("Alice"));

    var retrieved = await _userService.GetUser(user.Id);
    Assert.Equal("Alice", retrieved.Name);
}
```

## Test Organization

Use descriptive class and method names that read like specifications:

```csharp
public class When_user_checks_out
{
    [Fact]
    public void With_valid_cart_order_is_confirmed() { }

    [Fact]
    public void With_empty_cart_throws_validation_error() { }

    [Fact]
    public void With_expired_card_returns_payment_failed() { }
}
```
