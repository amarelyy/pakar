using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pakar.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSourceRoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Cameras",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SpotRois",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpotId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotRois", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpotRois_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpotRois_Spots_SpotId",
                        column: x => x.SpotId,
                        principalTable: "Spots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpotRois_CameraId_SpotId",
                table: "SpotRois",
                columns: new[] { "CameraId", "SpotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpotRois_SpotId",
                table: "SpotRois",
                column: "SpotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpotRois");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Cameras");
        }
    }
}
