using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerificacionDeDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "dominio_verificado_en",
                table: "tenants",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dominio_verificado_en",
                table: "tenants");
        }
    }
}
