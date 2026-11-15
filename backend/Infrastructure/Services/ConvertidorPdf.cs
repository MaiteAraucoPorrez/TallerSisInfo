using Breaku.Application.Eventos;
using Breaku.Application.Horarios;
using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace Breaku.Infrastructure.Services;

/// <summary>
/// Convierte un PDF a imágenes con PDFium (PDFtoImage 5.4.0). Se verificó en Windows; para publicar hay que usar
/// <c>-r &lt;rid&gt;</c> y Linux no está verificado.
/// </summary>
public class ConvertidorPdf : IConvertidorPdf
{
    private static readonly object Candado = new();   // PDFium no es seguro entre hilos

    public PaginasPdf Renderizar(byte[] pdf, int maxPaginas, int ladoMaxPx, int calidad)
    {
        try
        {
            lock (Candado)
            {
                using var flujo = new MemoryStream(pdf, writable: false);
                var total = Conversion.GetPageCount(flujo, leaveOpen: true, password: null);
                if (total < 1) throw Invalido();
                if (total > maxPaginas)
                    throw new ReglaNegocioException("PDF_MUCHAS_PAGINAS",
                        $"El PDF tiene {total} páginas y el máximo es {maxPaginas}. Sube solo el afiche.");

                var paginas = new List<byte[]>();
                for (var i = 0; i < total; i++)
                {
                    var opciones = new RenderOptions(Dpi: 96, WithAspectRatio: true, WithAnnotations: true,
                        BackgroundColor: SKColors.White);
                    flujo.Position = 0;
                    using var bitmap = Conversion.ToImage(flujo, i, leaveOpen: true, password: null, options: Acotar(flujo, i, opciones, ladoMaxPx));
                    using var imagen = SKImage.FromBitmap(bitmap);
                    using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, calidad);
                    paginas.Add(datos.ToArray());
                }
                return new PaginasPdf(total, paginas);
            }
        }
        catch (PdfPasswordProtectedException)
        {
            throw new ReglaNegocioException("PDF_PROTEGIDO", "El PDF tiene contraseña. Quítasela y vuelve a subirlo.");
        }
        catch (Exception ex) when (ex is PdfException or PdfInvalidFormatException or PdfCannotOpenFileException or PdfPageNotFoundException
                                      or ArgumentException or InvalidOperationException)
        {
            throw Invalido();
        }
    }

    /// <summary>Acota el lado mayor a <paramref name="ladoMaxPx"/> sin ampliar páginas pequeñas.</summary>
    private static RenderOptions Acotar(Stream flujo, int pagina, RenderOptions opciones, int ladoMaxPx)
    {
        flujo.Position = 0;
        var tam = Conversion.GetPageSize(flujo, pagina, leaveOpen: true, password: null);   // en puntos (1/72")
        var mayor = Math.Max(tam.Width, tam.Height);
        if (mayor <= 0) return opciones;
        var ancho = (int)Math.Round(tam.Width * ladoMaxPx / mayor);
        var alto = (int)Math.Round(tam.Height * ladoMaxPx / mayor);
        return opciones with { Width = Math.Max(ancho, 1), Height = Math.Max(alto, 1), Dpi = 72 };
    }

    private static ReglaNegocioException Invalido() =>
        new("PDF_INVALIDO", "No pudimos leer el PDF: parece dañado o no es un PDF válido.");
}
