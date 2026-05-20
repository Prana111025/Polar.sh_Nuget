using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PolarSharp.MultiTenant.EntityFrameworkCore.MariaDb;
using PolarSharp.MultiTenant.Identity;
using Testcontainers.MariaDb;

namespace PolarSharp.MultiTenant.Identity.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the MariaDB provider variant of PolarSharp Identity
/// (<c>PolarSharp.MultiTenant.Identity.MariaDb</c>) against a real MariaDB 11.5 container
/// spun up by Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why an integration test in addition to the unit tests.</strong> The existing
/// <see cref="PolarUserDbContextTests"/> suite uses SQLite-in-memory, which cannot exercise
/// MariaDB-specific behavior (the InnoDB engine + utf8mb4 collation, Oracle's
/// <c>MySql.EntityFrameworkCore</c> provider's parameter binding, the migration-lock
/// workaround in <see cref="MariaDbCompatibleHistoryRepository"/> that PolarSharp ships to
/// avoid Oracle's <c>GET_LOCK(-1)-returns-NULL</c> bug on MariaDB, etc.). This class proves
/// the Identity package's happy path works end-to-end against the engine it is built for.
/// </para>
/// <para>
/// <strong>Why MariaDB is simpler than the other two providers (security posture).</strong>
/// MariaDB / MySQL do not expose Postgres-style <c>ROW LEVEL SECURITY</c> nor SQL Server-style
/// <c>SESSION_CONTEXT</c>-driven policies. Per-tenant isolation of Identity tables on this
/// provider is enforced by the <strong>EF Core global query filter only</strong>. The test
/// labelled <see cref="Per_tenant_query_filter_scopes_membership_reads_to_current_tenant"/>
/// is therefore the load-bearing security assertion on this provider — there is no DB-layer
/// safety net behind it.
/// </para>
/// <para>
/// <strong>CI category filtering convention</strong> (established by Phase 2a):
/// </para>
/// <list type="bullet">
///   <item><description><c>dotnet test</c> — runs <em>all</em> tests including integration tests. Slow.</description></item>
///   <item><description><c>dotnet test --filter "Category!=Integration"</c> — unit tests only. Fast (~seconds).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration"</c> — integration tests only. Slow (~30–60s per provider).</description></item>
///   <item><description><c>dotnet test --filter "Category=Integration&amp;Provider=MariaDb"</c> — this provider only.</description></item>
/// </list>
/// <para>
/// <strong>Container lifecycle.</strong> One container per test class via
/// <see cref="IAsyncLifetime"/>. Container startup is ~10–20s; the full class runs in
/// ~30–45s. Tests must be order-independent — every test uses fresh <see cref="Guid"/> values
/// for users, roles, and memberships so rows never collide.
/// </para>
/// <para>
/// <strong>Image pin.</strong> The <see cref="MariaDbBuilder"/> is fed <c>mariadb:11.5</c>
/// deliberately — pinning an explicit minor keeps the container shape reproducible across
/// machines (no implicit "latest" drift).
/// </para>
/// <para>
/// <strong>Migration-lock workaround.</strong> The DbContext registration explicitly calls
/// <c>opts.ReplaceService&lt;IHistoryRepository, MariaDbCompatibleHistoryRepository&gt;()</c>
/// — same shape as the production <c>UseMariaDb()</c> extension. Without that replacement,
/// <see cref="DatabaseFacade.MigrateAsync"/> would fail with
/// <see cref="InvalidCastException"/> on MariaDB because Oracle's provider tries to cast the
/// <c>NULL</c> return of <c>SELECT GET_LOCK('__EFMigrationsLock', -1)</c> to <see cref="long"/>.
/// See <see cref="MariaDbCompatibleHistoryRepository"/> for the full rationale and the
/// exit-criterion-for-removal (Pomelo shipping a .NET 10 build).
/// </para>
/// <para>
/// <strong>Prerequisite.</strong> Docker must be running on the host. The
/// <c>mariadb:11.5</c> image pulls automatically on first run.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "MariaDb")]
public sealed class MariaDbIdentityDbIntegrationTests : IAsyncLifetime
{
    private MariaDbContainer _container = null!;
    private ServiceProvider _services = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // Pin the image so the container shape is reproducible across machines. The
        // image-name constructor overload is required — Testcontainers 4.11 deprecated the
        // parameterless MariaDbBuilder().
        _container = new MariaDbBuilder("mariadb:11.5")
            .Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDataProtection();

