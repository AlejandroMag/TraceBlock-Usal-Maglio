using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrazeaBlock.Data.Migrations
{
    /// <inheritdoc />
    public partial class Blockchain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoBlockchain",
                table: "Evidencias",
                type: "text",
                nullable: false,
                defaultValue: "RegistradoBlockchain");

            migrationBuilder.AddColumn<string>(
                name: "RedBlockchain",
                table: "Evidencias",
                type: "text",
                nullable: false,
                defaultValue: "mock");

            migrationBuilder.AddColumn<string>(
                name: "RedBlockchain",
                table: "Donaciones",
                type: "text",
                nullable: false,
                defaultValue: "mock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoBlockchain",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "RedBlockchain",
                table: "Evidencias");

            migrationBuilder.DropColumn(
                name: "RedBlockchain",
                table: "Donaciones");
        }
    }
}
