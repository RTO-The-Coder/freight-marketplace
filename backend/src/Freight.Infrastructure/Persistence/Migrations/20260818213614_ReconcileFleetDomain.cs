using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileFleetDomain : Migration
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
                name: "Trucks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckName = table.Column<string>(type: "text", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TruckType = table.Column<string>(type: "text", nullable: false),
                    TruckSize = table.Column<string>(type: "text", nullable: false),
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
                name: "TruckRouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ExpectedArrivalTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ShipmentLoadWeightKg = table.Column<double>(type: "double precision", nullable: true),
                    ShipmentLoadVolumeCubicMeters = table.Column<double>(type: "double precision", nullable: true),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "TruckRouteStops");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
