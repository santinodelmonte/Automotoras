using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutomotoraSaaS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// El catálogo base de marcas y modelos, para que exista también en producción.
    /// </summary>
    /// <remarks>
    /// Hasta acá el catálogo solo lo sembraba el seed de desarrollo, así que una base de
    /// producción arrancaba sin una sola marca y el primer dueño no podía cargar un auto.
    /// <para>
    /// Es idempotente a propósito: inserta solo lo que falta, comparando por nombre, que es
    /// lo que ya tiene índice único. Una base que viene del seed de desarrollo, o una donde
    /// el SuperAdmin ya cargó marcas a mano, queda con lo suyo más lo nuevo, y nada se
    /// duplica ni cambia de id.
    /// </para>
    /// <para>
    /// Los datos van escritos acá y no leídos de una clase compartida: una migración tiene
    /// que hacer siempre lo mismo, y si leyera una lista que después se edita, dos bases
    /// migradas en distinto momento terminarían con catálogos distintos.
    /// </para>
    /// </remarks>
    public partial class CatalogoDeProduccion : Migration
    {
        // Mismo orden que Core.Enums.Carroceria; repetido acá por lo mismo que los datos.
        private const int Sedan = 1, Hatchback = 2, Suv = 3, Pickup = 4, Wagon = 6, Van = 7, Minivan = 8;

        private static readonly (string Marca, (string Modelo, int Carroceria)[] Modelos)[] Catalogo =
        [
            ("Audi", [("A1", Hatchback), ("A3", Sedan), ("A4", Sedan), ("Q2", Suv), ("Q3", Suv), ("Q5", Suv)]),
            ("BMW", [("Serie 1", Hatchback), ("Serie 3", Sedan), ("X1", Suv), ("X3", Suv), ("X5", Suv)]),
            ("BYD", [("Dolphin", Hatchback), ("Dolphin Mini", Hatchback), ("Seal", Sedan), ("Song Plus", Suv), ("Song Pro", Suv), ("Yuan Plus", Suv), ("Yuan Pro", Suv), ("Shark", Pickup)]),
            ("Changan", [("CS15", Suv), ("CS35 Plus", Suv), ("CS55 Plus", Suv), ("Alsvin", Sedan), ("Hunter", Pickup)]),
            ("Chery", [("QQ", Hatchback), ("Arrizo 5", Sedan), ("Tiggo 2", Suv), ("Tiggo 3", Suv), ("Tiggo 4", Suv), ("Tiggo 7", Suv), ("Tiggo 8", Suv)]),
            ("Chevrolet", [("Agile", Hatchback), ("Celta", Hatchback), ("Classic", Sedan), ("Prisma", Sedan), ("Joy", Hatchback), ("Onix", Hatchback), ("Onix Plus", Sedan), ("Cruze", Sedan), ("Tracker", Suv), ("Equinox", Suv), ("Captiva", Suv), ("Spin", Minivan), ("Montana", Pickup), ("S10", Pickup)]),
            ("Citroën", [("C3", Hatchback), ("C4", Hatchback), ("C-Elysée", Sedan), ("C3 Aircross", Suv), ("C4 Cactus", Suv), ("C5 Aircross", Suv), ("Basalt", Suv), ("Berlingo", Van), ("Jumpy", Van)]),
            ("DFSK", [("Glory 500", Suv), ("Glory 580", Suv), ("C31", Pickup), ("C35", Van)]),
            ("Fiat", [("Uno", Hatchback), ("Mobi", Hatchback), ("Palio", Hatchback), ("Argo", Hatchback), ("Siena", Sedan), ("Grand Siena", Sedan), ("Cronos", Sedan), ("Pulse", Suv), ("Fastback", Suv), ("Fiorino", Van), ("Strada", Pickup), ("Toro", Pickup)]),
            ("Ford", [("Ka", Hatchback), ("Fiesta", Hatchback), ("Focus", Hatchback), ("EcoSport", Suv), ("Territory", Suv), ("Bronco Sport", Suv), ("Kuga", Suv), ("Ranger", Pickup), ("Maverick", Pickup), ("Transit", Van)]),
            ("Geely", [("Emgrand", Sedan), ("Coolray", Suv), ("Geometry C", Hatchback)]),
            ("Great Wall", [("Wingle 5", Pickup), ("Wingle 7", Pickup), ("Poer", Pickup)]),
            ("Haval", [("Jolion", Suv), ("H6", Suv)]),
            ("Honda", [("Fit", Hatchback), ("City", Sedan), ("Civic", Sedan), ("WR-V", Suv), ("HR-V", Suv), ("CR-V", Suv)]),
            ("Hyundai", [("i10", Hatchback), ("Grand i10", Hatchback), ("HB20", Hatchback), ("Accent", Sedan), ("Elantra", Sedan), ("Venue", Suv), ("Creta", Suv), ("Kona", Suv), ("Tucson", Suv), ("Santa Fe", Suv), ("H1", Van)]),
            ("JAC", [("S2", Suv), ("JS4", Suv), ("S3", Suv), ("T6", Pickup), ("T8", Pickup)]),
            ("Jeep", [("Renegade", Suv), ("Compass", Suv), ("Commander", Suv), ("Wrangler", Suv), ("Grand Cherokee", Suv)]),
            ("Kia", [("Picanto", Hatchback), ("Rio", Sedan), ("Soluto", Sedan), ("Cerato", Sedan), ("Stonic", Suv), ("Seltos", Suv), ("Sportage", Suv), ("Sorento", Suv), ("Carnival", Minivan), ("K2500", Pickup)]),
            ("Mazda", [("Mazda2", Hatchback), ("Mazda3", Sedan), ("CX-3", Suv), ("CX-30", Suv), ("CX-5", Suv), ("BT-50", Pickup)]),
            ("Mercedes-Benz", [("Clase A", Hatchback), ("Clase C", Sedan), ("GLA", Suv), ("GLC", Suv), ("Sprinter", Van)]),
            ("MG", [("MG3", Hatchback), ("MG4", Hatchback), ("ZS", Suv), ("HS", Suv)]),
            ("Mitsubishi", [("ASX", Suv), ("Outlander", Suv), ("Montero", Suv), ("L200", Pickup)]),
            ("Nissan", [("March", Hatchback), ("Note", Hatchback), ("Tiida", Hatchback), ("Versa", Sedan), ("Sentra", Sedan), ("Kicks", Suv), ("X-Trail", Suv), ("Frontier", Pickup)]),
            ("Peugeot", [("207", Hatchback), ("208", Hatchback), ("308", Hatchback), ("301", Sedan), ("408", Sedan), ("2008", Suv), ("3008", Suv), ("5008", Suv), ("Partner", Van), ("Expert", Van), ("Landtrek", Pickup)]),
            ("RAM", [("700", Pickup), ("1500", Pickup)]),
            ("Renault", [("Kwid", Hatchback), ("Clio", Hatchback), ("Sandero", Hatchback), ("Stepway", Hatchback), ("Logan", Sedan), ("Fluence", Sedan), ("Captur", Suv), ("Duster", Suv), ("Kardian", Suv), ("Koleos", Suv), ("Oroch", Pickup), ("Kangoo", Van), ("Master", Van)]),
            ("Subaru", [("Impreza", Hatchback), ("XV", Suv), ("Forester", Suv), ("Outback", Suv)]),
            ("Suzuki", [("Alto", Hatchback), ("Celerio", Hatchback), ("Swift", Hatchback), ("Baleno", Hatchback), ("Ignis", Hatchback), ("Dzire", Sedan), ("Fronx", Suv), ("Vitara", Suv), ("Grand Vitara", Suv), ("S-Cross", Suv), ("Jimny", Suv), ("Ertiga", Minivan)]),
            ("Toyota", [("Etios", Hatchback), ("Yaris", Hatchback), ("Corolla", Sedan), ("Yaris Cross", Suv), ("Corolla Cross", Suv), ("RAV4", Suv), ("SW4", Suv), ("Hilux", Pickup), ("Hiace", Van)]),
            ("Volkswagen", [("Up", Hatchback), ("Fox", Hatchback), ("Gol", Hatchback), ("Polo", Hatchback), ("Golf", Hatchback), ("Voyage", Sedan), ("Virtus", Sedan), ("Vento", Sedan), ("Suran", Wagon), ("Nivus", Suv), ("T-Cross", Suv), ("Taos", Suv), ("Tiguan", Suv), ("Saveiro", Pickup), ("Amarok", Pickup)]),
            ("Volvo", [("XC40", Suv), ("XC60", Suv), ("XC90", Suv)]),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = new StringBuilder();

            foreach (var (marca, modelos) in Catalogo)
            {
                sql.AppendLine(
                    $"INSERT INTO marcas (nombre, activo) SELECT '{marca}', 1 FROM DUAL " +
                    $"WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = '{marca}');");

                foreach (var (modelo, carroceria) in modelos)
                {
                    sql.AppendLine(
                        $"INSERT INTO modelos (marca_id, nombre, carroceria, activo) " +
                        $"SELECT m.id, '{modelo}', {carroceria}, 1 FROM marcas m " +
                        $"WHERE m.nombre = '{marca}' " +
                        $"AND NOT EXISTS (SELECT 1 FROM modelos o WHERE o.marca_id = m.id AND o.nombre = '{modelo}');");
                }
            }

            migrationBuilder.Sql(sql.ToString());
        }

        /// <summary>
        /// No borra nada. Para cuando alguien vuelva atrás esta migración ya habrá vehículos
        /// apuntando a estos modelos, y la foreign key no deja borrarlos; separar lo que puso
        /// esta migración de lo que cargó el SuperAdmin después tampoco es posible.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
