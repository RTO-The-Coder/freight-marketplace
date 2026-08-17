using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteLegs",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegIndex = table.Column<int>(type: "integer", nullable: false),
                    DurationTicks = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteLegs", x => new { x.TruckId, x.LegIndex });
                });

            migrationBuilder.CreateTable(
                name: "RouteProgresses",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentLegIndex = table.Column<int>(type: "integer", nullable: false),
                    TicksElapsedInCurrentLeg = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteProgresses", x => x.TruckId);
                });

            migrationBuilder.CreateTable(
                name: "Shippers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shippers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TruckingCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OfficeLatitude = table.Column<double>(type: "double precision", nullable: false),
                    OfficeLongitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckingCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationType = table.Column<int>(type: "integer", nullable: false),
                    PrimaryDriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondaryDriverId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverAssignment_Drivers_PrimaryDriverId",
                        column: x => x.PrimaryDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverAssignment_Drivers_SecondaryDriverId",
                        column: x => x.SecondaryDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverComplianceStates",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentActivity = table.Column<int>(type: "integer", nullable: false),
                    MinutesRemainingInCurrentActivity = table.Column<int>(type: "integer", nullable: false),
                    ContinuousDrivingMinutesSinceBreak = table.Column<int>(type: "integer", nullable: false),
                    AwaitingSecondBreakBlock = table.Column<bool>(type: "boolean", nullable: false),
                    DailyDrivingMinutesToday = table.Column<int>(type: "integer", nullable: false),
                    ExtendedDaysUsedThisWeek = table.Column<int>(type: "integer", nullable: false),
                    IsTodayExtended = table.Column<bool>(type: "boolean", nullable: false),
                    AwaitingSecondDailyRestBlock = table.Column<bool>(type: "boolean", nullable: false),
                    ReducedDailyRestsUsedSinceWeeklyRest = table.Column<int>(type: "integer", nullable: false),
                    WeeklyDrivingMinutesThisWeek = table.Column<int>(type: "integer", nullable: false),
                    WeeklyDrivingMinutesPriorWeek = table.Column<int>(type: "integer", nullable: false),
                    LastEvaluatedSimulatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "DriverRulePreferences",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakPreference = table.Column<int>(type: "integer", nullable: false),
                    DailyRestPreference = table.Column<int>(type: "integer", nullable: false),
                    WeeklyRestPreference = table.Column<int>(type: "integer", nullable: false),
                    ExtendDailyDrivingWhenEligible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRulePreferences", x => x.DriverId);
                    table.ForeignKey(
                        name: "FK_DriverRulePreferences_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickupLatitude = table.Column<double>(type: "double precision", nullable: false),
                    PickupLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryLongitude = table.Column<double>(type: "double precision", nullable: false),
                    CargoKind = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    VolumeCubicMeters = table.Column<double>(type: "double precision", nullable: false),
                    PickupWindowStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PickupWindowEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Truck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckType = table.Column<int>(type: "integer", nullable: false),
                    TotalWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    TotalVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: false),
                    RemainingWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    RemainingVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: false),
                    DriverAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    HazmatCertified = table.Column<bool>(type: "boolean", nullable: false),
                    MovementState = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Truck", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Truck_DriverAssignment_DriverAssignmentId",
                        column: x => x.DriverAssignmentId,
                        principalTable: "DriverAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Truck_TruckingCompanies_TruckingCompanyId",
                        column: x => x.TruckingCompanyId,
                        principalTable: "TruckingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stop",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedArrivalTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stop", x => new { x.TruckId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_Stop_Truck_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Truck",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverAssignment_PrimaryDriverId",
                table: "DriverAssignment",
                column: "PrimaryDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverAssignment_SecondaryDriverId",
                table: "DriverAssignment",
                column: "SecondaryDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipperId",
                table: "Shipments",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_Truck_DriverAssignmentId",
                table: "Truck",
                column: "DriverAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Truck_TruckingCompanyId",
                table: "Truck",
                column: "TruckingCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverComplianceStates");

            migrationBuilder.DropTable(
                name: "DriverRulePreferences");

            migrationBuilder.DropTable(
                name: "RouteLegs");

            migrationBuilder.DropTable(
                name: "RouteProgresses");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "Stop");

            migrationBuilder.DropTable(
                name: "Shippers");

            migrationBuilder.DropTable(
                name: "Truck");

            migrationBuilder.DropTable(
                name: "DriverAssignment");

            migrationBuilder.DropTable(
                name: "TruckingCompanies");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
