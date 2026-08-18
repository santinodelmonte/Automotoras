using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// El dominio propio deja de ser una columna de <c>tenants</c> y pasa a su propia tabla.
    /// </summary>
    /// <remarks>
    /// Los dominios que ya estaban cargados se copian como verificados: los puso a mano
    /// alguien de la plataforma, que es exactamente la verificación que había hasta ahora.
    /// Marcarlos pendientes apagaría el sitio de automotoras que están funcionando.
    /// </remarks>
    public partial class DominiosDeTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dominios_tenant",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<int>(type: "int", nullable: false),
                    dominio = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    estado = table.Column<int>(type: "int", nullable: false),
                    token_de_verificacion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    es_principal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    verificado_en = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ultima_verificacion = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    verificaciones_fallidas = table.Column<int>(type: "int", nullable: false),
                    ultimo_error = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dominios_tenant", x => x.id);
                    table.ForeignKey(
                        name: "fk_dominios_tenant_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_dominios_tenant_dominio",
                table: "dominios_tenant",
                column: "dominio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dominios_tenant_tenant_id",
                table: "dominios_tenant",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dominios_tenant_estado_dominio",
                table: "dominios_tenant",
                columns: new[] { "estado", "dominio" });

            // Los dominios que ya estaban andando se llevan verificados y como principales.
            // El estado 2 es EstadoDeDominio.Verificado; va el número porque el SQL no
            // conoce el enum, y por eso los valores del enum no se reordenan nunca.
            migrationBuilder.Sql(
                """
                INSERT INTO dominios_tenant
                    (tenant_id, dominio, estado, token_de_verificacion, es_principal,
                     verificado_en, ultima_verificacion, verificaciones_fallidas, created_at)
                SELECT
                    id, dominio_custom, 2, CONCAT('migrado-', id), 1,
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 0, UTC_TIMESTAMP(6)
                FROM tenants
                WHERE dominio_custom IS NOT NULL AND dominio_custom <> '';
                """);

            migrationBuilder.DropIndex(
                name: "ix_tenants_dominio_custom",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "dominio_custom",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dominio_custom",
                table: "tenants",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Vuelve solo el principal: la columna que se recupera admite uno por tenant.
            migrationBuilder.Sql(
                """
                UPDATE tenants t
                JOIN dominios_tenant d ON d.tenant_id = t.id AND d.es_principal = 1
                SET t.dominio_custom = d.dominio;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_dominio_custom",
                table: "tenants",
                column: "dominio_custom",
                unique: true);

            migrationBuilder.DropTable(
                name: "dominios_tenant");
        }
    }
}
