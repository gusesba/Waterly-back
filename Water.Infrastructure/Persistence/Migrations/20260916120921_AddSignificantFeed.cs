using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSignificantFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Audience = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortKey = table.Column<string>(type: "character varying(46)", maxLength: 46, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedEvents_PrivateGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PrivateGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedEvents_ActorUserId_SortKey",
                table: "FeedEvents",
                columns: new[] { "ActorUserId", "SortKey" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedEvents_GroupId_SortKey",
                table: "FeedEvents",
                columns: new[] { "GroupId", "SortKey" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedEvents_IdempotencyKey",
                table: "FeedEvents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedEvents_SortKey",
                table: "FeedEvents",
                column: "SortKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedEvents");
        }
    }
}
