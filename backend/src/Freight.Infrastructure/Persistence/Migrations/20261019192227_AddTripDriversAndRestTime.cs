using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDriversAndRestTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestTimeSoFar",
                table: "Trips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TripDrivers",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripDrivers", x => new { x.TripId, x.DriverId });
                    table.ForeignKey(
                        name: "FK_TripDrivers_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TruckingCompanyId_CompletedAt",
                table: "Trips",
                columns: new[] { "TruckingCompanyId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripDrivers");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TruckingCompanyId_CompletedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RestTimeSoFar",
                table: "Trips");
        }
    }
}
