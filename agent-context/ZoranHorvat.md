# ZoranHorvat.md

Applies to all .NET/C#, Blazor, Telerik, ServiceStack, `.cs`, `.csproj`, `.razor`, and `.cshtml` work.

All code generation, refactoring, review, or suggestions **MUST** strictly follow the rules below.
These guidelines are derived from Zoran Horvat's teachings on clean, object-oriented, and functional
C# (Pluralsight, Udemy, YouTube). They emphasize eliminating primitive obsession, favoring
immutability, reducing complexity through polymorphism and monads, and maintaining long-term
maintainability in enterprise systems.

Violations are only permitted with explicit user approval and a documented rationale in the response.
Document any durable exception in DECISIONS.md.

---

## Core Rules (Priority Reference)

| Priority | Category | Rule | Rationale |
| :--- | :--- | :--- | :--- |
| 1 | Naming & Typing | Use intention-revealing names; **eliminate primitive obsession**. | Primitives hide domain meaning and scatter validation. Replace with rich value objects (records). |
| 1 | Naming & Typing | Prefer immutable records with `required` properties and init-only setters. | Compile-time immutability and type safety. |
| 2 | Immutability & State | Default to immutable data structures; treat mutation as exceptional. | Use records, `with` expressions, read-only collections. Supports thread-safety and easier reasoning. |
| 2 | Immutability & State | Embrace emergent design over upfront over-engineering. | Let clean, small units evolve naturally through refactoring. |
| 3 | Control Flow | Eliminate branching via polymorphism (prefer objects over if/else/switch). | Replace conditionals with Strategy/State patterns or polymorphic classes. |
| 3 | Control Flow | Apply the **Rule of Three**: refactor duplicated logic only after it appears three times. | Prevents copy-paste debt; forces extraction to methods, classes, or monads. |
| 3 | Control Flow | **Level-zero nesting**: flatten code with guard clauses (early returns). | No deep nesting. `if (!condition) return;` or throw early. |
| 3 | Control Flow | Maintain **Single Level of Abstraction Principle (SLAP)** in every method. | Each method operates at one abstraction level; delegate details downward. |
| 4 | Functional Patterns | Use **monads** (Option, Result, Validation) to simplify error handling, validation, and optionals. | Replaces nulls, exceptions-as-flow-control, and boolean flags. Makes code declarative and composable. |
| 4 | Functional Patterns | Leverage modern C# functional features (switch expressions, pattern matching, LINQ, `with` expressions). | Concise, expressive code that reads like business rules. |
| 5 | Architectural | Apply DDD principles where domain complexity warrants it (bounded contexts, aggregates, value objects, domain events). | Keeps domain logic pure and isolated from infrastructure/UI. |
| 5 | Architectural | Keep methods small, focused, and testable; favor composition over inheritance. | Supports emergent design and easy unit testing. |

---

## Enforcement Flags

When generating, reviewing, or refactoring .NET/C# code, immediately flag:

- **Primitive obsession** — raw `string`, `int`, `Guid` where a domain value object should exist.
- **Mutable DTO/domain state** — public setters on domain objects unless explicitly justified.
- **Nested control flow** — `if` inside `if`, nested loops, deeply chained ternaries.
- **Exception-as-normal-flow** — throwing exceptions for recoverable validation or expected HTTP responses.
- **Missing guard clauses** — validation logic buried inside method bodies instead of at the top.
- **Mixed abstraction levels** — a method that both orchestrates and does low-level work in the same body.

For new features: start with domain models (value objects + monads), then services, then UI components.
When in doubt: favor clarity, immutability, and composability over cleverness.

---

## Expanded Rules with Examples

### 1. Eliminate Primitive Obsession

**Bad:**
```csharp
public class Order
{
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}
```

**Good:**
```csharp
public record CustomerId(string Value)
{
    public static CustomerId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(value);
    }
}

public record Money(decimal Amount, Currency Currency);
public record Currency(string Code);

public record Order
{
    public required CustomerId Customer { get; init; }
    public required Money Total { get; init; }
}
```

