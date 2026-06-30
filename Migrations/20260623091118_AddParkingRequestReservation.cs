using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingRequestReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedAt",
                table: "ParkingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservedByUserId",
                table: "ParkingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequests_ReservedByUserId",
                table: "ParkingRequests",
                column: "ReservedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingRequests_Users_ReservedByUserId",
                table: "ParkingRequests",
                column: "ReservedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingRequests_Users_ReservedByUserId",
                table: "ParkingRequests");

            migrationBuilder.DropIndex(
                name: "IX_ParkingRequests_ReservedByUserId",
                table: "ParkingRequests");

            migrationBuilder.DropColumn(
                name: "ReservedAt",
                table: "ParkingRequests");

            migrationBuilder.DropColumn(
                name: "ReservedByUserId",
                table: "ParkingRequests");
        }
    }
}
