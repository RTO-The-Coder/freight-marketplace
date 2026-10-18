using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceNamesAndShipperAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficePlaceName",
                table: "TruckingCompanies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPlaceName",
                table: "Shipments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PickupPlaceName",
                table: "Shipments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ShipperAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AreaName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipperAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipperAddresses_Shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "Shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipperAddresses_ShipperId",
                table: "ShipperAddresses",
                column: "ShipperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipperAddresses");

            migrationBuilder.DropColumn(
                name: "OfficePlaceName",
                table: "TruckingCompanies");

            migrationBuilder.DropColumn(
                name: "DeliveryPlaceName",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "PickupPlaceName",
                table: "Shipments");
        }
    }
}
