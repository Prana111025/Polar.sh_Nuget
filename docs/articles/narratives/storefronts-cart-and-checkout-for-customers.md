# Cart and checkout, in plain language

Suppose a customer walks into a digital storefront powered by PolarSharp. They see a product they like. They click "Add to cart". They click around some more, add another item, maybe punch in a discount code. Eventually they decide they're ready to buy, hit "Check out", type in their address, click "Pay", and a few seconds later see a confirmation page.

This narrative walks through what's happening behind that flow, in plain language. No prior knowledge of the SDK assumed. If you're more interested in the developer-facing details, see the [companion article](../storefronts-cart-checkout.md).

## What a cart is

A shopping cart is exactly what it sounds like — a list of things the customer wants to buy, with quantities, before they've actually paid. In PolarSharp, every cart lives **on the server**, not in the customer's browser. The browser sees the cart, displays it, and tells the server when the customer wants to change it — but the server is the source of truth.

This matters because the browser is not a trustworthy place to keep prices. Think of a fancy restaurant where the waiter writes your order on a notepad and brings it back to the kitchen. The kitchen has the menu. The waiter doesn't price the food themselves — that would be a great way for diners to "accidentally" pay $2 for a $200 wagyu steak. The kitchen prices the food, the kitchen writes the bill, and the waiter just carries the message. PolarSharp's cart works the same way: the *server* always re-checks the price against the catalog every time the cart changes, and the *browser* is just the waiter.

## How the server protects against tampering

Every time the customer adds a product to their cart, the cart service runs a few checks before agreeing:

- **Is this product real?** It looks the product up in the catalog. If the browser made up a product id that doesn't exist, the cart politely refuses.
- **Is this product currently available?** If the merchant has marked the product as out of stock, the cart refuses with a "this is unavailable right now" message.
- **What is the unit price, really?** The cart ignores whatever the browser said about the price and reads the price out of the catalog. Even if a sneaky customer used a browser developer tool to type their own price into the form, the cart never sees that value as anything other than untrusted noise.
- **Is the quantity sensible?** Negative, zero, or absurdly large quantities are refused.
- **Does the new total break our limits?** A merchant can configure a hard ceiling — "no single cart over $10,000," say — and the cart enforces it.

A discount code is a special case. The cart **stores** the code the customer typed but doesn't pretend to know whether it's valid. Validating a code (does it exist? is it still active? does it apply to these particular products?) is the job of the checkout pipeline — covered below. This is the same principle as before: the cart is just the waiter; deciding what counts as a real discount is the kitchen's job.

> **Why all this paranoia?** In an ecommerce world where a customer's browser can be modified by anyone with enough patience, "trust the browser" means "let people pay whatever they say they should pay." Restaurants discovered this principle hundreds of years ago. Online stores re-discover it the hard way roughly once every six months.

## Who owns the cart

A cart belongs to one of two kinds of shopper:

- **A signed-in customer** — someone with an account, an email on file, a saved address book. Their cart follows them across browsers and devices because the server keeps it under their customer id.
- **A guest** — someone browsing anonymously, no account, just exploring. Their cart is tied to a small encrypted **cookie** (a tiny piece of data the browser sends back to the server on every visit) that identifies their browser session for the next 30 days.

Either way, the cart lives on the server. The guest cookie is just a name tag — when the browser shows up, the server uses the cookie to find the right cart. The cookie itself doesn't contain any of the cart's contents; just the session id.

If a guest decides to sign up partway through their shopping, the cart they built as a guest is transferred onto their new account. The waiter (browser) hands the kitchen (server) both name tags at once and says "these are the same person now — please merge their two notepads."

## What's in a cart

A cart is a small bundle of information:

- A unique id (so the server can find it again)
- The owner (either a customer id or a guest session id)
- The tenant (if this is a multi-tenant store; otherwise blank)
- A list of **line items**, where each line is one specific product/variant + quantity + the unit price the server confirmed at add-to-cart time
- An optional **shipping address** (added once the customer decides where they want it shipped)
- An optional **discount code** (recorded but not yet validated)
- The **totals** — subtotal, discount, tax, shipping, grand total — all in cents, all recomputed by the server every time anything changes

