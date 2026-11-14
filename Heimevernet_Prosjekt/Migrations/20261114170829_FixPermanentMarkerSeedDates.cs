using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class FixPermanentMarkerSeedDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(4921));

            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(6351));

            migrationBuilder.UpdateData(
                table: "PermanentMapMarkers",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(6353));
        }
    }
}
