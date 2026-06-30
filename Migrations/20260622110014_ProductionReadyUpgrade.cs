using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class ProductionReadyUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccuracyRate",
                table: "Users",
                type: "float(5)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "DeviceFingerprintHash",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FalseReports",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDailyBonusAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastIpHash",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLatitude",
                table: "Users",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLongitude",
                table: "Users",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NegativeReports",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositiveReports",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedUntil",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspiciousScore",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedReports",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VotesCorrect",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ReportVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceScore",
                table: "ParkingReports",
                type: "float(5)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "ParkingReports",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ReportCount",
                table: "ParkingReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VirtualZoneKey",
                table: "ParkingReports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReputationScore",
                table: "Users",
                column: "ReputationScore");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                table: "Users",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReports_Status_ExpiresAt",
                table: "ParkingReports",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReports_VirtualZoneKey",
                table: "ParkingReports",
                column: "VirtualZoneKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ReputationScore",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Status",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ParkingReports_Status_ExpiresAt",
                table: "ParkingReports");

            migrationBuilder.DropIndex(
                name: "IX_ParkingReports_VirtualZoneKey",
                table: "ParkingReports");

            migrationBuilder.DropColumn(
                name: "AccuracyRate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeviceFingerprintHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FalseReports",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastDailyBonusAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastIpHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLongitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NegativeReports",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PositiveReports",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspiciousScore",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerifiedReports",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VotesCorrect",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ReportVotes");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "ParkingReports");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "ParkingReports");

            migrationBuilder.DropColumn(
                name: "ReportCount",
                table: "ParkingReports");

            migrationBuilder.DropColumn(
                name: "VirtualZoneKey",
                table: "ParkingReports");
        }
    }
}
