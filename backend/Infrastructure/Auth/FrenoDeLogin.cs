using System.Collections.Concurrent;

namespace AutomotoraSaaS.Infrastructure.Auth;

/// <summary>
/// Cuenta los logins fallidos por cuenta y frena la que acumula demasiados.
/// </summary>
/// <remarks>
/// Es el complemento del límite por IP: ese frena a quien prueba muchas cuentas desde un
/// lugar, este a quien prueba muchas contraseñas contra una cuenta desde muchos lugares.
/// El freno se mira antes de verificar la contraseña, así que una cuenta frenada tampoco
/// le cuesta al servidor las 210.000 iteraciones de cada intento.
/// <para>
/// Vive en memoria y no en la base a propósito: si el app pool recicla, los contadores
/// vuelven a cero, y eso le regala a un atacante cinco intentos más cada tanto — nada
/// comparado con lo que tenía sin freno, y sin una escritura por cada login fallido.
/// </para>
/// <para>
/// La contracara conocida: quien sabe el email de alguien puede dejarlo afuera quince
/// minutos. Es el mismo trato que hace cualquier banco, y el mensaje le dice al usuario
/// cuánto esperar.
/// </para>
/// </remarks>
public sealed class FrenoDeLogin
{
    public const int FallosPermitidos = 5;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A partir de cuántas cuentas registradas se barren las vencidas. Sin esto, alguien
    /// que prueba emails inventados haría crecer el diccionario sin techo.
    /// </summary>
    private const int CuentasAntesDeBarrer = 10_000;

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _fallos = new(StringComparer.Ordinal);
    private readonly TimeProvider _reloj;

    public FrenoDeLogin(TimeProvider reloj)
    {
        _reloj = reloj;
    }

    /// <summary>
    /// Cuánto falta para que la cuenta pueda volver a intentar, o <c>null</c> si puede ya.
    /// </summary>
    public TimeSpan? Frenada(string email)
    {
        if (!_fallos.TryGetValue(email, out var cola))
        {
            return null;
        }

        var ahora = _reloj.GetUtcNow();

        lock (cola)
        {
            Descartar(cola, ahora);

            if (cola.Count < FallosPermitidos)
            {
                return null;
            }

            // Se libera cuando el fallo más viejo de los que cuentan sale de la ventana.
            return cola.Peek() + Ventana - ahora;
        }
    }

    public void RegistrarFallo(string email)
    {
        var ahora = _reloj.GetUtcNow();

        if (_fallos.Count >= CuentasAntesDeBarrer)
        {
            Barrer(ahora);
        }

        var cola = _fallos.GetOrAdd(email, _ => new Queue<DateTimeOffset>());

        lock (cola)
        {
            Descartar(cola, ahora);
            cola.Enqueue(ahora);
        }
    }

    /// <summary>Un login correcto borra los fallos: el que se equivocó dos veces y entró no arrastra nada.</summary>
    public void Limpiar(string email) => _fallos.TryRemove(email, out _);

    private static void Descartar(Queue<DateTimeOffset> cola, DateTimeOffset ahora)
    {
        while (cola.Count > 0 && cola.Peek() + Ventana <= ahora)
        {
            cola.Dequeue();
        }
    }

    private void Barrer(DateTimeOffset ahora)
    {
        foreach (var (email, cola) in _fallos)
        {
            lock (cola)
            {
                Descartar(cola, ahora);

                if (cola.Count == 0)
                {
                    _fallos.TryRemove(email, out _);
                }
            }
        }
    }
}
