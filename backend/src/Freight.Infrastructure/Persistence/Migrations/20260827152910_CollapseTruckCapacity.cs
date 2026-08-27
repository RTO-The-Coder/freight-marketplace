using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseTruckCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TruckType",
                table: "Trucks",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "TruckSize",
                table: "Trucks",
                newName: "Size");

            migrationBuilder.RenameColumn(
                name: "ActualPickupAt",
                table: "Shipments",
                newName: "EstimatedPickup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Trucks",
                newName: "TruckType");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "Trucks",
                newName: "TruckSize");

            migrationBuilder.RenameColumn(
                name: "EstimatedPickup",
                table: "Shipments",
                newName: "ActualPickupAt");
        }
    }
}
