using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageGuestUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GuestUserId",
                table: "ParkingRequestMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequestMessages_ParkingRequestId_GuestUserId_CreatedAt",
                table: "ParkingRequestMessages",
                columns: new[] { "ParkingRequestId", "GuestUserId", "CreatedAt" });

            migrationBuilder.Sql("""
                UPDATE m
                SET m.GuestUserId = m.SenderUserId
                FROM ParkingRequestMessages m
                INNER JOIN ParkingRequests r ON r.Id = m.ParkingRequestId
                WHERE m.SenderUserId <> r.CreatedByUserId;

                UPDATE m
                SET m.GuestUserId = prev.GuestUserId
                FROM ParkingRequestMessages m
                INNER JOIN ParkingRequests r ON r.Id = m.ParkingRequestId
                CROSS APPLY (
                    SELECT TOP 1 m2.SenderUserId AS GuestUserId
                    FROM ParkingRequestMessages m2
                    WHERE m2.ParkingRequestId = m.ParkingRequestId
                      AND m2.SenderUserId <> r.CreatedByUserId
                      AND m2.CreatedAt <= m.CreatedAt
                    ORDER BY m2.CreatedAt DESC
                ) prev
                WHERE m.SenderUserId = r.CreatedByUserId
                  AND m.GuestUserId = '00000000-0000-0000-0000-000000000000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingRequestMessages_ParkingRequestId_GuestUserId_CreatedAt",
                table: "ParkingRequestMessages");

            migrationBuilder.DropColumn(
                name: "GuestUserId",
                table: "ParkingRequestMessages");
        }
    }
}
