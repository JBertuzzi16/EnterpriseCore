
namespace GenteFit.Persistencia.Excepciones;

public class GenteFitException : Exception
{
    public GenteFitException(string mensaje)
        : base(mensaje)
    {
    }

    public GenteFitException(
        string mensaje,
        Exception excepcionInterna)
        : base(mensaje, excepcionInterna)
    {
    }
}
