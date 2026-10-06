using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pakar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAuthAndZoneModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualOverrideCount",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                table: "Cameras");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Zones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StreamUrl",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "StreamUrl",
                table: "Cameras");

            migrationBuilder.AddColumn<int>(
                name: "ManualOverrideCount",
                table: "Zones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Cameras",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                table: "Cameras",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
