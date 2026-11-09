using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceUnavailable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnavailableReason",
                table: "Resources",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnavailableUntil",
                table: "Resources",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnavailableReason",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "UnavailableUntil",
                table: "Resources");
        }
    }
}
