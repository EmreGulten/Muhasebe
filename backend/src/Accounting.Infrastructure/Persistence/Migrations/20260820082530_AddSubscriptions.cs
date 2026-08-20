using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxWarehouses = table.Column<int>(type: "integer", nullable: false),
                    AiMonthlyQuestionLimit = table.Column<int>(type: "integer", nullable: false),
                    Features = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiMonthlyQuestionLimit", "Code", "CreatedAtUtc", "Features", "IsActive", "MaxUsers", "MaxWarehouses", "MonthlyPrice", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), 0, "starter", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", true, 1, 1, 199m, "Başlangıç" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), 100, "pro", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "stock,purchases,reports_advanced,ai_assistant,quotes", true, 5, 3, 349m, "Pro" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), 1000, "business", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "stock,purchases,reports_advanced,ai_assistant,multi_warehouse,api,integrations,quotes", true, 10, -1, 599m, "İşletme" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_CreatedAtUtc",
                table: "Subscriptions",
                columns: new[] { "TenantId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");
        }
    }
}
