using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_TenantId_Role_CreatedAtUtc",
                table: "AiMessages",
                columns: new[] { "TenantId", "Role", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_TenantId_UserId_CreatedAtUtc",
                table: "AiMessages",
                columns: new[] { "TenantId", "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMessages");
        }
    }
}
