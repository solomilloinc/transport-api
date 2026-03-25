# Interface Design for Testability (.NET / C#)

Good interfaces make testing natural:

1. **Accept dependencies, don't create them**

   Use constructor injection or method injection. Let the DI container (or the test) provide dependencies.

   ```csharp
   // Testable — dependency is injected
   public class OrderProcessor
   {
       private readonly IPaymentGateway _paymentGateway;

       public OrderProcessor(IPaymentGateway paymentGateway)
       {
           _paymentGateway = paymentGateway;
       }

       public OrderResult Process(Order order)
       {
           return _paymentGateway.Charge(order.Total);
       }
   }

   // Hard to test — creates its own dependency
   public class OrderProcessor
   {
       public OrderResult Process(Order order)
       {
           var gateway = new StripeGateway();
           return gateway.Charge(order.Total);
       }
   }
   ```

2. **Return results, don't produce side effects**

   ```csharp
   // Testable — returns a value you can assert on
   public Discount CalculateDiscount(Cart cart)
   {
       // pure logic
       return new Discount(cart.Total * 0.1m);
   }

   // Hard to test — mutates the input, returns nothing
   public void ApplyDiscount(Cart cart)
   {
       cart.Total -= cart.Total * 0.1m;
   }
   ```

3. **Program to interfaces, not concrete classes**

   ```csharp
   // Testable — easy to substitute in tests
   public class NotificationService
   {
       private readonly IEmailSender _emailSender;

       public NotificationService(IEmailSender emailSender)
       {
           _emailSender = emailSender;
       }
   }

   // Hard to test — tightly coupled to SmtpClient
   public class NotificationService
   {
       private readonly SmtpClient _smtpClient = new();
   }
   ```

4. **Small surface area**
   - Fewer methods = fewer tests needed
   - Fewer parameters = simpler test setup
   - Prefer focused interfaces (ISP) over fat ones
