using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TrazeaBlock.Data.Migrations
{
    /// <inheritdoc />
    public partial class PadronOng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashPdfCenoc",
                table: "Organizaciones");

            migrationBuilder.DropColumn(
                name: "UrlPdfCenoc",
                table: "Organizaciones");

            migrationBuilder.CreateTable(
                name: "PadronOngs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cuit = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    TipoSocietario = table.Column<string>(type: "text", nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: false),
                    Localidad = table.Column<string>(type: "text", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PadronOngs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PadronOngs_Cuit",
                table: "PadronOngs",
                column: "Cuit",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PadronOngs");

            migrationBuilder.AddColumn<string>(
                name: "HashPdfCenoc",
                table: "Organizaciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlPdfCenoc",
                table: "Organizaciones",
                type: "text",
                nullable: true);
        }
    }
}
