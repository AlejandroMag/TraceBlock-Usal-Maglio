using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrazeaBlock.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnicidadPagosYCuentasMp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrgMercadoPagos_MpUserId",
                table: "OrgMercadoPagos",
                column: "MpUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donaciones_PaymentId",
                table: "Donaciones",
                column: "PaymentId",
                unique: true,
                filter: "\"PaymentId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrgMercadoPagos_MpUserId",
                table: "OrgMercadoPagos");

            migrationBuilder.DropIndex(
                name: "IX_Donaciones_PaymentId",
                table: "Donaciones");
        }
    }
}
