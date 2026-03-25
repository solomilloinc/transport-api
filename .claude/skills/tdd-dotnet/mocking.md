# When to Mock (.NET / C#)

Mock at **system boundaries** only:

- External APIs (payment, email, etc.)
- Databases (sometimes — prefer test DB or in-memory provider)
- Time (`TimeProvider`, `ISystemClock`)
- File system (sometimes)
- `HttpClient` calls

Don't mock:

- Your own classes/modules
- Internal collaborators
- Anything you control

## Designing for Mockability

At system boundaries, design interfaces that are easy to mock:

**1. Use dependency injection**

Pass external dependencies via constructor injection. Register them in DI and let tests substitute fakes:

```csharp
// Easy to mock — interface injected
public class PaymentProcessor
{
    private readonly IPaymentClient _paymentClient;

    public PaymentProcessor(IPaymentClient paymentClient)
    {
        _paymentClient = paymentClient;
    }

    public async Task<PaymentResult> Process(Order order)
    {
        return await _paymentClient.Charge(order.Total);
    }
}

// In test
[Fact]
public async Task Process_charges_order_total()
{
    var fakeClient = Substitute.For<IPaymentClient>();
    fakeClient.Charge(100m).Returns(PaymentResult.Success);

    var processor = new PaymentProcessor(fakeClient);
    var result = await processor.Process(new Order { Total = 100m });

    result.Should().Be(PaymentResult.Success);
}
```

```csharp
// Hard to mock — creates dependency internally
public class PaymentProcessor
{
    public async Task<PaymentResult> Process(Order order)
    {
        var client = new StripeClient(Environment.GetEnvironmentVariable("STRIPE_KEY"));
        return await client.Charge(order.Total);
    }
}
```

**2. Prefer specific interfaces over generic ones**

Create focused interfaces for each external operation instead of one generic catch-all:

```csharp
// GOOD: Each method is independently mockable
public interface IUserApi
{
    Task<User> GetUser(int id);
    Task<IReadOnlyList<Order>> GetOrders(int userId);
    Task<Order> CreateOrder(CreateOrderRequest request);
}

// BAD: Mocking requires conditional logic inside the mock
public interface IApiClient
{
    Task<T> Send<T>(string endpoint, HttpMethod method, object? body = null);
}
```

The specific interface approach means:

- Each mock returns one specific shape
- No conditional logic in test setup
- Easier to see which operations a test exercises
- Full type safety per operation

**3. Use `TimeProvider` for time-dependent code**

```csharp
// Testable — time is injectable (built-in since .NET 8)
public class TrialService
{
    private readonly TimeProvider _timeProvider;

    public TrialService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool IsTrialExpired(DateTimeOffset trialStart)
    {
        return _timeProvider.GetUtcNow() > trialStart.AddDays(30);
    }
}

// In test
[Fact]
public void Trial_expires_after_30_days()
{
    var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var service = new TrialService(fakeTime);

    fakeTime.SetUtcNow(new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero));

    service.IsTrialExpired(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero))
        .Should().BeTrue();
}
```

**4. Use `IHttpClientFactory` for HTTP calls**

```csharp
// Testable — HttpClient is provided by factory, can be mocked with MockHttpMessageHandler
public class WeatherService
{
    private readonly HttpClient _httpClient;

    public WeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("Weather");
    }

    public async Task<WeatherForecast> GetForecast(string city)
    {
        var response = await _httpClient.GetFromJsonAsync<WeatherForecast>($"/forecast/{city}");
        return response!;
    }
}
```
