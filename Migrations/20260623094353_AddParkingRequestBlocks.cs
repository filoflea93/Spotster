using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingRequestBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParkingRequestBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingRequestBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingRequestBlocks_ParkingRequests_ParkingRequestId",
                        column: x => x.ParkingRequestId,
                        principalTable: "ParkingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParkingRequestBlocks_Users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequestBlocks_GuestUserId",
                table: "ParkingRequestBlocks",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequestBlocks_ParkingRequestId_GuestUserId",
                table: "ParkingRequestBlocks",
                columns: new[] { "ParkingRequestId", "GuestUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParkingRequestBlocks");
        }
    }
}
