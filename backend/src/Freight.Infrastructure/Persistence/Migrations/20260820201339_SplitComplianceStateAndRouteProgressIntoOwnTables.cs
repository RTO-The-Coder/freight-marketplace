using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitComplianceStateAndRouteProgressIntoOwnTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentProgress_CurrentDistanceKm",
                table: "Trucks");

            migrationBuilder.DropColumn(
                name: "CurrentProgress_TotalDistanceKm",
                table: "Trucks");

            migrationBuilder.DropColumn(
                name: "CurrentProgress_TotalTimeTick",
                table: "Trucks");

            migrationBuilder.DropColumn(
                name: "ComplianceState_AwaitingSecondBreakBlock",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_AwaitingSecondDailyRestBlock",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_ContinuousDrivingMinutesSinceBreak",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_CurrentActivity",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_DailyDrivingMinutesToday",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_ExtendedDaysUsedThisWeek",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_IsTodayExtended",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_LastEvaluatedSimulatedTime",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_MinutesRemainingInCurrentActivity",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_ReducedDailyRestsUsedSinceWeeklyRest",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_WeeklyDrivingMinutesPriorWeek",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ComplianceState_WeeklyDrivingMinutesThisWeek",
                table: "Drivers");

            migrationBuilder.CreateTable(
                name: "DriverComplianceStates",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceState_CurrentActivity = table.Column<string>(type: "text", nullable: false),
                    ComplianceState_MinutesRemainingInCurrentActivity = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_ContinuousDrivingMinutesSinceBreak = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_AwaitingSecondBreakBlock = table.Column<bool>(type: "boolean", nullable: false),
                    ComplianceState_DailyDrivingMinutesToday = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_ExtendedDaysUsedThisWeek = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_IsTodayExtended = table.Column<bool>(type: "boolean", nullable: false),
                    ComplianceState_AwaitingSecondDailyRestBlock = table.Column<bool>(type: "boolean", nullable: false),
                    ComplianceState_ReducedDailyRestsUsedSinceWeeklyRest = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_WeeklyDrivingMinutesThisWeek = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_WeeklyDrivingMinutesPriorWeek = table.Column<int>(type: "integer", nullable: false),
                    ComplianceState_LastEvaluatedSimulatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverComplianceStates", x => x.DriverId);
                    table.ForeignKey(
                        name: "FK_DriverComplianceStates_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TruckRouteProgresses",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentProgress_TotalDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    CurrentProgress_CurrentDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    CurrentProgress_TotalTimeTick = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckRouteProgresses", x => x.TruckId);
                    table.ForeignKey(
                        name: "FK_TruckRouteProgresses_Trucks_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Trucks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverComplianceStates");

            migrationBuilder.DropTable(
                name: "TruckRouteProgresses");

            migrationBuilder.AddColumn<double>(
                name: "CurrentProgress_CurrentDistanceKm",
                table: "Trucks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentProgress_TotalDistanceKm",
                table: "Trucks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentProgress_TotalTimeTick",
                table: "Trucks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComplianceState_AwaitingSecondBreakBlock",
                table: "Drivers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComplianceState_AwaitingSecondDailyRestBlock",
                table: "Drivers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_ContinuousDrivingMinutesSinceBreak",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceState_CurrentActivity",
                table: "Drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_DailyDrivingMinutesToday",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_ExtendedDaysUsedThisWeek",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ComplianceState_IsTodayExtended",
                table: "Drivers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ComplianceState_LastEvaluatedSimulatedTime",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_MinutesRemainingInCurrentActivity",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_ReducedDailyRestsUsedSinceWeeklyRest",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_WeeklyDrivingMinutesPriorWeek",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceState_WeeklyDrivingMinutesThisWeek",
                table: "Drivers",
                type: "integer",
                nullable: true);
        }
    }
}
