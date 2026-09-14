using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBeverageCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BeverageCode",
                table: "DrinkEntries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "water");

            migrationBuilder.AddColumn<int>(
                name: "HydrationMl",
                table: "DrinkEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Beverages",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HydrationFactor = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beverages", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "Beverages",
                columns: new[] { "Code", "HydrationFactor", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { "coffee", 1.000m, true, 3 },
                    { "sparkling-water", 1.000m, true, 2 },
                    { "tea", 1.000m, true, 4 },
                    { "water", 1.000m, true, 1 }
                });

            migrationBuilder.Sql("UPDATE \"DrinkEntries\" SET \"HydrationMl\" = \"VolumeMl\"");

            migrationBuilder.CreateIndex(
                name: "IX_DrinkEntries_BeverageCode",
                table: "DrinkEntries",
                column: "BeverageCode");

            migrationBuilder.AddForeignKey(
                name: "FK_DrinkEntries_Beverages_BeverageCode",
                table: "DrinkEntries",
                column: "BeverageCode",
                principalTable: "Beverages",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DrinkEntries_Beverages_BeverageCode",
                table: "DrinkEntries");

            migrationBuilder.DropTable(
                name: "Beverages");

            migrationBuilder.DropIndex(
                name: "IX_DrinkEntries_BeverageCode",
                table: "DrinkEntries");

            migrationBuilder.DropColumn(
                name: "BeverageCode",
                table: "DrinkEntries");

            migrationBuilder.DropColumn(
                name: "HydrationMl",
                table: "DrinkEntries");
        }
    }
}
