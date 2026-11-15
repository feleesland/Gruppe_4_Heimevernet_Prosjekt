using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimevernet_Prosjekt.Migrations
{
    /// <inheritdoc />
    public partial class NeedMeetingPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MeetingLatitude",
                table: "ResourceNeeds",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeetingLongitude",
                table: "ResourceNeeds",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingPointName",
                table: "ResourceNeeds",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeetingLatitude",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "MeetingLongitude",
                table: "ResourceNeeds");

            migrationBuilder.DropColumn(
                name: "MeetingPointName",
                table: "ResourceNeeds");
        }
    }
}
