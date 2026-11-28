using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pakar.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneMapArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapAreaJson",
                table: "Zones",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapAreaJson",
                table: "Zones");
        }
    }
}
