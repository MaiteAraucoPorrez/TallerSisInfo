using Breaku.Application.Admin;
using Breaku.Application.Horarios;
using Breaku.Application.Lugares;
using Breaku.Application.Puentes;
using Breaku.Application.Recomendaciones;
using Breaku.Domain;
using Breaku.Domain.Rules;

namespace Breaku.Tests;

/// <summary>HU30: las reglas puras de afinidad por likes (Domain).</summary>
public class ReglasAfinidadLugaresTests
{
    private const int Comer = 2, Deportes = 3, Estudio = 6;
    private static readonly DateTime Ahora = new(2026, 11, 15, 18, 0, 0, DateTimeKind.Utc);

    private static ReglasAfinidad.VotoLugar Voto(int lugar, string feedback, int diasAtras, params int[] etiquetas)
        => new(lugar, feedback, Ahora.AddDays(-diasAtras), etiquetas);

    // Una cafetería cerca, una cancha a media distancia y una biblioteca lejos.
    private static readonly ReglasAfinidad.CandidatoAfinidad[] Candidatos =
    [
        new(1, "Cafetería Central", 100, [Comer]),
        new(2, "Cancha Polideportiva", 300, [Deportes]),
        new(3, "Biblioteca Central", 500, [Estudio]),
    ];

    private static List<int> Orden(ReglasAfinidad.Perfil perfil, IEnumerable<ReglasAfinidad.CandidatoAfinidad>? candidatos = null)
        => ReglasAfinidad.Ordenar(candidatos ?? Candidatos, c => c, perfil).Select(c => c.LugarId).ToList();

    [Fact]
    public void Sin_historial_el_orden_es_el_de_hoy_por_distancia()
    {
        var perfil = ReglasAfinidad.ArmarPerfil([], Ahora);

        Assert.False(perfil.Personaliza);
        Assert.Equal([1, 2, 3], Orden(perfil));
    }

    [Fact]
    public void Con_menos_de_3_feedbacks_tampoco_cambia_nada_ni_siquiera_el_no_me_sirve()
    {
        var perfil = ReglasAfinidad.ArmarPerfil([Voto(1, "DISLIKE", 1, Comer), Voto(2, "LIKE", 1, Deportes)], Ahora);

        Assert.False(perfil.Personaliza);
        Assert.Equal([1, 2, 3], Orden(perfil));
    }

    [Fact]
    public void Dos_estudiantes_con_la_misma_peticion_y_distinto_historial_reciben_distinto_orden()
    {
        // Ana: le gustó lo deportivo. Diego: le gustó estudiar.
        var ana = ReglasAfinidad.ArmarPerfil([Voto(9, "LIKE", 3, Deportes), Voto(9, "LIKE", 2, Deportes), Voto(8, "LIKE", 1, Deportes)], Ahora);
        var diego = ReglasAfinidad.ArmarPerfil([Voto(7, "LIKE", 3, Estudio), Voto(7, "LIKE", 2, Estudio), Voto(5, "LIKE", 1, Estudio)], Ahora);

        Assert.Equal([2, 1, 3], Orden(ana));
        Assert.Equal([3, 1, 2], Orden(diego));
    }

    [Fact]
    public void Un_like_suma_1_y_un_no_me_sirve_resta_2_a_cada_etiqueta_del_lugar()
    {
        var perfil = ReglasAfinidad.ArmarPerfil(
        [
            Voto(10, "LIKE", 5, Comer, Deportes),   // dos etiquetas: suma a las dos
            Voto(11, "LIKE", 4, Deportes),
            Voto(12, "DISLIKE", 40, Comer),         // vieja: igual cuenta para el peso
        ], Ahora);

        Assert.Equal(-1, perfil.PesosPorEtiqueta[Comer]);
        Assert.Equal(2, perfil.PesosPorEtiqueta[Deportes]);
        Assert.Equal(1, ReglasAfinidad.Puntaje([Comer, Deportes], perfil));
        Assert.Equal(0, ReglasAfinidad.Puntaje([Estudio], perfil));
    }

    [Fact]
    public void Un_lugar_con_no_me_sirve_no_se_repite_durante_30_dias()
    {
        var reciente = ReglasAfinidad.ArmarPerfil(
            [Voto(2, "DISLIKE", 29, Deportes), Voto(1, "LIKE", 2, Comer), Voto(3, "LIKE", 1, Estudio)], Ahora);
        var vencido = ReglasAfinidad.ArmarPerfil(
            [Voto(2, "DISLIKE", 31, Deportes), Voto(1, "LIKE", 2, Comer), Voto(3, "LIKE", 1, Estudio)], Ahora);

        Assert.DoesNotContain(2, Orden(reciente));
        Assert.Contains(2, Orden(vencido));
    }

