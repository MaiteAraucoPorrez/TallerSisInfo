using Breaku.Application.Horarios;
using Breaku.Application.Puentes;
using Breaku.Application.Recomendaciones;
using Breaku.Application.Ubicacion;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Application.Lugares;

public class BusquedaLugaresService : IRecuperadorLugares
{
    public const string EstadoOk = "OK";
    public const string EstadoSinResultados = "SIN_RESULTADOS";
    public const string EstadoSinZona = "SIN_ZONA";

    private readonly ILugarRepository _lugares;
    private readonly IPuenteRepository _puentes;
    private readonly TimeProvider _reloj;

    public BusquedaLugaresService(ILugarRepository lugares, IPuenteRepository puentes, TimeProvider? reloj = null)
    {
        _lugares = lugares;
        _puentes = puentes;
        _reloj = reloj ?? TimeProvider.System;
    }

    public async Task<ResultadoBusquedaLugaresDto> BuscarParaPuenteAsync(int usuarioId, int puenteId, CancellationToken ct = default)
    {
        var puente = (await _puentes.ListarPorUsuarioAsync(usuarioId, ct)).FirstOrDefault(p => p.Id == puenteId)
                     ?? throw new ReglaNegocioException("NO_ENCONTRADO", "El puente no existe.");
        var disponible = MinutosDelPuente(puente);

        // La lista de lugares viables (HU6) no se personaliza: muestra todo lo que cabe, por distancia.
        var busqueda = await BuscarAsync(usuarioId, puente, ReglasBusquedaLugares.MaximoLugaresListado, personalizar: false, ct);
        var zona = busqueda.Zona;
        var candidatos = busqueda.Candidatos;

        if (zona is null)
            return new ResultadoBusquedaLugaresDto(EstadoSinZona, ReglasBusquedaLugares.MensajeSinZona, null, null, disponible, []);

        var zonaDto = new ZonaDto(zona.Id, zona.Nombre);
        if (candidatos.Count == 0)
            return new ResultadoBusquedaLugaresDto(EstadoSinResultados, ReglasBusquedaLugares.MensajeSinResultados, zonaDto, zona.RadioMaxM, disponible, []);

        return new ResultadoBusquedaLugaresDto(EstadoOk, null, zonaDto, zona.RadioMaxM, disponible, candidatos.Select(ADto).ToList());
    }

    /// <summary>
    /// Los candidatos para el asistente (HU7). HU30: con 3 feedbacks o más, la afinidad del estudiante decide el orden (y
    /// saca los lugares con «no me sirve» de los últimos 30 días) ANTES del recorte a 10, así decide cuáles llegan al LLM.
    /// </summary>
    public async Task<ContextoRecuperado> RecuperarAsync(int usuarioId, Puente puente, CancellationToken ct = default)
    {
        var b = await BuscarAsync(usuarioId, puente, ReglasBusquedaLugares.MaximoCandidatosAsistente, personalizar: true, ct);
        return new ContextoRecuperado(b.Zona?.Id, b.Zona?.RadioMaxM, b.Candidatos, b.Personalizado, b.Preferencias);
    }

    private sealed record Busqueda(Zona? Zona, List<LugarCandidato> Candidatos, bool Personalizado = false,
        ReglasAfinidad.ResumenPreferencias? Preferencias = null);

