using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ResourceNeedItemId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AnswerBy = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Delivery = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfirmedByPhone = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceRequests_ResourceNeedItems_ResourceNeedItemId",
                        column: x => x.ResourceNeedItemId,
                        principalTable: "ResourceNeedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequests_ResourceId",
                table: "ResourceRequests",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRequests_ResourceNeedItemId",
                table: "ResourceRequests",
                column: "ResourceNeedItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceRequests");
        }
    }
}
