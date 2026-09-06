using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                    LastName = table.Column<string>(type: "text", nullable: false),
                    BreakRule = table.Column<string>(type: "text", nullable: false),
                    DailyRestRule = table.Column<string>(type: "text", nullable: false),
                    WeeklyRestRule = table.Column<string>(type: "text", nullable: false),
                    ExtendDailyDrivingWhenEligible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupLatitude = table.Column<double>(type: "double precision", nullable: false),
                    PickupLongitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryLatitude = table.Column<double>(type: "double precision", nullable: false),
                    DeliveryLongitude = table.Column<double>(type: "double precision", nullable: false),
                    LoadWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    LoadVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: false),
                    RequiredTruckType = table.Column<string>(type: "text", nullable: false),
                    PickupWindowEarliest = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PickupWindowLatest = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryWindowEarliest = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryWindowLatest = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OfferDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledPickupWindowEarliest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledPickupWindowLatest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledDeliveryWindowEarliest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledDeliveryWindowLatest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedPickup = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
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
                name: "SimulationClock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationClock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DistanceTravelledSoFar = table.Column<double>(type: "double precision", nullable: false),
                    TimeElapsedSoFar = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
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
                name: "Trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckName = table.Column<string>(type: "text", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: false),
                    TotalCapacityWeightKg = table.Column<double>(type: "double precision", nullable: false),
                    TotalCapacityVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: false),
                    DriverConfigurationType = table.Column<string>(type: "text", nullable: true),
                    DriverAssignment_PrimaryDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverAssignment_SecondaryDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActiveDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    HazmatCertified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trucks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trucks_Drivers_DriverAssignment_PrimaryDriverId",
                        column: x => x.DriverAssignment_PrimaryDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trucks_Drivers_DriverAssignment_SecondaryDriverId",
                        column: x => x.DriverAssignment_SecondaryDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LocationLatitude = table.Column<double>(type: "double precision", nullable: false),
                    LocationLongitude = table.Column<double>(type: "double precision", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IncomingLegDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    IncomingLegTimeTick = table.Column<int>(type: "integer", nullable: false),
                    ReachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitTimeTick = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    WaitTimeTickElapsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ShipmentLoadWeightKg = table.Column<double>(type: "double precision", nullable: true),
                    ShipmentLoadVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: true),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripStops_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TruckRouteProgresses",
                columns: table => new
                {
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentProgress_TotalDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    CurrentProgress_TotalTimeTick = table.Column<int>(type: "integer", nullable: false),
                    CurrentProgress_CurrentDrivingTimeTick = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipperId",
                table: "Shipments",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TruckingCompanyId",
                table: "Shipments",
                column: "TruckingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TruckId",
                table: "Trips",
                column: "TruckId");

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_TripId",
                table: "TripStops",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_DriverAssignment_PrimaryDriverId",
                table: "Trucks",
                column: "DriverAssignment_PrimaryDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_DriverAssignment_SecondaryDriverId",
                table: "Trucks",
                column: "DriverAssignment_SecondaryDriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverComplianceStates");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "Shippers");

            migrationBuilder.DropTable(
                name: "SimulationClock");

            migrationBuilder.DropTable(
                name: "TripStops");

            migrationBuilder.DropTable(
                name: "TruckingCompanies");

            migrationBuilder.DropTable(
                name: "TruckRouteProgresses");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
