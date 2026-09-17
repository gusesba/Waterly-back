using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContestRewardsAndMedals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardRuleVersion",
                table: "Contests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ContestRewardCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestRewardCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestRewardCheckpoints_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestRewardDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DropsReward = table.Column<int>(type: "integer", nullable: false),
                    PrestigeReward = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestRewardDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedalDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedalDefinitions_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMedals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedalDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMedals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMedals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMedals_MedalDefinitions_MedalDefinitionId",
                        column: x => x.MedalDefinitionId,
                        principalTable: "MedalDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ContestRewardDefinitions",
                columns: new[] { "Id", "DropsReward", "DurationDays", "Position", "PrestigeReward", "RuleVersion" },
                values: new object[,]
                {
                    { 1, 25, 7, 0, 10, 1 },
                    { 2, 100, 7, 1, 50, 1 },
                    { 3, 60, 7, 2, 30, 1 },
                    { 4, 40, 7, 3, 20, 1 },
                    { 5, 75, 30, 0, 30, 1 },
                    { 6, 100, 30, 1, 50, 1 },
                    { 7, 60, 30, 2, 30, 1 },
                    { 8, 40, 30, 3, 20, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestRewardCheckpoints_ContestId",
                table: "ContestRewardCheckpoints",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestRewardDefinitions_RuleVersion_DurationDays_Position",
                table: "ContestRewardDefinitions",
                columns: new[] { "RuleVersion", "DurationDays", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedalDefinitions_Code",
                table: "MedalDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedalDefinitions_ContestId",
                table: "MedalDefinitions",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMedals_MedalDefinitionId",
                table: "UserMedals",
                column: "MedalDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMedals_UserId_MedalDefinitionId",
                table: "UserMedals",
                columns: new[] { "UserId", "MedalDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestRewardCheckpoints");

            migrationBuilder.DropTable(
                name: "ContestRewardDefinitions");

            migrationBuilder.DropTable(
                name: "UserMedals");

            migrationBuilder.DropTable(
                name: "MedalDefinitions");

            migrationBuilder.DropColumn(
                name: "RewardRuleVersion",
                table: "Contests");
        }
    }
}