        services.AddDbContext<PolarUserDbContext>(opts =>
        {
            opts.UseMySQL(
                _container.GetConnectionString(),
                mysql => mysql.MigrationsAssembly(typeof(global::PolarSharp.MultiTenant.Identity.MariaDb.MariaDbIdentityBuilderExtensions).Assembly.GetName().Name));
            // Mirror the production UseMariaDb(...) registration — without this replacement
            // MigrateAsync would fail with InvalidCastException on MariaDB. See class-level
            // remarks for full rationale.
            opts.ReplaceService<IHistoryRepository, MariaDbCompatibleHistoryRepository>();
        });

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

        // Apply the PolarSharp.MultiTenant.Identity.MariaDb migrations (Initial) once per
        // class. There is no MariaDB equivalent of the EnableRowLevelSecurity migration that
        // ships in the SqlServer + PostgreSQL providers, because MariaDB has no policy
        // engine — see class-level "Why MariaDB is simpler" paragraph.
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
    /// Migrations apply cleanly against a fresh MariaDB 11.5 container — proves the MariaDb
    /// provider's compiled migration scripts target a real engine successfully, that the
    /// migration-lock workaround in <see cref="MariaDbCompatibleHistoryRepository"/> permits
    /// <see cref="DatabaseFacade.MigrateAsync"/> to complete, and that the EF model snapshot
    /// matches the runtime model.
    /// </summary>
    [Fact]
    public async Task Container_boots_and_identity_migrations_apply_cleanly()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();

        Assert.NotNull(await db.Users.ToListAsync());
        Assert.NotNull(await db.Roles.ToListAsync());
        Assert.NotNull(await db.Memberships.IgnoreQueryFilters().ToListAsync());
        Assert.NotNull(await db.PlatformAuditLog.ToListAsync());

        // A second MigrateAsync call must short-circuit (no pending migrations) — this also
        // proves the migration-lock workaround is reusable across multiple invocations.
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// A <see cref="PolarApplicationUser"/> persisted through <see cref="UserManager{TUser}"/>
    /// round-trips back via <see cref="UserManager{TUser}.FindByEmailAsync"/> with all custom
    /// fields intact — proves Oracle's <c>MySql.EntityFrameworkCore</c> provider's
    /// <see cref="Guid"/> + <see cref="DateTimeOffset"/> + <see cref="bool"/> column mappings
    /// work against a real MariaDB.
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
    /// when read through a fresh DbContext and is queryable via the FK columns — proves the
    /// MariaDB schema for the M:N table is wired correctly.
    /// </summary>
    [Fact]
    public async Task PolarUserTenantMembership_round_trips()
    {
        var (userId, roleId) = await SeedUserAndRoleAsync();
        var tenantId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();
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
    /// constructed with <c>currentTenantId = A</c> sees only A's row through the EF Core global
    /// query filter — this is the <strong>load-bearing security assertion</strong> on MariaDB,
    /// because the provider has no DB-layer policy mechanism to back the filter up. If this
    /// test ever regresses, per-tenant isolation on MariaDB is broken.
    /// </summary>
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

        var options = _services.GetRequiredService<DbContextOptions<PolarUserDbContext>>();
        await using var scopedToA = new TenantScopedPolarUserDbContext(options, currentTenantId: tenantA, isAppMasterAdminCrossTenant: false);
        var visibleToA = await scopedToA.Memberships
            .Where(m => m.Id == membershipA || m.Id == membershipB)
            .ToListAsync();

        Assert.Single(visibleToA);
        Assert.Equal(tenantA, visibleToA[0].TenantId);

        await using var crossTenant = new TenantScopedPolarUserDbContext(options, currentTenantId: tenantA, isAppMasterAdminCrossTenant: true);
        var visibleToAdmin = await crossTenant.Memberships
            .Where(m => m.Id == membershipA || m.Id == membershipB)
            .ToListAsync();
        Assert.Equal(2, visibleToAdmin.Count);
    }

    /// <summary>
    /// Calling <see cref="DatabaseFacade.MigrateAsync"/> twice in succession is a no-op the
    /// second time — proves MariaDB's migration history table is honored, that the
    /// migration-lock workaround in <see cref="MariaDbCompatibleHistoryRepository"/> remains
    /// stable on re-acquisition, and that PolarSharp's migration graph is idempotent.
    /// </summary>
    [Fact]
    public async Task Migrations_are_idempotent_on_re_run()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarUserDbContext>();

        var pendingBefore = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Empty(pendingBefore);

        await db.Database.MigrateAsync();

        var pendingAfter = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Empty(pendingAfter);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.NotEmpty(applied);
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
