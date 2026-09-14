using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Water.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetBeverageHydrationFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Beverages",
                keyColumn: "Code",
                keyValue: "coffee",
                column: "HydrationFactor",
                value: 0.800m);

            migrationBuilder.UpdateData(
                table: "Beverages",
                keyColumn: "Code",
                keyValue: "tea",
                column: "HydrationFactor",
                value: 0.900m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Beverages",
                keyColumn: "Code",
                keyValue: "coffee",
                column: "HydrationFactor",
                value: 1.000m);

            migrationBuilder.UpdateData(
                table: "Beverages",
                keyColumn: "Code",
                keyValue: "tea",
                column: "HydrationFactor",
                value: 1.000m);
        }
    }
}
