using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestMessagingAndReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ParkingRequests",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodsJson",
                table: "ParkingRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RewardAmount",
                table: "ParkingRequests",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParkingRequestMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParkingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingRequestMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingRequestMessages_ParkingRequests_ParkingRequestId",
                        column: x => x.ParkingRequestId,
                        principalTable: "ParkingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParkingRequestMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequestMessages_ParkingRequestId_CreatedAt",
                table: "ParkingRequestMessages",
                columns: new[] { "ParkingRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequestMessages_SenderUserId",
                table: "ParkingRequestMessages",
                column: "SenderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParkingRequestMessages");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ParkingRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethodsJson",
                table: "ParkingRequests");

            migrationBuilder.DropColumn(
                name: "RewardAmount",
                table: "ParkingRequests");
        }
    }
}
