namespace AutomotoraSaaS.Core.Enums;

// Los valores numéricos son explícitos y no se reordenan nunca: se persisten como int.
// Cambiar un número acá reinterpreta silenciosamente los datos ya guardados.

public enum RolUsuario
{
    SuperAdmin = 1,
    Owner = 2,
    Seller = 3,
}

public enum Carroceria
{
    Sedan = 1,
    Hatchback = 2,
    Suv = 3,
    Pickup = 4,
    Coupe = 5,
    Wagon = 6,
    Van = 7,
    Minivan = 8,
    Convertible = 9,
}

public enum Combustible
{
    Nafta = 1,
    Diesel = 2,
    Hibrido = 3,
    Electrico = 4,
    Gnc = 5,
}

public enum Transmision
{
    Manual = 1,
    Automatica = 2,
}

public enum Moneda
{
    Usd = 1,
    Uyu = 2,
}

public enum EstadoVehiculo
{
    Disponible = 1,
    Reservado = 2,
    Vendido = 3,
    Pausado = 4,
}

public enum TipoEvento
{
    ViewFicha = 1,
    ViewListado = 2,
    ClickWhatsapp = 3,
    ClickTelefono = 4,
    BusquedaSinResultado = 5,
}

public enum EstadoSolicitudModelo
{
    Pendiente = 1,
    Aprobada = 2,
    Rechazada = 3,
}
