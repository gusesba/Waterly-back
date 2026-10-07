using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMvpAcquisitionMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AcceptanceCount",
                table: "GroupInvites",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PreviewCount",
                table: "GroupInvites",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "InputMethod",
                table: "DrinkEntries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCount",
                table: "GroupInvites");

            migrationBuilder.DropColumn(
                name: "PreviewCount",
                table: "GroupInvites");

            migrationBuilder.DropColumn(
                name: "InputMethod",
                table: "DrinkEntries");

            migrationBuilder.DropColumn(
                name: "RegisteredAt",
                table: "AspNetUsers");
        }
    }
}