    [Fact]
    public void En_empate_desempata_por_distancia_y_despues_por_nombre_sin_popularidad()
    {
        var perfil = ReglasAfinidad.ArmarPerfil(
            [Voto(9, "LIKE", 3, Comer), Voto(9, "LIKE", 2, Comer), Voto(9, "LIKE", 1, Comer)], Ahora);
        ReglasAfinidad.CandidatoAfinidad[] empatados =
        [
            new(21, "Pizzería", 400, [Comer]),
            new(22, "Cairo Restaurante", 200, [Comer]),
            new(23, "911 Fast Food", 200, [Comer]),
        ];

        Assert.Equal([23, 22, 21], Orden(perfil, empatados));
    }

    [Fact]
    public void Las_preferencias_solo_reordenan_nunca_agregan_lugares()
    {
        var perfil = ReglasAfinidad.ArmarPerfil(
            [Voto(99, "LIKE", 1, Deportes), Voto(98, "LIKE", 1, Deportes), Voto(97, "LIKE", 1, Deportes)], Ahora);

        var orden = Orden(perfil);

        Assert.Subset(Candidatos.Select(c => c.LugarId).ToHashSet(), orden.ToHashSet());
        Assert.DoesNotContain(99, orden);
    }

    [Fact]
    public void El_resumen_para_el_llm_tiene_solo_nombres_de_etiquetas()
    {
        var perfil = ReglasAfinidad.ArmarPerfil(
            [Voto(1, "LIKE", 1, Deportes), Voto(2, "LIKE", 1, Deportes), Voto(3, "DISLIKE", 1, Estudio)], Ahora);
        var nombres = new Dictionary<int, string> { [Comer] = "Comer", [Deportes] = "Deportes", [Estudio] = "Estudio" };

        var resumen = ReglasAfinidad.Resumir(perfil, nombres)!;

        Assert.Equal(["Deportes"], resumen.LeGustan);
        Assert.Equal(["Estudio"], resumen.PrefiereEvitar);
        Assert.Null(ReglasAfinidad.Resumir(ReglasAfinidad.Perfil.Vacio, nombres));
    }

    [Fact]
    public void Sin_personalizacion_el_plan_b_es_el_de_siempre_y_con_ella_respeta_la_afinidad()
    {
        ReglasRendimiento.Candidato[] candidatos =
        [
            new(1, 2, 100, 20, Afinidad: 0),
            new(2, 6, 400, 20, Afinidad: 3),
        ];

        Assert.Equal(1, ReglasRendimiento.Respaldo(candidatos.Select(c => c with { Afinidad = 0 }), 60)[0].LugarId);
        var personalizado = ReglasRendimiento.Respaldo(candidatos, 60, personalizado: true);
        Assert.Equal(2, personalizado[0].LugarId);
        Assert.DoesNotContain("cercana", personalizado[0].Explicacion);
    }
}

/// <summary>HU30: el servicio de búsqueda puntúa antes del recorte a 10, y el prompt lleva solo etiquetas.</summary>
public class RecuperarConAfinidadTests
{
    private const int Comer = 2, Deportes = 3;

    /// <summary>12 lugares que caben, cada uno un poco más lejos: 1–6 de comida y 7–12 deportivos.</summary>
    private sealed class Lugares : ILugarRepository
    {
        public Dictionary<int, List<ReglasAfinidad.VotoLugar>> Votos { get; } = [];

        public Task<Zona?> ObtenerZonaDelUsuarioAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult<Zona?>(new Zona { Id = 1, Nombre = "Campus", Latitud = -17.37m, Longitud = -66.16m, RadioMaxM = 2000 });

        public Task<List<Lugar>> ListarActivosAsync(CancellationToken ct = default)
            => Task.FromResult(Enumerable.Range(1, 12).Select(i => new Lugar
            {
                Id = i,
                Nombre = $"Lugar {i:00}",
                Categoria = new Categoria { Id = 1, Nombre = i <= 6 ? "Comida" : "Deporte" },
                Latitud = -17.37m + i * 0.0004m,
                Longitud = -66.16m,
                DuracionEstimadaMin = 20,
                Estado = "ACTIVO"
            }).ToList());

