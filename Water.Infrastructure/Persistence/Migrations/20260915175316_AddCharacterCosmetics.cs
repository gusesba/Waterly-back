using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CosmeticItems",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiredAchievementCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CosmeticItems", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CosmeticItems_AchievementDefinitions_RequiredAchievementCode",
                        column: x => x.RequiredAchievementCode,
                        principalTable: "AchievementDefinitions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterLoadouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AuraCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterLoadouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterLoadouts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterLoadouts_CosmeticItems_AuraCode",
                        column: x => x.AuraCode,
                        principalTable: "CosmeticItems",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCosmetics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CosmeticCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCosmetics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCosmetics_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCosmetics_CosmeticItems_CosmeticCode",
                        column: x => x.CosmeticCode,
                        principalTable: "CosmeticItems",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CosmeticItems",
                columns: new[] { "Code", "RequiredAchievementCode", "SortOrder" },
                values: new object[,]
                {
                    { "natural", null, 1 },
                    { "ocean", "first-goal", 2 },
                    { "stellar", "streak-7", 4 },
                    { "sunset", "streak-3", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterLoadouts_AuraCode",
                table: "CharacterLoadouts",
                column: "AuraCode");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterLoadouts_UserId",
                table: "CharacterLoadouts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CosmeticItems_RequiredAchievementCode",
                table: "CosmeticItems",
                column: "RequiredAchievementCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserCosmetics_CosmeticCode",
                table: "UserCosmetics",
                column: "CosmeticCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserCosmetics_UserId_CosmeticCode",
                table: "UserCosmetics",
                columns: new[] { "UserId", "CosmeticCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterLoadouts");

            migrationBuilder.DropTable(
                name: "UserCosmetics");

            migrationBuilder.DropTable(
                name: "CosmeticItems");
        }
    }
}
