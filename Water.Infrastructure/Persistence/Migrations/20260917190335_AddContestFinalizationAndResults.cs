using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContestFinalizationAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestFinalizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    ParticipantCount = table.Column<int>(type: "integer", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestFinalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestFinalizations_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    ScoredDays = table.Column<int>(type: "integer", nullable: false),
                    IsTied = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestResults_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContestResults_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestFinalizations_ContestId",
                table: "ContestFinalizations",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestResults_ContestId_Position_TotalScore",
                table: "ContestResults",
                columns: new[] { "ContestId", "Position", "TotalScore" });

            migrationBuilder.CreateIndex(
                name: "IX_ContestResults_ContestId_UserId",
                table: "ContestResults",
                columns: new[] { "ContestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestResults_UserId",
                table: "ContestResults",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestFinalizations");

            migrationBuilder.DropTable(
                name: "ContestResults");
        }
    }
}
