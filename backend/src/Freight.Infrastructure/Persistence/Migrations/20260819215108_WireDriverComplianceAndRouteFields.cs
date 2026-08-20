using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WireDriverComplianceAndRouteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "TruckRouteStops",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "TruckRouteStops",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "TruckRouteStops",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "LocationLatitude",
                table: "TruckRouteStops");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "TruckRouteStops");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "TruckRouteStops");

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
        }
    }
}
