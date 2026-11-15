using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pakar.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddReportReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Reports",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "Reports",
                type: "uuid",
                nullable: true);

            // "xmin" is a Postgres system column used as the concurrency token; it already exists on every table.

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewedById",
                table: "Reports",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_SubmittedAt",
                table: "Reports",
                columns: new[] { "Status", "SubmittedAt" });

            // Reports created before the status enum used "Pending".
            migrationBuilder.Sql("""UPDATE "Reports" SET "Status" = 'PendingReview' WHERE "Status" = 'Pending';""");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_ReviewedById",
                table: "Reports",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Reports" SET "Status" = 'Pending' WHERE "Status" = 'PendingReview';""");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_ReviewedById",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReviewedById",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Status_SubmittedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "Reports");
        }
    }
}
