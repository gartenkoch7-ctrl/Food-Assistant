using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FoodAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpiryLeadDays",
                table: "Households",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "ChangeLog",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLog", x => x.Sequence);
                    table.ForeignKey(
                        name: "FK_ChangeLog_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodFactsCache",
                columns: table => new
                {
                    Barcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Found = table.Column<bool>(type: "boolean", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodFactsCache", x => x.Barcode);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    SubjectVersionAfter = table.Column<long>(type: "bigint", nullable: true),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdActivities_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageLocations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastCheckedSectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stores_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DefaultStorageLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MinimumStockValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    MinimumStockUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MinimumStockTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    LastShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    LastExpiryKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FoodFactsBarcode = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_MinimumStock", "(\"MinimumStockValue\" IS NULL) = (\"MinimumStockUnitCode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Products_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Products_StorageLocations_DefaultStorageLocationId",
                        column: x => x.DefaultStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StoreSectionOrders",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSectionOrders", x => new { x.StoreId, x.SectionKey });
                    table.ForeignKey(
                        name: "FK_StoreSectionOrders_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreSectionOrders_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreSectionTransitions",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToSectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSectionTransitions", x => new { x.StoreId, x.FromSectionKey, x.ToSectionKey });
                    table.ForeignKey(
                        name: "FK_StoreSectionTransitions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreSectionTransitions_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsumptionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    QuantityValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    QuantityUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpiryStatusAtEvent = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumptionEvents_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConsumptionEvents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductBarcodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBarcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBarcodes_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductBarcodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShelfLifeOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DaysAfterOpening = table.Column<int>(type: "integer", nullable: true),
                    DaysFrozen = table.Column<int>(type: "integer", nullable: true),
                    DaysAfterThawing = table.Column<int>(type: "integer", nullable: true),
                    DaysAsLeftover = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelfLifeOverrides", x => x.Id);
                    table.CheckConstraint("CK_ShelfLifeOverrides_CategoryOrProduct", "(\"CategoryKey\" IS NULL) <> (\"ProductId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ShelfLifeOverrides_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShelfLifeOverrides_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    QuantityUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StorageType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PackageExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PackageExpiryKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    FrozenOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ThawedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PreparedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AddedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockBatches_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBatches_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringShoppingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantityValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    QuantityUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    NextDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastCreatedItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringShoppingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringShoppingRules_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringShoppingRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecurringShoppingRules_ShoppingLists_ListId",
                        column: x => x.ListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantityValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    QuantityUnitCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SectionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Checked = table.Column<bool>(type: "boolean", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingItems_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShoppingItems_ShoppingLists_ListId",
                        column: x => x.ListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLog_HouseholdId_Sequence",
                table: "ChangeLog",
                columns: new[] { "HouseholdId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEvents_HouseholdId_OccurredOn",
                table: "ConsumptionEvents",
                columns: new[] { "HouseholdId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEvents_HouseholdId_ProductId_OccurredAt",
                table: "ConsumptionEvents",
                columns: new[] { "HouseholdId", "ProductId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionEvents_ProductId",
                table: "ConsumptionEvents",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdActivities_HouseholdId_OccurredAt",
                table: "HouseholdActivities",
                columns: new[] { "HouseholdId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_HouseholdId_Code",
                table: "ProductBarcodes",
                columns: new[] { "HouseholdId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_ProductId",
                table: "ProductBarcodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DefaultStorageLocationId",
                table: "Products",
                column: "DefaultStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_HouseholdId_Name",
                table: "Products",
                columns: new[] { "HouseholdId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringShoppingRules_HouseholdId",
                table: "RecurringShoppingRules",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringShoppingRules_ListId",
                table: "RecurringShoppingRules",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringShoppingRules_ProductId",
                table: "RecurringShoppingRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfLifeOverrides_HouseholdId_CategoryKey_StorageType",
                table: "ShelfLifeOverrides",
                columns: new[] { "HouseholdId", "CategoryKey", "StorageType" },
                unique: true,
                filter: "\"CategoryKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfLifeOverrides_HouseholdId_ProductId_StorageType",
                table: "ShelfLifeOverrides",
                columns: new[] { "HouseholdId", "ProductId", "StorageType" },
                unique: true,
                filter: "\"ProductId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfLifeOverrides_ProductId",
                table: "ShelfLifeOverrides",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_HouseholdId_ListId",
                table: "ShoppingItems",
                columns: new[] { "HouseholdId", "ListId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_HouseholdId_ProductId",
                table: "ShoppingItems",
                columns: new[] { "HouseholdId", "ProductId" },
                filter: "\"DeletedAt\" IS NULL AND NOT \"Checked\"");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_ListId",
                table: "ShoppingItems",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_ProductId",
                table: "ShoppingItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_HouseholdId",
                table: "ShoppingLists",
                column: "HouseholdId",
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_StoreId",
                table: "ShoppingLists",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_HouseholdId_ExpiresOn",
                table: "StockBatches",
                columns: new[] { "HouseholdId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_HouseholdId_ProductId",
                table: "StockBatches",
                columns: new[] { "HouseholdId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_ProductId",
                table: "StockBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_StorageLocationId",
                table: "StockBatches",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_HouseholdId_SortOrder",
                table: "StorageLocations",
                columns: new[] { "HouseholdId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_HouseholdId",
                table: "Stores",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSectionOrders_HouseholdId",
                table: "StoreSectionOrders",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSectionTransitions_HouseholdId",
                table: "StoreSectionTransitions",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeLog");

            migrationBuilder.DropTable(
                name: "ConsumptionEvents");

            migrationBuilder.DropTable(
                name: "FoodFactsCache");

            migrationBuilder.DropTable(
                name: "HouseholdActivities");

            migrationBuilder.DropTable(
                name: "ProductBarcodes");

            migrationBuilder.DropTable(
                name: "RecurringShoppingRules");

            migrationBuilder.DropTable(
                name: "ShelfLifeOverrides");

            migrationBuilder.DropTable(
                name: "ShoppingItems");

            migrationBuilder.DropTable(
                name: "StockBatches");

            migrationBuilder.DropTable(
                name: "StoreSectionOrders");

            migrationBuilder.DropTable(
                name: "StoreSectionTransitions");

            migrationBuilder.DropTable(
                name: "ShoppingLists");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "ExpiryLeadDays",
                table: "Households");
        }
    }
}
