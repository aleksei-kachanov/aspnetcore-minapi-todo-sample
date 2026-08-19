using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMinRouteGroup.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Todos_Title",
                table: "Todos",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Todos_Title",
                table: "Todos");
        }
    }
}
