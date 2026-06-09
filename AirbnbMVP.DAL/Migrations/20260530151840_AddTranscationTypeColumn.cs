using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirbnbMVP.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscationTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "transactions");
        }
    }
}
