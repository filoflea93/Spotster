using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestRenewalCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RenewalCount",
                table: "ParkingRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenewalCount",
                table: "ParkingRequests");
        }
    }
}
