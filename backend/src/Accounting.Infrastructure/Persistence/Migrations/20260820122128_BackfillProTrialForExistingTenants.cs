using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProTrialForExistingTenants : Migration
    {
        /// <summary>
        /// öncesi kaydolmuş işletmeler (abonelik satırı olmayanlar) 14 günlük
        /// Pro denemesiyle sisteme davet edilir — yeni kayıtlarla aynı deneyim.
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Subscriptions" (
                    "Id", "TenantId", "PlanId", "Status",
                    "CurrentPeriodStartUtc", "CurrentPeriodEndUtc", "TrialEndsAtUtc",
                    "CancelledAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), t."Id", p."Id", 1,
                       now(), now() + interval '14 days', now() + interval '14 days',
                       NULL, now(), NULL
                FROM "Tenants" t
                CROSS JOIN "SubscriptionPlans" p
                WHERE p."Code" = 'pro'
                  AND NOT EXISTS (
                      SELECT 1 FROM "Subscriptions" s WHERE s."TenantId" = t."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: yalnızca bu migration'ın eklediği deneme satırlarını sil.
            migrationBuilder.Sql("""
                DELETE FROM "Subscriptions" s
                USING "Tenants" t
                WHERE s."TenantId" = t."Id"
                  AND s."Status" = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM "Subscriptions" s2
                      WHERE s2."TenantId" = t."Id" AND s2."Id" != s."Id");
                """);
        }
    }
}
