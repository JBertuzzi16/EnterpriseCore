using Dapper;
using Microsoft.Data.SqlClient;
using GenteFit.Persistencia.Entidades;

string cadenaConexion =
    @"Server=localhost\SQLEXPRESS;Database=GenteFit;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";

using var conexion = new SqlConnection(cadenaConexion);

conexion.Open();

string sql = """
    SELECT Id, Nombre, Ubicacion, Activa
    FROM dbo.Salas
    ORDER BY Nombre;
    """;

var salas = conexion.Query<Sala>(sql).ToList();

foreach (var sala in salas)
{
    Console.WriteLine(
        $"{sala.Id} | {sala.Nombre} | {sala.Ubicacion} | Activa: {sala.Activa}");
}

Console.WriteLine($"Total de salas: {salas.Count}");