    private async Task<Busqueda> BuscarAsync(int usuarioId, Puente puente, int maximo, bool personalizar, CancellationToken ct)
    {
        var zona = await _lugares.ObtenerZonaDelUsuarioAsync(usuarioId, ct);
        if (zona is null) return new Busqueda(null, []);

        var disponible = MinutosDelPuente(puente);
        var activos = (await _lugares.ListarActivosAsync(ct))
            .Where(l => l.Estado == ReglasBusquedaLugares.EstadoActivo);

        // Los filtros de siempre (estado, métricas, radio y CabeEnElPuente): la afinidad solo reordena lo que queda.
        var queCaben = activos
            .Select(l =>
            {
                var distancia = (int)Math.Round(ReglasBusquedaLugares.DistanciaMetros(zona.Latitud, zona.Longitud, l.Latitud, l.Longitud));
                var traslado = ReglasBusquedaLugares.TiempoTrasladoIdaVueltaMin(distancia);
                return new LugarCandidato(l.Id, l.Nombre, l.Categoria.Nombre, l.Descripcion, distancia, traslado, l.DuracionEstimadaMin,
                    l.Validado, l.TipoLugar, ListasLugar.SepararPorComas(l.Modalidad), ListasLugar.SepararPorComas(l.NivelEnergia),
                    ListasLugar.LeerJson(l.ActividadesSugeridas), ReglasFotoLugar.Url(l.Id, l.FotoActualizadaEn));
            })
            .Where(c => ReglasBusquedaLugares.TieneMetricasCompletas(c.Nombre, c.Categoria, c.DistanciaM, c.DuracionEstimadaMin))
            .Where(c => ReglasBusquedaLugares.EstaDentroDelRadio(c.DistanciaM, zona.RadioMaxM))
            .Where(c => ReglasBusquedaLugares.CabeEnElPuente(c.TiempoTrasladoMin, c.DuracionEstimadaMin, disponible))
            .ToList();

        var perfil = ReglasAfinidad.Perfil.Vacio;
        Dictionary<int, List<int>> etiquetas = [];
        if (personalizar && queCaben.Count > 0)
        {
            perfil = ReglasAfinidad.ArmarPerfil(await _lugares.ListarVotosDelUsuarioAsync(usuarioId, ct), _reloj.GetUtcNow().UtcDateTime);
            if (perfil.Personaliza)
                etiquetas = await _lugares.ListarEtiquetasDeLugaresAsync(queCaben.Select(c => c.LugarId).ToList(), ct);
        }

        // Sin personalización, Ordenar deja el orden de siempre (distancia y nombre).
        var candidatos = ReglasAfinidad
            .Ordenar(queCaben, c => new ReglasAfinidad.CandidatoAfinidad(c.LugarId, c.Nombre, c.DistanciaM,
                etiquetas.GetValueOrDefault(c.LugarId) ?? []), perfil)
            .Take(maximo)
            .ToList();

        // La puntuación sale de los votos de los estudiantes (HU9): una consulta para todos los candidatos.
        var votos = await _lugares.ContarVotosAsync(candidatos.Select(c => c.LugarId).ToList(), ct);
        candidatos = candidatos
            .Select(c => votos.TryGetValue(c.LugarId, out var v)
                ? c with { Puntuacion = ReglasFeedback.Puntuacion(v.MeGusta, v.NoMeSirve), Votos = v.MeGusta + v.NoMeSirve }
                : c)
            .ToList();

        if (!perfil.Personaliza) return new Busqueda(zona, candidatos);

        var nombres = await _lugares.ListarNombresEtiquetasAsync(ct);
        candidatos = candidatos
            .Select(c =>
            {
                var suyas = etiquetas.GetValueOrDefault(c.LugarId) ?? [];
                return c with
                {
                    Etiquetas = suyas.Where(nombres.ContainsKey).Select(e => nombres[e]).ToList(),
                    Afinidad = ReglasAfinidad.Puntaje(suyas, perfil)
                };
            })
            .ToList();
        return new Busqueda(zona, candidatos, true, ReglasAfinidad.Resumir(perfil, nombres));
    }

    private static int MinutosDelPuente(Puente puente) => (int)(puente.HoraFin - puente.HoraInicio).TotalMinutes;

    private static LugarViableDto ADto(LugarCandidato c) =>
        new(c.LugarId, c.Nombre, c.Categoria, c.Descripcion, c.DistanciaM, c.TiempoTrasladoMin, c.DuracionEstimadaMin,
            c.Validado, c.TipoLugar, c.Modalidad ?? [], c.NivelEnergia ?? [], c.ActividadesSugeridas ?? [], c.FotoUrl,
            c.Puntuacion, c.Votos);
}
