using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TranslatedValue = table.Column<string>(type: "text", nullable: false),
                    IsMachineTranslated = table.Column<bool>(type: "boolean", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SourceModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_translations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_admin_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    // Fixed 2026-05-20: original scaffold emitted "nvarchar(max)" (a SQL Server type)
                    // here because the EF migration was scaffolded against a SqlServer-shaped model
                    // snapshot. PostgreSQL has no `nvarchar` type and rejects the column with error
                    // 42704 "type \"nvarchar\" does not exist" at CREATE TABLE time. Discovered by
                    // the PostgreSqlCatalogDbIntegrationTests Testcontainers harness; the EnableRowLevelSecurity
                    // migration's matching AlterColumn oldType strings were corrected in the same pass.
                    BeforeValues = table.Column<string>(type: "text", nullable: true),
                    AfterValues = table.Column<string>(type: "text", nullable: true),
                    ChangedFields = table.Column<string>(type: "text", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false),
                    CrossTenantAccess = table.Column<bool>(type: "boolean", nullable: false),
                    CrossTenantJustification = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_admin_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_business_profiles",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TaxBehavior = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StreetLine1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StreetLine2 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateOrProvince = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProductDescription = table.Column<string>(type: "text", nullable: true),
                    IntendedUse = table.Column<string>(type: "text", nullable: true),
                    PricingModelsJson = table.Column<string>(type: "text", nullable: true),
                    SellingCategoriesJson = table.Column<string>(type: "text", nullable: true),
                    FutureAnnualRevenue = table.Column<long>(type: "bigint", nullable: true),
                    SwitchingFrom = table.Column<string>(type: "text", nullable: true),
                    LegalEntityJson = table.Column<string>(type: "text", nullable: true),
                    StripeConnectAccountId = table.Column<string>(type: "text", nullable: true),
                    PayoutAccountId = table.Column<string>(type: "text", nullable: true),
                    PayoutStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayoutStatusLastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TranslationProvider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TranslationApiKeyEncrypted = table.Column<string>(type: "text", nullable: true),
                    TranslationModel = table.Column<string>(type: "text", nullable: true),
                    TranslationEndpoint = table.Column<string>(type: "text", nullable: true),
                    MasterLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SupportedLanguagesJson = table.Column<string>(type: "text", nullable: false),
                    AutoTranslateOnSave = table.Column<bool>(type: "boolean", nullable: false),
                    AllowFakeData = table.Column<bool>(type: "boolean", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_business_profiles", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    BenefitKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PropertiesJson = table.Column<string>(type: "text", nullable: false),
                    PolarBenefitId = table.Column<string>(type: "text", nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_benefits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    MasterName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_checkout_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProductIdsJson = table.Column<string>(type: "text", nullable: false),
                    SuccessUrl = table.Column<string>(type: "text", nullable: true),
                    CancelUrl = table.Column<string>(type: "text", nullable: true),
                    ThemeColor = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "text", nullable: false),
                    AllowDiscountCodes = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBillingAddress = table.Column<bool>(type: "boolean", nullable: false),
                    PolarCheckoutLinkId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_checkout_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    MasterName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    MasterName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AmountOff = table.Column<int>(type: "integer", nullable: true),
                    PercentageOff = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    DurationWire = table.Column<string>(type: "text", nullable: true),
                    DurationKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DurationInMonths = table.Column<int>(type: "integer", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    ApplicableProductIdsJson = table.Column<string>(type: "text", nullable: false),
                    PolarDiscountId = table.Column<string>(type: "text", nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_discounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_product_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_product_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    MasterName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MasterDescription = table.Column<string>(type: "text", nullable: true),
                    MasterLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TierGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    HasVariants = table.Column<bool>(type: "boolean", nullable: false),
                    PriceJson = table.Column<string>(type: "text", nullable: false),
                    AttachedBenefitsJson = table.Column<string>(type: "text", nullable: false),
                    MsrpAmount = table.Column<int>(type: "integer", nullable: true),
                    MsrpCurrency = table.Column<string>(type: "text", nullable: true),
                    Manufacturer = table.Column<string>(type: "text", nullable: true),
                    Isbn = table.Column<string>(type: "text", nullable: true),
                    PolarProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_tier_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LevelsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_tier_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "polar_local_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AxesJson = table.Column<string>(type: "text", nullable: false),
                    SurchargeAmount = table.Column<int>(type: "integer", nullable: true),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PolarProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    InventoryCount = table.Column<int>(type: "integer", nullable: true),
                    InventoryLowThreshold = table.Column<int>(type: "integer", nullable: true),
                    LastStockChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsFakeData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polar_local_variants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_translations_TenantId_EntityType_EntityId",
                table: "catalog_translations",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_translations_TenantId_EntityType_EntityId_Language_~",
                table: "catalog_translations",
                columns: new[] { "TenantId", "EntityType", "EntityId", "Language", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_admin_audit_log_TenantId_EntityType_EntityId",
                table: "polar_admin_audit_log",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_admin_audit_log_TenantId_OccurredAt",
                table: "polar_admin_audit_log",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_benefits_TenantId_BenefitKind",
                table: "polar_local_benefits",
                columns: new[] { "TenantId", "BenefitKind" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_categories_TenantId_ParentCategoryId",
                table: "polar_local_categories",
                columns: new[] { "TenantId", "ParentCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_departments_TenantId_MasterName",
                table: "polar_local_departments",
                columns: new[] { "TenantId", "MasterName" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_discounts_TenantId_Code",
                table: "polar_local_discounts",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_ProductId_CategoryId",
                table: "polar_local_product_categories",
                columns: new[] { "ProductId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_TenantId_CategoryId",
                table: "polar_local_product_categories",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_product_categories_TenantId_ProductId",
                table: "polar_local_product_categories",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_IsFakeData",
                table: "polar_local_products",
                columns: new[] { "TenantId", "IsFakeData" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_MasterName",
                table: "polar_local_products",
                columns: new[] { "TenantId", "MasterName" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_products_TenantId_PolarProductId",
                table: "polar_local_products",
                columns: new[] { "TenantId", "PolarProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_polar_local_variants_TenantId_ProductId",
                table: "polar_local_variants",
                columns: new[] { "TenantId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_translations");

            migrationBuilder.DropTable(
                name: "polar_admin_audit_log");

            migrationBuilder.DropTable(
                name: "polar_business_profiles");

            migrationBuilder.DropTable(
                name: "polar_local_benefits");

            migrationBuilder.DropTable(
                name: "polar_local_categories");

            migrationBuilder.DropTable(
                name: "polar_local_checkout_links");

            migrationBuilder.DropTable(
                name: "polar_local_departments");

            migrationBuilder.DropTable(
                name: "polar_local_discounts");

            migrationBuilder.DropTable(
                name: "polar_local_product_categories");

            migrationBuilder.DropTable(
                name: "polar_local_products");

            migrationBuilder.DropTable(
                name: "polar_local_tier_groups");

            migrationBuilder.DropTable(
                name: "polar_local_variants");
        }
    }
}
