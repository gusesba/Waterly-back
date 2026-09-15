using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressionLedgers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DropsReward",
                table: "AchievementDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrestigeReward",
                table: "AchievementDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DropsLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DropsLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DropsLedgerEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrestigeLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrestigeLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrestigeLedgerEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AchievementDefinitions",
                keyColumn: "Code",
                keyValue: "first-goal",
                columns: new[] { "DropsReward", "PrestigeReward" },
                values: new object[] { 25, 10 });

            migrationBuilder.UpdateData(
                table: "AchievementDefinitions",
                keyColumn: "Code",
                keyValue: "streak-3",
                columns: new[] { "DropsReward", "PrestigeReward" },
                values: new object[] { 50, 25 });

            migrationBuilder.UpdateData(
                table: "AchievementDefinitions",
                keyColumn: "Code",
                keyValue: "streak-7",
                columns: new[] { "DropsReward", "PrestigeReward" },
                values: new object[] { 100, 50 });

            migrationBuilder.CreateIndex(
                name: "IX_DropsLedgerEntries_UserId_IdempotencyKey",
                table: "DropsLedgerEntries",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrestigeLedgerEntries_UserId_IdempotencyKey",
                table: "PrestigeLedgerEntries",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DropsLedgerEntries");

            migrationBuilder.DropTable(
                name: "PrestigeLedgerEntries");

            migrationBuilder.DropColumn(
                name: "DropsReward",
                table: "AchievementDefinitions");

            migrationBuilder.DropColumn(
                name: "PrestigeReward",
                table: "AchievementDefinitions");
        }
    }
}
