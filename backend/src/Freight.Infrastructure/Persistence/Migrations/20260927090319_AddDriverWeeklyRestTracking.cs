using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverWeeklyRestTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_CurrentActivityLengthMinutes",
                table: "DriverComplianceStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceState_LastWeeklyRestEndedAt",
                table: "DriverComplianceStates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_WeeklyRestMinutesOwed",
                table: "DriverComplianceStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing ledgers: the trip opening counts as the last weekly rest (freight-driving-rules.md
            // decision 10), so an in-progress trip isn't sent straight into a weekly rest by year 0001.
            migrationBuilder.Sql(
                """UPDATE "DriverComplianceStates" SET "ComplianceState_LastWeeklyRestEndedAt" = "ComplianceState_LastRestEndedAt";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComplianceState_CurrentActivityLengthMinutes",
                table: "DriverComplianceStates");

            migrationBuilder.DropColumn(
                name: "ComplianceState_LastWeeklyRestEndedAt",
                table: "DriverComplianceStates");

            migrationBuilder.DropColumn(
                name: "ComplianceState_WeeklyRestMinutesOwed",
                table: "DriverComplianceStates");
        }
    }
}
