using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AvisosDeCobro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "avisos_de_cobro",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<int>(type: "int", nullable: false),
                    suscripcion_id = table.Column<int>(type: "int", nullable: false),
                    estado = table.Column<int>(type: "int", nullable: false),
                    paga_hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    destinatarios = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos_de_cobro", x => x.id);
                    table.ForeignKey(
                        name: "fk_avisos_de_cobro_suscripciones_suscripcion_id",
                        column: x => x.suscripcion_id,
                        principalTable: "suscripciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_avisos_de_cobro_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_de_cobro_suscripcion_id_paga_hasta_estado",
                table: "avisos_de_cobro",
                columns: new[] { "suscripcion_id", "paga_hasta", "estado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_avisos_de_cobro_tenant_id",
                table: "avisos_de_cobro",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avisos_de_cobro");
        }
    }
}