Value objects centralize validation, prevent invalid states, and make method signatures self-documenting.

---

### 2. Immutability & Emergent Design

Use `record` + `with` for state transitions. Mutation only in controlled, named methods.

```csharp
public record Transaction(Money Amount, TransactionType Type, DateTimeOffset Timestamp);

public record Account
{
    public required OwnerId Owner { get; init; }
    public Money Balance { get; init; } = Money.Zero;
    public IReadOnlyList<Transaction> History { get; init; } = [];

    public Account Credit(Money amount) =>
        this with
        {
            Balance = Balance.Add(amount),
            History = [..History, new Transaction(amount, TransactionType.Credit, DateTimeOffset.UtcNow)]
        };
}
```

---

### 3. Control Flow

**Guard clauses at level zero:**
```csharp
public Result<Order, OrderError> PlaceOrder(CustomerId customerId, ProductId productId, int quantity)
{
    ArgumentNullException.ThrowIfNull(customerId);
    ArgumentNullException.ThrowIfNull(productId);
    if (quantity <= 0) return Result<Order, OrderError>.Failure(OrderError.InvalidQuantity);

    // main logic here — no nesting
}
```

**Polymorphism over branching:**
```csharp
// Instead of: if (paymentType == "card") { ... } else if (paymentType == "wallet") { ... }
public interface IPaymentMethod
{
    Task<Result<PaymentConfirmation, PaymentError>> ProcessAsync(Money amount, CancellationToken ct);
}

public sealed class CardPayment(CardDetails card) : IPaymentMethod { ... }
public sealed class WalletPayment(WalletId walletId) : IPaymentMethod { ... }
```

---

### 4. Monads — Reference Implementations

#### Option\<T\> — for values that may or may not exist

```csharp
public readonly record struct Option<T>
{
    private readonly T? _value;
    public bool HasValue { get; }

    private Option(T value) { _value = value; HasValue = true; }

    public static Option<T> Some(T value) => new(value);
    public static readonly Option<T> None = default;

    /// <summary>Transforms the contained value if present; returns None if absent.</summary>
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
        => HasValue ? Option<TResult>.Some(mapper(_value!)) : Option<TResult>.None;

    /// <summary>Chains an Option-returning operation; returns None if this is None.</summary>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
        => HasValue ? binder(_value!) : Option<TResult>.None;

    /// <summary>Pattern-match without branching at the call site.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
        => HasValue ? onSome(_value!) : onNone();
}
```

**Usage — no null checks, no branching:**
```csharp
public Option<Customer> FindCustomer(CustomerId id) =>
    _repository.Get(id) is { } customer
        ? Option<Customer>.Some(customer)
        : Option<Customer>.None;

var displayName = FindCustomer(id)
    .Map(c => c.DisplayName)
    .Match(onSome: name => name, onNone: () => "Guest");
```

---

#### Result\<TValue, TError\> — for operations that can fail with typed errors

```csharp
public readonly record struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    public bool IsSuccess { get; }

    private Result(TValue value) { _value = value; IsSuccess = true; }
    private Result(TError error) { _error = error; IsSuccess = false; }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);

    /// <summary>Transforms the success value; passes failure through unchanged.</summary>
    public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> mapper)
        => IsSuccess
            ? Result<TResult, TError>.Success(mapper(_value!))
            : Result<TResult, TError>.Failure(_error!);

    /// <summary>Chains a Result-returning operation; short-circuits on failure.</summary>
    public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> binder)
        => IsSuccess ? binder(_value!) : Result<TResult, TError>.Failure(_error!);

    /// <summary>Async version of Bind for chaining async Result-returning operations.</summary>
    public Task<Result<TResult, TError>> BindAsync<TResult>(
        Func<TValue, Task<Result<TResult, TError>>> binder)
        => IsSuccess ? binder(_value!) : Task.FromResult(Result<TResult, TError>.Failure(_error!));

    /// <summary>Pattern-match the result without branching at the call site.</summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public Task<TResult> MatchAsync<TResult>(
        Func<TValue, Task<TResult>> onSuccess,
        Func<TError, Task<TResult>> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}
```

