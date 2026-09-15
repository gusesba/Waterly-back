using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AchievementDefinitions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Criterion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Requirement = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementDefinitions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AchievementCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AchievementDefinitions_AchievementCode",
                        column: x => x.AchievementCode,
                        principalTable: "AchievementDefinitions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AchievementDefinitions",
                columns: new[] { "Code", "Criterion", "Requirement", "RuleVersion", "SortOrder" },
                values: new object[,]
                {
                    { "first-goal", "completed-days", 1, 1, 1 },
                    { "streak-3", "longest-streak", 3, 1, 2 },
                    { "streak-7", "longest-streak", 7, 1, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementCode",
                table: "UserAchievements",
                column: "AchievementCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementCode",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "AchievementDefinitions");
        }
    }
}
