using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL.Migrations
{
    /// <summary>
    /// V20-008 Layer 2: enables PostgreSQL row-level security on all 12
    /// <c>ITenantOwned</c> catalog tables. Pairs with
    /// <c>PostgreSqlTenantSessionInterceptor</c>.
    /// </summary>
    /// <remarks>
    /// Up() also captures pre-existing model drift (BeforeValues/AfterValues column
    /// types on polar_admin_audit_log) that was never migrated — those AlterColumn
    /// calls were emitted by the migration generator independently of the V20-008 work.
    /// </remarks>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Pre-existing model drift catch-up (auto-generated) ──────────────
            // NOTE: the oldType strings were originally "nvarchar(max)" — that came from the
            // EF scaffolding having been run against a SqlServer-shaped model snapshot. On
            // PostgreSQL the columns were actually created as `text` by Npgsql's type mapper
            // at Initial-migration time (Npgsql silently maps `nvarchar(max)` → `text` in
            // CreateTable). The npgsql migration SQL generator does NOT apply that same
            // mapping to AlterColumn's oldType — it emits `ALTER COLUMN … USING …::nvarchar`
            // and PostgreSQL rejects the cast with "type nvarchar does not exist" (error 42704).
            // Updating oldType to the engine-truth value ("text") makes the AlterColumn a
            // detected no-op and the migration applies cleanly. Discovered by the
            // PostgreSqlCatalogDbIntegrationTests Testcontainers harness on 2026-05-20.
            migrationBuilder.AlterColumn<string>(
                name: "BeforeValues",
                table: "polar_admin_audit_log",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AfterValues",
                table: "polar_admin_audit_log",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // ── V20-008 row-level security ──────────────────────────────────────
            migrationBuilder.Sql(@"
                ALTER TABLE polar_business_profiles      ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_business_profiles      FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_products         ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_products         FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_product_categories ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_product_categories FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_variants         ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_variants         FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_categories       ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_categories       FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_departments      ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_departments      FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_tier_groups      ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_tier_groups      FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_benefits         ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_benefits         FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_discounts        ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_discounts        FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_checkout_links   ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_local_checkout_links   FORCE ROW LEVEL SECURITY;
                ALTER TABLE polar_admin_audit_log        ENABLE ROW LEVEL SECURITY; ALTER TABLE polar_admin_audit_log        FORCE ROW LEVEL SECURITY;
                ALTER TABLE catalog_translations         ENABLE ROW LEVEL SECURITY; ALTER TABLE catalog_translations         FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation ON polar_business_profiles      USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_products         USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_product_categories USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_variants         USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_categories       USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_departments      USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_tier_groups      USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_benefits         USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_discounts        USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_local_checkout_links   USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON polar_admin_audit_log        USING (""TenantId"" = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
                CREATE POLICY tenant_isolation ON catalog_translations         USING (""TenantId""::text = current_setting('app.current_tenant_id', true) OR current_setting('app.is_app_master_admin', true) = 'true');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON polar_business_profiles;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_products;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_product_categories;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_variants;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_categories;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_departments;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_tier_groups;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_benefits;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_discounts;
                DROP POLICY IF EXISTS tenant_isolation ON polar_local_checkout_links;
                DROP POLICY IF EXISTS tenant_isolation ON polar_admin_audit_log;
                DROP POLICY IF EXISTS tenant_isolation ON catalog_translations;

                ALTER TABLE polar_business_profiles      NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_business_profiles      DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_products         NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_products         DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_product_categories NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_product_categories DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_variants         NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_variants         DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_categories       NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_categories       DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_departments      NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_departments      DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_tier_groups      NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_tier_groups      DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_benefits         NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_benefits         DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_discounts        NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_discounts        DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_local_checkout_links   NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_local_checkout_links   DISABLE ROW LEVEL SECURITY;
                ALTER TABLE polar_admin_audit_log        NO FORCE ROW LEVEL SECURITY; ALTER TABLE polar_admin_audit_log        DISABLE ROW LEVEL SECURITY;
                ALTER TABLE catalog_translations         NO FORCE ROW LEVEL SECURITY; ALTER TABLE catalog_translations         DISABLE ROW LEVEL SECURITY;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "BeforeValues",
                table: "polar_admin_audit_log",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AfterValues",
                table: "polar_admin_audit_log",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
