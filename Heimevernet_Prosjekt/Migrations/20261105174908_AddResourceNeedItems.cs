using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceNeedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ResourceNeeds",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NeededFrom",
                table: "ResourceNeeds",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NeededTo",
                table: "ResourceNeeds",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RadiusKm",
                table: "ResourceNeeds",
                type: "double",
                nullable: false,
                defaultValue: 2.0);

            migrationBuilder.AddColumn<int>(
                name: "Urgency",
                table: "ResourceNeeds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ResourceNeedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ResourceNeedId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceNeedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceNeedItems_ResourceNeeds_ResourceNeedId",
                        column: x => x.ResourceNeedId,
                        principalTable: "ResourceNeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceNeedItems_ResourceNeedId",
                table: "ResourceNeedItems",
                column: "ResourceNeedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceNeedItems");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "NeededFrom",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "NeededTo",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "RadiusKm",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "Urgency",
                table: "ResourceNeeds");
        }
    }
}
