using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContestDailyScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EligibleFrom",
                table: "ContestParticipants",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContestDailyScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DailyTargetMl = table.Column<int>(type: "integer", nullable: false),
                    HydrationMl = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestDailyScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestDailyScores_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContestDailyScores_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestDailyScores_ContestId_UserId_LocalDate",
                table: "ContestDailyScores",
                columns: new[] { "ContestId", "UserId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestDailyScores_UserId",
                table: "ContestDailyScores",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestDailyScores");

            migrationBuilder.DropColumn(
                name: "EligibleFrom",
                table: "ContestParticipants");
        }
    }
}
