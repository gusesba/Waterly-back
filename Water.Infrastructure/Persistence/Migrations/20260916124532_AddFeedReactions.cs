using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedReactions",
                columns: table => new
                {
                    FeedEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedReactions", x => new { x.FeedEventId, x.UserId });
                    table.ForeignKey(
                        name: "FK_FeedReactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedReactions_FeedEvents_FeedEventId",
                        column: x => x.FeedEventId,
                        principalTable: "FeedEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedReactions_FeedEventId_Type",
                table: "FeedReactions",
                columns: new[] { "FeedEventId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedReactions_UserId",
                table: "FeedReactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedReactions");
        }
    }
}
