using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentMapMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermanentMapMarkers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitude = table.Column<double>(type: "double", nullable: false),
                    Longitude = table.Column<double>(type: "double", nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermanentMapMarkers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "PermanentMapMarkers",
                columns: new[] { "Id", "CreatedAt", "Description", "IconType", "Latitude", "Longitude", "Name", "Type" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(4921), "Politistasjon", "police", 58.146000000000001, 7.9960000000000004, "Kristiansand Politistasjon", "Politi" },
                    { 2, new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(6351), "Brannstasjon", "firestation", 58.168999999999997, 8.0190000000000001, "Kristiansand Brannstasjon", "Brann" },
                    { 3, new DateTime(2026, 11, 12, 20, 18, 56, 486, DateTimeKind.Utc).AddTicks(6353), "Akuttsykehus", "hospital", 58.155999999999999, 7.9779999999999998, "Sørlandet Sykehus Kristiansand", "Sykehus" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermanentMapMarkers");
        }
    }
}