If the customer changes their cart in any way, the server throws out the previous totals and recomputes everything from scratch. No partial updates, no "subtract the old line and add the new line and hope the math still works" — just always recompute. It's slightly more work per change but it leaves zero room for the running totals to drift away from reality.

## Storing the cart

The cart needs a place to live between visits. PolarSharp's storefront-core packages ship a simple **in-memory store** by default — meaning the carts live in the running web server's memory and are gone if the server restarts.

For a development environment, that's fine. For production, the merchant typically swaps in something more durable — a database, or a Redis cache, or whatever else fits their stack. They do this by writing one extra class (an `IStorefrontCartStore` implementation) and registering it during startup. Nothing else in PolarSharp's storefront code has to change; the cart service was designed to be agnostic about where the data lives.

Think of it like a coat-check service. The coat-check policy (number-it, give the customer a stub, return the coat when the stub comes back) is the same regardless of whether the coats are hung on hooks, stored on rolling racks, or stuffed into bins. PolarSharp specifies the policy and ships hooks-on-the-wall as the default; merchants who need rolling racks plug their own in.

## What checkout is

Checkout is the moment the cart becomes an order. The customer says "I'm ready to buy this" and the server takes them through a series of confirmation + processing steps:

1. **Take a snapshot of the cart.** A new "checkout session" is created from the cart's current contents. From this moment on, the cart can change but the checkout session is frozen.
2. **Re-validate every line.** The catalog could have changed since the customer added things. The server re-checks every price, every availability flag, every variant. If something has drifted (e.g. a product went out of stock between add-to-cart and check-out), the customer is told and asked to revise.
3. **Reserve inventory.** For products that track stock, the server tentatively holds the requested quantities so nobody else can grab them mid-checkout.
4. **Apply any discount codes.** Now the discount code that the cart was carrying gets validated against the merchant's discount table. If it's valid for this set of products, the discount is applied. If it's expired or doesn't apply, the customer is told.
5. **Quote tax.** The shipping address determines the tax region, and the tax provider (TaxJar, Vertex, whichever the merchant uses) returns the tax to add.
6. **Quote shipping.** The shipping provider (Shippo, EasyPost) calculates the shipping cost based on the address and the items.
7. **Take payment.** The customer's payment information (credit card, wallet credit, etc.) is charged for the grand total.
8. **Fulfill.** Digital goods are delivered, shipping labels are generated, license keys are minted, whatever the products require.
9. **Notify.** The customer gets a receipt email. The merchant gets a "new order" notification. Anyone else who should know is told.

Each of these steps is one **stage** in what we call the **checkout pipeline** — an ordered chain that the order travels through. The pipeline is a separate package; it'll be fleshed out in upcoming phases of the v1.4.0 work. For now, what matters is the *shape*: the checkout service hands a snapshot of the cart to the pipeline, the pipeline steps run in order, and the customer-facing surface streams updates as each stage finishes.

## Streaming progress to the customer

Watch a sports score app on your phone: when the game updates, the score appears on your screen *without you tapping refresh*. The server pushes the new score down. That's the same model the checkout uses. As each pipeline stage finishes, the server emits an **event** — "validating line items", "checking inventory", "applying discount", "quoting tax", "capturing payment", and so on — and the customer's browser, which is listening, updates the progress display in real time.

In code terms, the checkout service exposes an *asynchronous stream* of events: the browser asks "what's happening with this checkout?" and the server says "stage 1 done, stage 2 done, stage 3 done, … succeeded" one at a time. This means even if the pipeline takes 6 seconds end-to-end, the customer sees a moving progress indicator the whole time instead of a frozen "loading…" spinner.

## What happens if something goes wrong

If any pipeline stage fails — payment declines, inventory was actually unavailable, tax provider times out — the pipeline emits a **CheckoutFailed** event. The customer's browser shows a friendly explanation ("we couldn't authorize your card; please try a different one"), the order doesn't ship, and the customer can revise the cart and try again.

