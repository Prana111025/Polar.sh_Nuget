using Microsoft.EntityFrameworkCore;
using PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore;

namespace PolarSharp.PrepaidWallets.EventStore.EntityFrameworkCore.Tests;

/// <summary>
/// Concrete <see cref="WalletEventStoreDbContext"/> for the in-memory SQLite test database.
/// The real provider packages (<c>.SqlServer</c>, <c>.Sqlite</c>, etc.) ship their own
/// host-callable subclasses; this is the test mirror of those.
/// </summary>
public sealed class SqliteWalletEventStoreDbContext : WalletEventStoreDbContext
{
    public SqliteWalletEventStoreDbContext(DbContextOptions<SqliteWalletEventStoreDbContext> options)
        : base(options) { }
}
