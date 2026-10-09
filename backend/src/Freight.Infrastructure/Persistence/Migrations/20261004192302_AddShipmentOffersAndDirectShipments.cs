using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentOffersAndDirectShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirect",
                table: "Shipments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShipmentOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckingCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TruckId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickupInsertIndex = table.Column<int>(type: "integer", nullable: false),
                    DeliveryInsertIndex = table.Column<int>(type: "integer", nullable: false),
                    AddedDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    PriceEur = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    LimitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentOffers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOffers_ShipmentId_TruckId",
                table: "ShipmentOffers",
                columns: new[] { "ShipmentId", "TruckId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentOffers_TruckingCompanyId",
                table: "ShipmentOffers",
                column: "TruckingCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentOffers");

            migrationBuilder.DropColumn(
                name: "IsDirect",
                table: "Shipments");
        }
    }
}
