using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class AddActorKindAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorKind",
                table: "AspNetUsers",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActorSkills",
                table: "AspNetUsers",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorKind",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ActorSkills",
                table: "AspNetUsers");
        }
    }
}
