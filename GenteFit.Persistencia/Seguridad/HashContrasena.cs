using System.Security.Cryptography;

namespace GenteFit.Persistencia.Seguridad;

// Guarda las contraseñas como PBKDF2-SHA256 con sal aleatoria.
// Formato guardado en Usuario.HashContrasena:  PBKDF2$iteraciones$sal(base64)$hash(base64)
public static class HashContrasena
{
    private const int Iteraciones = 100_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;

    public static string Crear(string contrasena)
    {
        ArgumentException.ThrowIfNullOrEmpty(contrasena);

        byte[] sal = RandomNumberGenerator.GetBytes(TamanoSal);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);

        return $"PBKDF2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string contrasena, string almacenado)
    {
        if (string.IsNullOrEmpty(contrasena) || string.IsNullOrEmpty(almacenado))
            return false;

        string[] partes = almacenado.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2" || !int.TryParse(partes[1], out int iteraciones))
            return false;

        try
        {
            byte[] sal = Convert.FromBase64String(partes[2]);
            byte[] esperado = Convert.FromBase64String(partes[3]);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(
                contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);

            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
