namespace Breaku.Application.Eventos;

public record PaginasPdf(int TotalPaginas, List<byte[]> Jpeg);

public interface IConvertidorPdf
{
    /// <summary>
    /// Cuenta las páginas y, si no pasan de <paramref name="maxPaginas"/>, las renderiza a JPEG con el lado mayor acotado
    /// a <paramref name="ladoMaxPx"/>. Lanza <c>PDF_PROTEGIDO</c>, <c>PDF_INVALIDO</c> o <c>PDF_MUCHAS_PAGINAS</c>.
    /// </summary>
    PaginasPdf Renderizar(byte[] pdf, int maxPaginas, int ladoMaxPx, int calidad);
}
