using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanesYSuscripciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    codigo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    precio_mensual = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    moneda = table.Column<int>(type: "int", nullable: false),
                    max_vehiculos = table.Column<int>(type: "int", nullable: true),
                    max_usuarios = table.Column<int>(type: "int", nullable: true),
                    incluye_reportes = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    incluye_benchmark = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    incluye_dominio_propio = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    horas_soporte_mes = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planes", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "suscripciones",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<int>(type: "int", nullable: false),
                    plan_id = table.Column<int>(type: "int", nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fin = table.Column<DateOnly>(type: "date", nullable: true),
                    paga_hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo_de_baja = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id_vigente = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN fin IS NULL THEN tenant_id END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suscripciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_suscripciones_planes_plan_id",
                        column: x => x.plan_id,
                        principalTable: "planes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_suscripciones_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pagos",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<int>(type: "int", nullable: false),
                    suscripcion_id = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    moneda = table.Column<int>(type: "int", nullable: false),
                    periodo_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    medio = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    comprobante = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nota = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagos_suscripciones_suscripcion_id",
                        column: x => x.suscripcion_id,
                        principalTable: "suscripciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pagos_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "planes",
                columns: new[] { "id", "activo", "codigo", "created_at", "horas_soporte_mes", "incluye_benchmark", "incluye_dominio_propio", "incluye_reportes", "max_usuarios", "max_vehiculos", "moneda", "nombre", "precio_mensual" },
                values: new object[,]
                {
                    { 1, true, "vidriera", new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Utc), 0, false, false, false, 2, 40, 2, "Vidriera", 4000m },
                    { 2, true, "demanda", new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Utc), 2, false, true, true, 6, 120, 2, "Demanda", 6900m },
                    { 3, true, "full", new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, true, true, null, null, 2, "Full", 9900m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_pagos_suscripcion_id",
                table: "pagos",
                column: "suscripcion_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagos_tenant_id_fecha",
                table: "pagos",
                columns: new[] { "tenant_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_planes_codigo",
                table: "planes",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suscripciones_plan_id",
                table: "suscripciones",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_suscripciones_tenant_id_inicio",
                table: "suscripciones",
                columns: new[] { "tenant_id", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_suscripciones_tenant_id_vigente",
                table: "suscripciones",
                column: "tenant_id_vigente",
                unique: true);

            // Ningún tenant queda sin suscripción. Las automotoras que ya existen pasan al
            // plan Full: hoy usan todo —reportes y benchmark incluidos— y el primer cambio
            // que vieran por esta migración no puede ser perder una pantalla. Arrancan con
            // los dos meses bonificados de la propuesta, registrados como un pago de monto
            // cero. Si alguna corresponde a otro plan, se cambia desde el panel.
            //
            // El SQL es el mismo para MySQL y MariaDB.
            migrationBuilder.Sql(
                """
                INSERT INTO suscripciones (tenant_id, plan_id, inicio, fin, paga_hasta, motivo_de_baja, created_at)
                SELECT t.id, p.id, CURDATE(), NULL,
                       DATE_SUB(DATE_ADD(CURDATE(), INTERVAL 2 MONTH), INTERVAL 1 DAY),
                       NULL, UTC_TIMESTAMP(6)
                FROM tenants t
                CROSS JOIN planes p
                WHERE p.codigo = 'full';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO pagos (tenant_id, suscripcion_id, fecha, monto, moneda, periodo_desde, periodo_hasta, medio, comprobante, nota, created_at)
                SELECT s.tenant_id, s.id, s.inicio, 0, p.moneda, s.inicio, s.paga_hasta,
                       'Bonificación', NULL,
                       '2 primeros meses sin costo (condición de lanzamiento).',
                       UTC_TIMESTAMP(6)
                FROM suscripciones s
                JOIN planes p ON p.id = s.plan_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagos");

            migrationBuilder.DropTable(
                name: "suscripciones");

            migrationBuilder.DropTable(
                name: "planes");
        }
    }
}
