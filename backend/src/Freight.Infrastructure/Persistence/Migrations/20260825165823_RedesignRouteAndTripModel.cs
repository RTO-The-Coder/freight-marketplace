using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignRouteAndTripModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TruckRouteStops");

            migrationBuilder.DropColumn(
                name: "CurrentProgress_CurrentDistanceKm",
                table: "TruckRouteProgresses");

            migrationBuilder.AddColumn<int>(
                name: "CurrentProgress_CurrentDrivingTimeTick",
                table: "TruckRouteProgresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TruckId",
                table: "Trips",
                column: "TruckId");

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_TripId",
                table: "TripStops",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulationClock");

            migrationBuilder.DropTable(
                name: "TripStops");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropColumn(
                name: "CurrentProgress_CurrentDrivingTimeTick",
                table: "TruckRouteProgresses");

            migrationBuilder.AddColumn<double>(
                name: "CurrentProgress_CurrentDistanceKm",
                table: "TruckRouteProgresses",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "TruckRouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedArrivalTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationLatitude = table.Column<double>(type: "double precision", nullable: false),
                    LocationLongitude = table.Column<double>(type: "double precision", nullable: false),
                    ShipmentLoadVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: true),
                    ShipmentLoadWeightKg = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckRouteStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TruckRouteStops_Trucks_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Trucks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TruckRouteStops_TruckId",
                table: "TruckRouteStops",
                column: "TruckId");
        }
    }
}
