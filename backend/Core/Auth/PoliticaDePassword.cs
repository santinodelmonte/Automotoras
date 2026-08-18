namespace AutomotoraSaaS.Core.Auth;

/// <summary>
/// Requisitos mínimos de una contraseña. Vive acá y no repetido en cada validador para
/// que subir la exigencia sea un cambio en un solo lugar.
/// </summary>
public static class PoliticaDePassword
{
    public const int MinimoDeCaracteres = 10;
    public const int MaximoDeCaracteres = 128;

    public const string Mensaje =
        "La contraseña necesita al menos 10 caracteres, con letras y números.";

    /// <summary>
    /// Largo por encima del mínimo, con al menos una letra y al menos un dígito. No se
    /// exigen símbolos: alargan la contraseña que la gente termina anotando en un papel
    /// sin agregar entropía real.
    /// </summary>
    public static bool EsAceptable(string? password)
    {
        if (password is null || password.Length is < MinimoDeCaracteres or > MaximoDeCaracteres)
        {
            return false;
        }

        return password.Any(char.IsLetter) && password.Any(char.IsDigit);
    }
}
