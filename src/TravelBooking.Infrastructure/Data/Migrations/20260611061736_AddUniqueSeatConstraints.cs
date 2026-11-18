using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBooking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueSeatConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seat_holds_FlightId_SeatNumber_Status",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_bookings_FlightId_SeatNumber_Status",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_FlightId_SeatNumber",
                table: "seat_holds",
                columns: new[] { "FlightId", "SeatNumber" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_FlightId_SeatNumber",
                table: "bookings",
                columns: new[] { "FlightId", "SeatNumber" },
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seat_holds_FlightId_SeatNumber",
                table: "seat_holds");

            migrationBuilder.DropIndex(
                name: "IX_bookings_FlightId_SeatNumber",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_seat_holds_FlightId_SeatNumber_Status",
                table: "seat_holds",
                columns: new[] { "FlightId", "SeatNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_FlightId_SeatNumber_Status",
                table: "bookings",
                columns: new[] { "FlightId", "SeatNumber", "Status" });
        }
    }
}
