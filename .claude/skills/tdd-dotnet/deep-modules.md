# Deep Modules (.NET / C#)

From "A Philosophy of Software Design":

**Deep module** = small interface + lots of implementation

```
┌─────────────────────┐
│   Small Interface   │  ← Few methods, simple params
├─────────────────────┤
│                     │
│                     │
│  Deep Implementation│  ← Complex logic hidden
│                     │
│                     │
└─────────────────────┘
```

**Shallow module** = large interface + little implementation (avoid)

```
┌─────────────────────────────────┐
│       Large Interface           │  ← Many methods, complex params
├─────────────────────────────────┤
│  Thin Implementation            │  ← Just passes through
└─────────────────────────────────┘
```

## .NET Example

```csharp
// DEEP: Simple interface, complex logic hidden inside
public interface IOrderPricer
{
    Money CalculateTotal(Order order);
}

public class OrderPricer : IOrderPricer
{
    // Inside: tax rules, discount tiers, shipping calculations,
    // currency conversions, promotional logic...
    public Money CalculateTotal(Order order) { /* deep logic */ }
}

// SHALLOW: Big interface, each method is trivial
public interface IOrderPricer
{
    Money CalculateSubtotal(Order order);
    Money CalculateTax(Order order, TaxRegion region);
    Money CalculateShipping(Order order, ShippingMethod method);
    Money ApplyDiscount(Order order, DiscountCode code);
    Money ApplyCoupon(Order order, Coupon coupon);
    Money ConvertCurrency(Money amount, Currency target);
    Money CalculateTotal(Order order, TaxRegion region, ShippingMethod method,
        DiscountCode? code, Coupon? coupon, Currency? currency);
}
```

When designing interfaces, ask:

- Can I reduce the number of methods?
- Can I simplify the parameters?
- Can I hide more complexity inside?
