using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class ResourceAvailabilityTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvailabilityKind",
                table: "Resources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvailableDays",
                table: "Resources",
                type: "int",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFromDate",
                table: "Resources",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AvailableFromTime",
                table: "Resources",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableToDate",
                table: "Resources",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AvailableToTime",
                table: "Resources",
                type: "time(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailabilityKind",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AvailableDays",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AvailableFromDate",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AvailableFromTime",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AvailableToDate",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "AvailableToTime",
                table: "Resources");
        }
    }
}
