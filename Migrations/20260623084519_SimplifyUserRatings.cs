using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyUserRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserReviews_ParkingRequests_ParkingRequestId",
                table: "UserReviews");

            migrationBuilder.DropIndex(
                name: "IX_UserReviews_ReviewerUserId_ReviewedUserId_ParkingRequestId",
                table: "UserReviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParkingRequestId",
                table: "UserReviews",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_UserReviews_ReviewerUserId_ReviewedUserId",
                table: "UserReviews",
                columns: new[] { "ReviewerUserId", "ReviewedUserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReviews_ParkingRequests_ParkingRequestId",
                table: "UserReviews",
                column: "ParkingRequestId",
                principalTable: "ParkingRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserReviews_ParkingRequests_ParkingRequestId",
                table: "UserReviews");

            migrationBuilder.DropIndex(
                name: "IX_UserReviews_ReviewerUserId_ReviewedUserId",
                table: "UserReviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParkingRequestId",
                table: "UserReviews",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserReviews_ReviewerUserId_ReviewedUserId_ParkingRequestId",
                table: "UserReviews",
                columns: new[] { "ReviewerUserId", "ReviewedUserId", "ParkingRequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReviews_ParkingRequests_ParkingRequestId",
                table: "UserReviews",
                column: "ParkingRequestId",
                principalTable: "ParkingRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
