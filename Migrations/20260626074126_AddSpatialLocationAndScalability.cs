using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Spotster.Migrations
{
    /// <inheritdoc />
    public partial class AddSpatialLocationAndScalability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "ParkingRequests",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "ParkingReports",
                type: "geography",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE ParkingReports
                SET Location = geography::Point(Longitude, Latitude, 4326)
                WHERE Location IS NULL;

                UPDATE ParkingRequests
                SET Location = geography::Point(Longitude, Latitude, 4326)
                WHERE Location IS NULL;
                """);

            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "ParkingRequests",
                type: "geography",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography",
                oldNullable: true);

            migrationBuilder.AlterColumn<Point>(
                name: "Location",
                table: "ParkingReports",
                type: "geography",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography",
                oldNullable: true);

            migrationBuilder.Sql("""
                CREATE SPATIAL INDEX SIX_ParkingReports_Location
                ON ParkingReports(Location)
                USING GEOGRAPHY_GRID
                WITH (GRIDS = (LEVEL_1 = MEDIUM, LEVEL_2 = MEDIUM, LEVEL_3 = MEDIUM, LEVEL_4 = MEDIUM));

                CREATE SPATIAL INDEX SIX_ParkingRequests_Location
                ON ParkingRequests(Location)
                USING GEOGRAPHY_GRID
                WITH (GRIDS = (LEVEL_1 = MEDIUM, LEVEL_2 = MEDIUM, LEVEL_3 = MEDIUM, LEVEL_4 = MEDIUM));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_ParkingRequests_Location' AND object_id = OBJECT_ID('ParkingRequests'))
                    DROP INDEX SIX_ParkingRequests_Location ON ParkingRequests;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'SIX_ParkingReports_Location' AND object_id = OBJECT_ID('ParkingReports'))
                    DROP INDEX SIX_ParkingReports_Location ON ParkingReports;
                """);

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ParkingRequests");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ParkingReports");
        }
    }
}
