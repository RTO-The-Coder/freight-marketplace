using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    ActualPickupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipperId",
                table: "Shipments",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TruckingCompanyId",
                table: "Shipments",
                column: "TruckingCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Shipments");
        }
    }
}
