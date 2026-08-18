using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreciosDeMercado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "precios_de_mercado",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    modelo_id = table.Column<int>(type: "int", nullable: false),
                    anio = table.Column<int>(type: "int", nullable: false),
                    moneda = table.Column<int>(type: "int", nullable: false),
                    precio_mediano = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_minimo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_maximo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    publicaciones = table.Column<int>(type: "int", nullable: false),
                    fuente = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precios_de_mercado", x => x.id);
                    table.ForeignKey(
                        name: "fk_precios_de_mercado_modelos_modelo_id",
                        column: x => x.modelo_id,
                        principalTable: "modelos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_precios_de_mercado_fuente_modelo_id_anio_fecha",
                table: "precios_de_mercado",
                columns: new[] { "fuente", "modelo_id", "anio", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_precios_de_mercado_modelo_id_anio_fecha",
                table: "precios_de_mercado",
                columns: new[] { "modelo_id", "anio", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "precios_de_mercado");
        }
    }
}
