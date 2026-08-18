using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreciosDeReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "precios_referencia",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    modelo_id = table.Column<int>(type: "int", nullable: false),
                    anio = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    moneda = table.Column<int>(type: "int", nullable: false),
                    promedio = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    minimo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    maximo = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    muestras = table.Column<int>(type: "int", nullable: false),
                    fuente = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precios_referencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_precios_referencia_modelos_modelo_id",
                        column: x => x.modelo_id,
                        principalTable: "modelos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_precios_referencia_modelo_id_anio_fecha",
                table: "precios_referencia",
                columns: new[] { "modelo_id", "anio", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_precios_referencia_modelo_id_anio_moneda_fecha_fuente",
                table: "precios_referencia",
                columns: new[] { "modelo_id", "anio", "moneda", "fecha", "fuente" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "precios_referencia");
        }
    }
}