A specific edge case worth noting: if the merchant hasn't actually wired up the checkout pipeline yet (because they're still building their store and haven't gotten to it), the checkout service emits a polite CheckoutFailed saying "the pipeline isn't registered — please tell your developer." The system is designed to fail visibly + gracefully rather than crash + take the storefront down with it.

## Where the customer's name shows up

For an authenticated customer, the server already knows who they are (from the sign-in cookie they're carrying around), so checkout just goes ahead with their saved details. For a guest, the customer **has** to supply an email address as part of initiating checkout — otherwise the merchant has no way to send them a receipt. The checkout service refuses to start a guest checkout without an email.

Optional billing-address-different-from-shipping handling, marketing-opt-in checkboxes, gift-message fields, etc., all sit alongside the email on the initiation command. The merchant configures which to require + show; the checkout service just records them.

## Once the order is paid

Once payment captures and fulfillment runs, the checkout session gets marked **Completed** with the resulting **order id**. Subsequent visits to the order-confirmation URL load the session by id and show the customer what they bought. The merchant's order-management tools pick up the new order from the order id and run their post-sale workflows (packing slips, inventory deductions, accounting sync).

The cart, by this point, has done its job; the server typically clears it so the customer starts fresh next visit.

## Things to know

- **Cart sizes have hard ceilings.** The defaults are 100 distinct lines and $10,000 grand total. Merchants tune these via `StorefrontOptions` if their use case demands different limits — wholesale storefronts often raise the line limit; consumer storefronts sometimes lower the dollar cap as a fraud guardrail.
- **Discount codes are not validated by the cart.** Customers can type any string into the discount field and the cart will record it without complaint. They only find out whether the code is real at checkout time, when the pipeline's discount stage looks it up. This is intentional — it keeps the cart simple and means an out-of-date code in the merchant's table never causes a cart save to fail.
- **Guest cookies last 30 days by default.** A customer's guest cart survives them closing and re-opening their browser. After 30 days of inactivity, the cookie expires and the customer effectively starts fresh.
- **Guest cookies are signed, not encrypted-with-secrets.** The cookie contains the guest's session id (a meaningless random number, not their identity) sealed by the ASP.NET Core data-protection keys. The data-protection keys are managed by the host application, not by PolarSharp. Merchants in multi-server deployments need to configure data-protection with a shared key store (such as Azure Key Vault, AWS KMS, or a shared file share) so cookies issued by one server are readable by every other server.
- **Multi-tenant storefronts get per-tenant carts automatically.** If a single customer shops at two storefronts on the same multi-tenant deployment, they get two separate carts — one per tenant. Switching tenants doesn't bleed cart contents between merchants.
- **The default cart store is in-memory.** It's perfect for development. For production it's almost always replaced with a database-backed store; the swap is one line in startup configuration.
- **The checkout service works even without the pipeline registered.** It emits a clear failure event explaining the misconfiguration. This means the storefront-core packages can be installed and exercised before the pipeline package is wired, supporting incremental adoption.
- **PolarSharp does not talk to Stripe.** Payouts, bank-account setup, and connect-style onboarding all happen on the merchant's Polar.sh dashboard. The PolarSharp checkout service handles **customer payments** (credit cards charged for orders); the merchant connects their bank account to **Polar.sh itself** so Polar.sh can route those payments to them. The merchant may see Stripe-branded screens inside Polar.sh's dashboard when they set up their bank account — that's a Polar.sh internal design detail, not something PolarSharp is involved in.
- **Tests exist for both modes.** The cart + checkout services have unit tests covering single-tenant authenticated, single-tenant guest, multi-tenant authenticated, and multi-tenant guest paths. The fraud-prevention discipline (server-side price re-validation, quantity clamping, cart-size limits) is also under test.

## See also

- [Storefronts Cart + Checkout (technical article)](../storefronts-cart-checkout.md)
- [Choosing your PolarSharp DI wiring](choosing-your-polarsharp-di-wiring.md) — how all these packages compose into one DI graph
- [Ecommerce Store Management](../ecommerce-catalog.md) — the catalog the cart service re-validates against