**Map/Bind pipeline — validation → business rule → state update (no try/catch, no unwrapping):**
```csharp
public Result<Order, OrderError> PlaceOrder(PlaceOrderRequest request) =>
    ValidateRequest(request)
        .Bind(ValidateInventory)
        .Bind(ApplyPricing)
        .Map(CreateOrderRecord);
```

**Async chaining:**
```csharp
var result = await ValidateCustomer(customerId)
    .BindAsync(c => FetchProductAsync(c, productId, ct))
    .BindAsync(p => CreateOrderAsync(p, ct));
```

**Terminal dispatch (Minimal API):**
```csharp
app.MapPost("/orders", async (PlaceOrderRequest req, IOrderService orders, CancellationToken ct) =>
    (await orders.PlaceOrderAsync(req, ct))
        .Match(
            onSuccess: order => Results.Created($"/orders/{order.Id}", order),
            onFailure: error => error switch
            {
                OrderError.NotFound    => Results.NotFound(),
                OrderError.Validation  => Results.ValidationProblem(error.Fields),
                _                      => Results.Problem(error.Message)
            }));
```

---

### 5. DDD — Apply Where Domain Complexity Warrants It

- **Value Objects:** `Money`, `Currency`, `CustomerId`, `OrderId` — domain-specific identifiers and quantities.
- **Aggregates:** Root entity that owns its invariants and child entities; never expose child entities directly.
- **Domain Events:** Raise events (`OrderPlaced`, `PaymentReceived`) rather than calling cross-domain services directly.
- **Separation:** Domain layer has zero dependencies on EF Core, HTTP, or any infrastructure. Infrastructure implements domain interfaces.

---

## Blazor / Telerik / Tailwind Extensions

### Blazor Component Guidelines

- All components functional-first: minimal state, prefer `[Parameter]` + `EventCallback` over internal mutable state.
- Use immutable records for component models/state snapshots.
- Single Responsibility — one component = one clear UI concern.
- Prefer composition: small, reusable components over monolithic ones.
- Component parameters: prefer records for complex props.
- State management: use Fluxor or a simple immutable state record passed down; avoid excessive cascading parameters.
- Performance: `Virtualize` for large lists.
- Prefer Telerik components where licensed; use MudBlazor as fallback.

### Styling (Telerik UI + Tailwind CSS)

- **Primary approach:** Tailwind CSS utility classes for layout, spacing, colors, and responsive design.
- **Do not fight Telerik themes.** Wrap Telerik components in a `<div>` with Tailwind classes for container control.
- For deeper customization: Telerik ThemeBuilder → custom theme → layer Tailwind utilities on top.
- Consistent spacing: Tailwind scale (`p-4`, `space-y-6`, `gap-8`).
- Responsive: always mobile-first breakpoints (`sm:`, `md:`, `lg:`).
- Dark mode: `dark:` prefix throughout.
- Accessibility: proper contrast, `focus-visible:ring-2`.

**Example (generic card component):**
```razor
@* ItemCard.razor *@
<div class="bg-white dark:bg-gray-800 rounded-2xl shadow-lg p-6 border border-gray-100 dark:border-gray-700">
    <TelerikCard>
        <CardHeader>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">@Title</h3>
        </CardHeader>
        <CardBody>@ChildContent</CardBody>
    </TelerikCard>
</div>

@code {
    [Parameter] public required string Title { get; set; }
    [Parameter] public required RenderFragment ChildContent { get; set; }
}
```

---

## Enforcement in Agentic Tooling

- Always reference the relevant rule number when flagging a violation.
- Flag violations immediately and provide a corrected version.
- For new features: domain models first (value objects + monads) → services → UI.
- Comment code for automated documentation ingestion (XML doc comments on all public members).
- When in doubt: favor clarity, immutability, and composability over cleverness.
