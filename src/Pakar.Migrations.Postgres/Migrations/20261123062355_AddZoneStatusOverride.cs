using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pakar.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneStatusOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusOverride",
                table: "Zones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusOverrideNote",
                table: "Zones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusOverrideSetAt",
                table: "Zones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusOverrideSetBy",
                table: "Zones",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusOverride",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "StatusOverrideNote",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "StatusOverrideSetAt",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "StatusOverrideSetBy",
                table: "Zones");
        }
    }
}