        public Task<Dictionary<int, (int MeGusta, int NoMeSirve)>> ContarVotosAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default)
            => Task.FromResult(new Dictionary<int, (int, int)>());

        public Task<Dictionary<int, List<int>>> ListarEtiquetasDeLugaresAsync(IReadOnlyCollection<int> lugarIds, CancellationToken ct = default)
            => Task.FromResult(lugarIds.ToDictionary(id => id, id => new List<int> { id <= 6 ? Comer : Deportes }));

        public Task<List<ReglasAfinidad.VotoLugar>> ListarVotosDelUsuarioAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(Votos.GetValueOrDefault(usuarioId) ?? []);

        public Task<Dictionary<int, string>> ListarNombresEtiquetasAsync(CancellationToken ct = default)
            => Task.FromResult(new Dictionary<int, string> { [Comer] = "Comer", [Deportes] = "Deportes" });
    }

    private sealed class Puentes : IPuenteRepository
    {
        public Task<List<Puente>> ListarPorUsuarioAsync(int usuarioId, CancellationToken ct = default)
            => Task.FromResult(new List<Puente> { new() { Id = 1, UsuarioId = usuarioId, DiaSemana = 1, HoraInicio = new(10, 0), HoraFin = new(12, 0) } });

        public Task ReemplazarAsync(int usuarioId, IReadOnlyList<Puente> nuevos, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly Puente PuenteDeDosHoras = new() { Id = 1, DiaSemana = 1, HoraInicio = new(10, 0), HoraFin = new(12, 0) };

    private static ReglasAfinidad.VotoLugar Like(int lugar, int etiqueta) => new(lugar, "LIKE", DateTime.UtcNow.AddDays(-1), [etiqueta]);

    private static (BusquedaLugaresService Servicio, Lugares Repo) Armar()
    {
        var repo = new Lugares();
        return (new BusquedaLugaresService(repo, new Puentes()), repo);
    }

    [Fact]
    public async Task Sin_historial_llegan_al_llm_los_10_mas_cercanos_como_hoy()
    {
        var (servicio, _) = Armar();

        var contexto = await servicio.RecuperarAsync(1, PuenteDeDosHoras);

        Assert.Equal(Enumerable.Range(1, 10), contexto.Lugares.Select(l => l.LugarId));
        Assert.False(contexto.Personalizado);
        Assert.Null(contexto.Preferencias);
    }

    [Fact]
    public async Task La_afinidad_puntua_antes_del_recorte_y_decide_quienes_llegan_al_llm()
    {
        var (servicio, repo) = Armar();
        repo.Votos[1] = [Like(12, Deportes), Like(12, Deportes), Like(11, Deportes)];

        var contexto = await servicio.RecuperarAsync(1, PuenteDeDosHoras);

        // Los deportivos, incluso los más lejanos (11 y 12), entran antes que la comida.
        Assert.Equal([7, 8, 9, 10, 11, 12, 1, 2, 3, 4], contexto.Lugares.Select(l => l.LugarId));
        Assert.True(contexto.Personalizado);
        Assert.Equal(["Deportes"], contexto.Preferencias!.LeGustan);
        Assert.Equal(["Deportes"], contexto.Lugares[0].Etiquetas);
    }

    [Fact]
    public async Task Dos_estudiantes_con_distinto_historial_reciben_distintos_candidatos()
    {
        var (servicio, repo) = Armar();
        repo.Votos[1] = [Like(12, Deportes), Like(11, Deportes), Like(10, Deportes)];
        repo.Votos[2] = [Like(1, Comer), Like(2, Comer), Like(3, Comer)];

        var ana = await servicio.RecuperarAsync(1, PuenteDeDosHoras);
        var diego = await servicio.RecuperarAsync(2, PuenteDeDosHoras);

        Assert.NotEqual(ana.Lugares.Select(l => l.LugarId), diego.Lugares.Select(l => l.LugarId));
        Assert.Equal(7, ana.Lugares[0].LugarId);
        Assert.Equal(1, diego.Lugares[0].LugarId);
    }

    [Fact]
    public async Task Nunca_entra_un_lugar_que_no_cabe_en_el_puente()
    {
        var (servicio, repo) = Armar();
        repo.Votos[1] = [Like(12, Deportes), Like(11, Deportes), Like(10, Deportes)];
        // Un puente de 30 minutos: solo caben los más cercanos (ida y vuelta + 20 min de actividad).
        var corto = new Puente { Id = 1, DiaSemana = 1, HoraInicio = new(10, 0), HoraFin = new(10, 30) };

        var contexto = await servicio.RecuperarAsync(1, corto);

        Assert.All(contexto.Lugares, l => Assert.True(l.TiempoTrasladoMin + l.DuracionEstimadaMin <= 30));
    }

    [Fact]
    public async Task La_lista_de_lugares_viables_no_se_personaliza()
    {
        var (servicio, repo) = Armar();
        repo.Votos[1] = [Like(12, Deportes), Like(11, Deportes), Like(10, Deportes)];

        var lista = await servicio.BuscarParaPuenteAsync(1, 1);

        Assert.Equal(Enumerable.Range(1, 12), lista.Lugares.Select(l => l.LugarId));
    }

    [Fact]
    public void Sin_preferencias_el_prompt_es_el_de_antes_y_con_ellas_lleva_solo_etiquetas()
    {
        var lugares = new List<LugarCandidato>
        {
            new(7, "Cancha", "Deporte", null, 300, 8, 20, Etiquetas: ["Deportes"]),
        };

        var sinPreferencias = PromptRecomendacion.Construir(lugares, 60, "algo movido");
        var conPreferencias = PromptRecomendacion.Construir(lugares, 60, "algo movido",
            new ReglasAfinidad.ResumenPreferencias(["Deportes"], ["Estudio"]));

        Assert.DoesNotContain("PREFERENCIAS_DEL_ESTUDIANTE", sinPreferencias.Usuario);
        Assert.DoesNotContain("etiquetas", sinPreferencias.Usuario);
        Assert.Contains("PREFERENCIAS_DEL_ESTUDIANTE", conPreferencias.Usuario);
        Assert.Contains("\"leGustan\":[\"Deportes\"]", conPreferencias.Usuario);
        Assert.Contains("\"etiquetas\":[\"Deportes\"]", conPreferencias.Usuario);
        Assert.Contains("nunca agregues un lugar", conPreferencias.Sistema);
    }

    [Fact]
    public async Task Si_el_llm_falla_el_plan_b_respeta_la_afinidad_y_el_llm_no_puede_agregar_lugares()
    {
        var (busqueda, repo) = Armar();
        repo.Votos[1] = [Like(12, Deportes), Like(11, Deportes), Like(10, Deportes)];
        var guardado = new Recomendaciones();

        var caido = new RecomendacionService(new Puentes(), busqueda, new AsistenteCaido(), guardado);
        var plan = await caido.RecomendarAsync(1, new SolicitudRecomendacionDto(1, null));
        Assert.All(plan.Recomendaciones, r => Assert.Equal("Deporte", r.Categoria));

        var inventor = new RecomendacionService(new Puentes(), busqueda, new AsistenteInventor(), guardado);
        var respuesta = await inventor.RecomendarAsync(1, new SolicitudRecomendacionDto(1, null));
        Assert.Equal([7], respuesta.Recomendaciones.Select(r => r.LugarId));
    }

    private sealed class AsistenteCaido : IAsistenteIa
    {
        public Task<RespuestaAsistente> GenerarAsync(PromptRag prompt, CancellationToken ct = default)
            => throw new HttpRequestException("El LLM respondió 500.");
    }

    /// <summary>Devuelve un lugar recuperado y otro que no estaba en la lista (999): FiltrarSoloRecuperados lo saca.</summary>
    private sealed class AsistenteInventor : IAsistenteIa
    {
        public Task<RespuestaAsistente> GenerarAsync(PromptRag prompt, CancellationToken ct = default)
            => Task.FromResult(new RespuestaAsistente("falso", "{}",
                [new SugerenciaAsistente(7, "Ideal para moverte un rato."), new SugerenciaAsistente(999, "Un lugar inventado.")]));
    }

    private sealed class Recomendaciones : IRecomendacionRepository
    {
        public Task GuardarAsync(LogAuditoriaRag log, IReadOnlyList<Recomendacion> recomendaciones, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Recomendacion?> ObtenerDeUsuarioAsync(long recomendacionId, int usuarioId, CancellationToken ct = default) => Task.FromResult<Recomendacion?>(null);
        public Task GuardarFeedbackAsync(Recomendacion recomendacion, string feedback, CancellationToken ct = default) => Task.CompletedTask;
    }
}

/// <summary>HU30: etiquetas del catálogo en el admin.</summary>
public class EtiquetasAdminTests
{
    private const int Admin = 2;

    [Theory]
    [InlineData("  Juegos   de mesa ", "Juegos de mesa")]
    [InlineData("Voluntariado", "Voluntariado")]
    public void El_nombre_se_guarda_sin_espacios_de_mas(string entrada, string esperado)
        => Assert.Equal(esperado, ReglasEtiquetas.LimpiarNombre(entrada));

    [Fact]
    public void Las_etiquetas_tienen_que_existir_y_un_lugar_activo_recomendable_necesita_al_menos_una()
    {
        int[] catalogo = [1, 2, 3];
        Assert.Null(ReglasEtiquetas.ValidarAsignacion([1, 3], catalogo, true));
        Assert.Equal(ReglasEtiquetas.MensajeNoExiste, ReglasEtiquetas.ValidarAsignacion([9], catalogo, false));
        Assert.Equal(ReglasEtiquetas.MensajeSinEtiqueta, ReglasEtiquetas.ValidarAsignacion([], catalogo, true));
        Assert.Null(ReglasEtiquetas.ValidarAsignacion([], catalogo, false));   // en revisión: puede esperar
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(false, 0, true)]   // de referencia (bloques, parqueo): no se recomienda, no necesita etiqueta
    public void Solo_se_activa_un_recomendable_con_etiqueta(bool recomendable, int etiquetas, bool puede)
        => Assert.Equal(puede, ReglasEtiquetas.PuedeActivarse(recomendable, etiquetas));

    private sealed class Etiquetas : IEtiquetaRepository
    {
        public List<EtiquetaDto> Catalogo { get; } = [new(1, "Comer"), new(3, "Deportes")];
        public EstadoEtiquetasLugar? Estado { get; set; }

        public Task<List<EtiquetaDto>> ListarAsync(CancellationToken ct = default) => Task.FromResult(Catalogo);
        public Task<bool> NombreEnUsoAsync(string nombre, CancellationToken ct = default)
            => Task.FromResult(Catalogo.Any(e => string.Equals(e.Nombre, nombre, StringComparison.OrdinalIgnoreCase)));
        public Task<int> CrearAsync(string nombre, CancellationToken ct = default)
        {
            Catalogo.Add(new EtiquetaDto(9, nombre));
            return Task.FromResult(9);
        }
        public Task<EstadoEtiquetasLugar?> EstadoDelLugarAsync(int lugarId, CancellationToken ct = default) => Task.FromResult(Estado);
    }

    private sealed class Admin_ : IAdminRepository
    {
        public DatosLugar? Creado { get; private set; }
        public DatosLugar? Editado { get; private set; }
        public string? EstadoNuevo { get; private set; }

        public Task<List<CampusAdminDto>> ListarCampusActivosAsync(CancellationToken ct = default)
            => Task.FromResult(new List<CampusAdminDto> { new(1, "Campus", -17.37m, -66.16m, 1000) });
        public Task<List<CategoriaAdminDto>> ListarCategoriasAsync(CancellationToken ct = default)
            => Task.FromResult(new List<CategoriaAdminDto> { new(1, "Comida") });
        public Task<bool> NombreEnUsoAsync(string nombre, int? excluirId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CrearLugarAsync(DatosLugar datos, int adminId, CancellationToken ct = default)
        {
            Creado = datos;
            return Task.FromResult(30);
        }
        public Task<bool> EditarLugarAsync(int lugarId, DatosLugar datos, int adminId, CancellationToken ct = default)
        {
            Editado = datos;
            return Task.FromResult(true);
        }
        public Task<string?> CambiarEstadoLugarAsync(int lugarId, string estado, int adminId, CancellationToken ct = default)
        {
            EstadoNuevo = estado;
            return Task.FromResult<string?>("EN_REVISION");
        }

        public Task<ResumenAdminDto> ResumenAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<LugarAdminDto>> ListarLugaresAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool?> CambiarValidacionLugarAsync(int lugarId, bool validado, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LugarDetalleAdminDto?> ObtenerLugarAsync(int lugarId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> LugarEnUsoAsync(int lugarId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> EliminarLugarAsync(int lugarId, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<ReporteAdminDto>> ListarReportesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RevisionReporte?> ResolverReporteAsync(int reporteId, string estado, string? respuesta, int adminId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<TurnoConsultaRag>> ListarConsultasRagAsync(int limite, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MetricasAdminDto> MetricasAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static GuardarLugarDto Lugar(List<int>? etiquetas)
        => new("Pizzería Marechiaro", null, 1, -17.3702m, -66.1601m, 40, null, null, null, etiquetas);

    private static (AdminService Servicio, Admin_ Repo, Etiquetas Etiquetas) Armar()
    {
        var repo = new Admin_();
        var etiquetas = new Etiquetas();
        return (new AdminService(repo, null, etiquetas), repo, etiquetas);
    }

    [Fact]
    public async Task Al_crear_un_lugar_guarda_sus_etiquetas()
    {
        var (servicio, repo, _) = Armar();

        await servicio.CrearLugarAsync(Lugar([3, 1, 3]), Admin);

        Assert.Equal([1, 3], repo.Creado!.EtiquetaIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new int[0])]
    public async Task Un_lugar_nuevo_sin_etiquetas_no_se_crea(int[]? etiquetas)
    {
        var (servicio, repo, _) = Armar();

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearLugarAsync(Lugar(etiquetas?.ToList()), Admin));

        Assert.Equal(("VALIDACION", ReglasEtiquetas.MensajeSinEtiqueta), (ex.Codigo, ex.Message));
        Assert.Null(repo.Creado);
    }

    [Fact]
    public async Task Una_etiqueta_que_no_esta_en_el_catalogo_es_error_de_validacion()
    {
        var (servicio, _, _) = Armar();
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearLugarAsync(Lugar([42]), Admin));
        Assert.Equal(ReglasEtiquetas.MensajeNoExiste, ex.Message);
    }

    [Fact]
    public async Task Al_editar_sin_mandar_etiquetas_el_lugar_conserva_las_suyas()
    {
        var (servicio, repo, etiquetas) = Armar();
        etiquetas.Estado = new EstadoEtiquetasLugar("ACTIVO", true, 2);

        await servicio.EditarLugarAsync(5, Lugar(null), Admin);

        Assert.Null(repo.Editado!.EtiquetaIds);
    }

    [Fact]
    public async Task No_se_le_pueden_quitar_todas_las_etiquetas_a_un_lugar_activo_recomendable()
    {
        var (servicio, repo, etiquetas) = Armar();
        etiquetas.Estado = new EstadoEtiquetasLugar("ACTIVO", true, 2);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.EditarLugarAsync(5, Lugar([]), Admin));

        Assert.Equal(ReglasEtiquetas.MensajeSinEtiqueta, ex.Message);
        Assert.Null(repo.Editado);
    }

    [Fact]
    public async Task Activar_un_lugar_recomendable_sin_etiquetas_se_rechaza()
    {
        var (servicio, repo, etiquetas) = Armar();
        etiquetas.Estado = new EstadoEtiquetasLugar("EN_REVISION", true, 0);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            servicio.CambiarEstadoLugarAsync(7, new CambioEstadoLugarDto("ACTIVO"), Admin));

        Assert.Equal(("VALIDACION", ReglasEtiquetas.MensajeSinEtiqueta), (ex.Codigo, ex.Message));
        Assert.Null(repo.EstadoNuevo);
    }

    [Fact]
    public async Task Con_una_etiqueta_si_se_activa_y_desactivar_nunca_la_pide()
    {
        var (servicio, repo, etiquetas) = Armar();
        etiquetas.Estado = new EstadoEtiquetasLugar("EN_REVISION", true, 1);
        await servicio.CambiarEstadoLugarAsync(7, new CambioEstadoLugarDto("ACTIVO"), Admin);
        Assert.Equal("ACTIVO", repo.EstadoNuevo);

        etiquetas.Estado = new EstadoEtiquetasLugar("ACTIVO", true, 0);
        await servicio.CambiarEstadoLugarAsync(7, new CambioEstadoLugarDto("INACTIVO"), Admin);
        Assert.Equal("INACTIVO", repo.EstadoNuevo);
    }

    [Fact]
    public async Task El_admin_agrega_etiquetas_sin_repetir_nombres()
    {
        var (servicio, _, _) = Armar();

        var nueva = await servicio.CrearEtiquetaAsync(new NuevaEtiquetaDto("  Voluntariado "));
        Assert.Equal(new EtiquetaDto(9, "Voluntariado"), nueva);

        var repetida = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearEtiquetaAsync(new NuevaEtiquetaDto("deportes")));
        Assert.Equal("NOMBRE_REPETIDO", repetida.Codigo);
        var vacia = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearEtiquetaAsync(new NuevaEtiquetaDto("   ")));
        Assert.Equal("VALIDACION", vacia.Codigo);
    }
}
