using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.Identity;
using Testcontainers.MsSql;

namespace PolarSharp.MultiTenant.Identity.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the SQL Server provider variant of PolarSharp Identity
/// (<c>PolarSharp.MultiTenant.Identity.SqlServer</c>) against a real SQL Server 2022 container
/// spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The existing
/// <see cref="PolarUserDbContextTests"/> suite uses SQLite-in-memory, which cannot exercise
/// SQL-Server-specific behavior (the <c>nvarchar</c> / <c>uniqueidentifier</c> column types,
/// SQL Server's identity columns, the SqlServer-specific migration script that wires up
/// SESSION_CONTEXT-driven RLS in the <c>EnableRowLevelSecurity</c> migration, etc.). This
/// class proves the Identity package's happy path works end-to-end against the engine it is
/// built for.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (established by Phase 2a):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test</c> — runs <em>all</em> tests including integration tests. Slow.</description></item>
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast (~seconds).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — integration tests only. Slow (~30–60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=SqlServer"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Container startup is ~25–40s (SQL Server is the heaviest of
/// the three engines); the full class runs in ~45–75s. Tests must be order-independent because
/// xUnit does not guarantee execution order within a class — every test uses fresh
/// <see cref="Guid"/> values for users, roles, and memberships so rows never collide.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="MsSqlBuilder"/> is fed
/// <c>mcr.microsoft.com/mssql/server:2022-latest</c> deliberately — pinning the major version
/// keeps the container shape reproducible across machines (no implicit "latest" drift).
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>mcr.microsoft.com/mssql/server:2022-latest</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
public sealed class SqlServerIdentityDbIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer _container = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines. The
        // image-name constructor overload is required — Testcontainers 4.11 deprecated the
        // parameterless MsSqlBuilder().
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await _container.StartAsync();

        // Build a DI graph that mirrors what UseSqlServer() wires up in production for
        // standalone integration testing. We intentionally bypass the production
        // SqlServerIdentityBuilderExtensions.UseSqlServer here because that overload also
        // registers SqlServerTenantSessionInterceptor (which assumes a Finbuckle pipeline is
        // present). The Identity DbContext + migrations are what we want to exercise.
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDataProtection();

        services.AddDbContext<PolarUserDbContext>(opts =>
            opts.UseSqlServer(
                _container.GetConnectionString(),
                sql => sql.MigrationsAssembly(typeof(global::PolarSharp.MultiTenant.Identity.SqlServer.SqlServerIdentityBuilderExtensions).Assembly.GetName().Name)));

        services.AddIdentityCore<PolarApplicationUser>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequiredLength = 8;
                opts.Password.RequireDigit = false;
                opts.Password.RequireLowercase = false;
                opts.Password.RequireUppercase = false;
                opts.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<PolarApplicationRole>()
            .AddEntityFrameworkStores<PolarUserDbContext>()
            .AddDefaultTokenProviders();

        _services = services.BuildServiceProvider();

        // Apply all PolarSharp.MultiTenant.Identity.SqlServer migrations once per class
        // (Initial + EnableRowLevelSecurity).
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Migrations apply cleanly against a fresh SQL Server 2022 container — proves the
    /// SqlServer provider's compiled migration scripts target a real engine successfully and
    /// that the EF model snapshot matches the runtime model.
    /// </summary>
    [Fact]
    public async Task Container_boots_and_identity_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();

        // MigrateAsync was called in InitializeAsync; verifying the resulting schema by
        // touching each Identity-derived DbSet proves the tables exist + are queryable.
        Assert.NotNull(await db.Users.ToListAsync());
        Assert.NotNull(await db.Roles.ToListAsync());
        Assert.NotNull(await db.Memberships.IgnoreQueryFilters().ToListAsync());
        Assert.NotNull(await db.PlatformAuditLog.ToListAsync());

        // A second MigrateAsync call must short-circuit (no pending migrations) — this is
        // the "Migrations_are_idempotent_on_re_run" guarantee folded into the smoke test.
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// A <see cref="PolarApplicationUser"/> persisted through <see cref="UserManager{TUser}"/>
    /// round-trips back via <see cref="UserManager{TUser}.FindByEmailAsync"/> with all custom
    /// fields intact — proves <c>nvarchar</c> + <c>datetimeoffset</c> + <c>bit</c> column
    /// mappings work against a real SQL Server.
    /// </summary>
    [Fact]
    public async Task AspNetCore_Identity_user_round_trips()
    {
        using var scope = _services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PolarApplicationUser>>();

        var email = $"alice-{Guid.NewGuid():N}@example.com";
        var user = new PolarApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = "Alice Example",
            OnboardedAt = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero),
            IsAppMasterAdmin = false,
        };

        var createResult = await userManager.CreateAsync(user, "Password!1");
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

        var loaded = await userManager.FindByEmailAsync(email);
        Assert.NotNull(loaded);
        Assert.Equal("Alice Example", loaded!.FullName);
        Assert.Equal(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero), loaded.OnboardedAt);
        Assert.False(loaded.IsAppMasterAdmin);
    }

    /// <summary>
    /// A <see cref="PolarUserTenantMembership"/> linking a user to a tenant via a role round-trips
    /// when read through the same DbContext (filter bypassed) and is queryable via the FK columns.
    /// </summary>
    /// <remarks>
    /// The SQL Server provider ships an <c>EnableRowLevelSecurity</c> migration that installs a
    /// <c>SECURITY POLICY</c> with a BLOCK PREDICATE on <c>polar_user_tenant_memberships</c> —
    /// inserts are refused unless <c>SESSION_CONTEXT(N'is_app_master_admin') = 1</c> (or
    /// <c>SESSION_CONTEXT(N'tenant_id')</c> matches the row's TenantId). Production hosts get
    /// this set automatically by <c>SqlServerTenantSessionInterceptor</c>; this test sets it
    /// manually because the DI graph here intentionally bypasses the production interceptor
    /// (the interceptor requires a Finbuckle pipeline that is not wired here).
    /// </remarks>
    [Fact]
    public async Task PolarUserTenantMembership_round_trips()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
            await OpenConnectionAndSetCrossTenantContextAsync(db);
            db.Memberships.Add(new PolarUserTenantMembership
            {
                Id = membershipId,
                UserId = userId,
                TenantId = tenantId,
                RoleId = roleId,
                JoinedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
            await OpenConnectionAndSetCrossTenantContextAsync(db);
            // Default constructor → _currentTenantId == null → EF Core filter bypassed.
            // The DB-layer RLS FILTER PREDICATE still applies, hence the cross-tenant
            // session context set above.
            var loaded = await db.Memberships
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(m => m.Id == membershipId);

            Assert.NotNull(loaded);
            Assert.Equal(userId, loaded!.UserId);
            Assert.Equal(tenantId, loaded.TenantId);
            Assert.Equal(roleId, loaded.RoleId);
            Assert.True(loaded.IsActive);
        }
    }

    /// <summary>
    /// With memberships inserted for two different tenants, a <see cref="PolarUserDbContext"/>
    /// constructed with <c>currentTenantId = A</c> sees only A's row through the EF Core
    /// global query filter — proves the per-tenant scoping holds end-to-end on SQL Server in
    /// the EF layer (the DB-layer RLS FILTER PREDICATE is a separate defense layer covered by
    /// other tests).
    /// </summary>
    /// <remarks>
    /// Seeds the two memberships via a connection with the cross-tenant session context set
    /// (so the RLS BLOCK PREDICATE permits the inserts). Asserts the EF filter behavior on a
    /// connection that ALSO has the cross-tenant flag set — that way the only filter in play
    /// during the read is the EF Core global query filter, which is what this test is about.
    /// </remarks>
    [Fact]
    public async Task Per_tenant_query_filter_scopes_membership_reads_to_current_tenant()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var membershipA = Guid.NewGuid();
        var membershipB = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
            await OpenConnectionAndSetCrossTenantContextAsync(db);
            db.Memberships.AddRange(
                new PolarUserTenantMembership
                {
                    Id = membershipA,
                    UserId = userId,
                    TenantId = tenantA,
                    RoleId = roleId,
                    JoinedAt = DateTimeOffset.UtcNow,
                    IsActive = true,
                },
                new PolarUserTenantMembership
                {
                    Id = membershipB,
                    UserId = userId,
                    TenantId = tenantB,
                    RoleId = roleId,
                    JoinedAt = DateTimeOffset.UtcNow,
                    IsActive = true,
                });
            await db.SaveChangesAsync();
        }

        // Construct a tenant-aware context with currentTenantId = A. The protected constructor
        // is reached through the test-only TenantScopedPolarUserDbContext subclass. The EF
        // query filter runs first; we set the SQL-Server session context to is_app_master_admin
        // so the underlying RLS FILTER PREDICATE does not also intervene — isolating the EF
        // filter as the only mechanism under test.
        var options = _services.GetRequiredService<DbContextOptions<PolarUserDbContext>>();
        await using var scopedToA = new TenantScopedPolarUserDbContext(options, currentTenantId: tenantA, isAppMasterAdminCrossTenant: false);
        await OpenConnectionAndSetCrossTenantContextAsync(scopedToA);
        var visibleToA = await scopedToA.Memberships
            .Where(m => m.Id == membershipA || m.Id == membershipB)
            .ToListAsync();

        Assert.Single(visibleToA);
        Assert.Equal(tenantA, visibleToA[0].TenantId);

        // Sanity check: with the EF cross-tenant flag set, both rows are visible.
        await using var crossTenant = new TenantScopedPolarUserDbContext(options, currentTenantId: tenantA, isAppMasterAdminCrossTenant: true);
        await OpenConnectionAndSetCrossTenantContextAsync(crossTenant);
        var visibleToAdmin = await crossTenant.Memberships
            .Where(m => m.Id == membershipA || m.Id == membershipB)
            .ToListAsync();
        Assert.Equal(2, visibleToAdmin.Count);
    }

    /// <summary>
    /// Calling <see cref="DatabaseFacade.MigrateAsync"/> twice in succession is a no-op the
    /// second time — proves the migration history table is honored and PolarSharp's migration
    /// graph is idempotent on SQL Server.
    /// </summary>
    [Fact]
    public async Task Migrations_are_idempotent_on_re_run()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();

        var pendingBefore = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Empty(pendingBefore);

        // Second invocation must short-circuit without throwing.
        await db.Database.MigrateAsync();

        var pendingAfter = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Empty(pendingAfter);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.NotEmpty(applied);
    }

    /// <summary>
    /// Opens the DbContext's underlying SQL Server connection (so EF will keep using it for
    /// the duration of the context's lifetime instead of returning it to the pool between
    /// commands) and sets <c>SESSION_CONTEXT(N'is_app_master_admin') = 1</c>. This is the
    /// shape of state that <c>SqlServerTenantSessionInterceptor</c> would set on every
    /// connection open in production; the integration test bypasses that interceptor and so
    /// must set the equivalent state directly. Without this the RLS BLOCK PREDICATE on the
    /// <c>polar_user_tenant_memberships</c> table refuses every insert.
    /// </summary>
    private static async Task OpenConnectionAndSetCrossTenantContextAsync(PolarUserDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'is_app_master_admin', @value=1;");
    }

    /// <summary>
    /// Seeds one <see cref="PolarApplicationUser"/> + one <see cref="PolarApplicationRole"/>
    /// with unique-per-call identifiers so membership tests can satisfy the FK constraints
    /// without colliding with other tests' rows.
    /// </summary>
    private async Task<(Guid userId, Guid roleId)> SeedUserAndRoleAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
        var suffix = Guid.NewGuid().ToString("N");

        var user = new PolarApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user-{suffix}@example.com",
            NormalizedUserName = $"USER-{suffix.ToUpperInvariant()}@EXAMPLE.COM",
            Email = $"user-{suffix}@example.com",
            NormalizedEmail = $"USER-{suffix.ToUpperInvariant()}@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        var role = new PolarApplicationRole($"TenantAdmin-{suffix}")
        {
            Id = Guid.NewGuid(),
            NormalizedName = $"TENANTADMIN-{suffix.ToUpperInvariant()}",
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            IsBuiltIn = false,
        };
        db.Users.Add(user);
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return (user.Id, role.Id);
    }
}
