using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deja de guardar la IP cifrada, el navegador y la página de origen de cada visita, y
    /// borra lo que ya había.
    /// </summary>
    /// <remarks>
    /// Ningún reporte los usaba. Eran datos personales sin finalidad, y sacarlos deja el
    /// registro de visitas anónimo: qué se miró y cuándo, sin nada de quién. El
    /// <c>Down</c> recrea las columnas vacías; lo borrado no vuelve.
    /// </remarks>
    public partial class QuitarDatosDeVisita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ip_hash",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "referer",
                table: "eventos");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "eventos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ip_hash",
                table: "eventos",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "referer",
                table: "eventos",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "eventos",
                type: "varchar(400)",
                maxLength: 400,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
