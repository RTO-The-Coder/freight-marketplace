using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverLastRestEndedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceState_LastRestEndedAt",
                table: "DriverComplianceStates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing ledgers: treat the last evaluation as the last rest end, so an
            // in-progress team trip isn't pushed straight into a shared rest by year 0001.
            migrationBuilder.Sql(
                """UPDATE "DriverComplianceStates" SET "ComplianceState_LastRestEndedAt" = "ComplianceState_LastEvaluatedSimulatedTime";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComplianceState_LastRestEndedAt",
                table: "DriverComplianceStates");
        }
    }
}
