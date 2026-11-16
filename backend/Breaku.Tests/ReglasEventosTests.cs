using Breaku.Domain;

namespace Breaku.Tests;

public sealed class ReglasEventosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 16, 8, 0, 0, TimeSpan.FromHours(-4));

    private static DateTime Utc(int dia, int hora, int minuto)
        => new DateTimeOffset(2026, 11, dia, hora, minuto, 0, TimeSpan.FromHours(-4)).UtcDateTime;

    private static Evento EventoFijo(int inicioHora, int inicioMin, int finHora, int finMin)
        => new()
        {
            Id = 1,
            LugarId = 2,
            Estado = Evento.EstadoPublicado,
            Asistencia = ReglasEvento.AsistenciaFija,
            Inicio = Utc(16, inicioHora, inicioMin),
            Fin = Utc(16, finHora, finMin)
        };

    private static Puente PuenteEntreClases(TimeOnly inicio, TimeOnly fin)
        => new()
        {
            DiaSemana = 1,
            HoraInicio = inicio,
            HoraFin = fin,
            Tipo = Puente.TipoEntreClases
        };

    [Fact]
    public void Evento_fijo_considera_traslado_de_ida_vuelta_y_margen()
    {
        var puente = PuenteEntreClases(new TimeOnly(10, 0), new TimeOnly(11, 15));
        var evento = EventoFijo(10, 3, 11, 3);

        Assert.Null(ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 20));
        Assert.Null(ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 10));

        var resultado = ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 4);

        Assert.NotNull(resultado);
        Assert.Null(resultado.MinutosPuedeQuedarse);
        Assert.Contains("Hay que llegar a la hora", resultado.Explicacion);
    }

    [Fact]
    public void Evento_libre_usa_la_interseccion_y_respeta_el_minimo_y_maximo_de_visita()
    {
        var evento = new Evento
        {
            LugarId = 2,
            Estado = Evento.EstadoPublicado,
            Asistencia = ReglasEvento.AsistenciaLibre,
            VisitaMin = 45,
            VisitaMax = 60,
            Inicio = Utc(16, 8, 0),
            Fin = Utc(16, 13, 0)
        };
        var puente = PuenteEntreClases(new TimeOnly(10, 0), new TimeOnly(11, 15));

        var resultado = ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 20);

        Assert.NotNull(resultado);
        Assert.Equal(45, resultado.MinutosPuedeQuedarse);
        Assert.Contains("13:00", resultado.Explicacion);
        Assert.Contains("45 min", resultado.Explicacion);
        Assert.Contains("10:55", resultado.Explicacion);

        evento.VisitaMin = 46;
        Assert.Null(ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 20));
    }

    [Fact]
    public void Evento_libre_ya_iniciado_aprovecha_el_tiempo_desde_ahora()
    {
        var evento = new Evento
        {
            LugarId = 2,
            Estado = Evento.EstadoPublicado,
            Asistencia = ReglasEvento.AsistenciaLibre,
            VisitaMin = 20,
            VisitaMax = 60,
            Inicio = Utc(16, 8, 0),
            Fin = Utc(16, 13, 0)
        };
        var puente = PuenteEntreClases(new TimeOnly(10, 0), new TimeOnly(11, 15));
        var ahora = new DateTimeOffset(2026, 11, 16, 10, 30, 0, TimeSpan.FromHours(-4));

        var resultado = ReglasEventos.EvaluarEnPuente(evento, puente, ahora, 20);

        Assert.NotNull(resultado);
        Assert.Equal(25, resultado.MinutosPuedeQuedarse);
        Assert.Contains("10:55", resultado.Explicacion);
    }

    [Fact]
    public void Despues_de_clases_no_descuenta_vuelta_ni_margen_y_respeta_el_tope_de_21()
    {
        var evento = new Evento
        {
            LugarId = 2,
            Estado = Evento.EstadoPublicado,
            Asistencia = ReglasEvento.AsistenciaLibre,
            VisitaMin = 45,
            VisitaMax = 300,
            Inicio = Utc(16, 16, 30),
            Fin = Utc(16, 21, 30)
        };
        var puente = new Puente
        {
            DiaSemana = 1,
            HoraInicio = new TimeOnly(16, 0),
            HoraFin = new TimeOnly(22, 0),
            Tipo = Puente.TipoDespuesDeClases
        };

        var resultado = ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 20);

        Assert.NotNull(resultado);
        Assert.Equal(270, resultado.MinutosPuedeQuedarse);
        Assert.Contains("límite del puente: 21:00", resultado.Explicacion);
    }

    [Fact]
    public void No_recomienda_eventos_no_publicados_sin_lugar_catalogado_ni_ya_iniciados_fijos()
    {
        var puente = PuenteEntreClases(new TimeOnly(10, 0), new TimeOnly(12, 0));
        var evento = EventoFijo(10, 30, 11, 30);

        evento.Estado = Evento.EstadoPendiente;
        Assert.Null(ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 0));

        evento.Estado = Evento.EstadoPublicado;
        evento.LugarId = null;
        Assert.Null(ReglasEventos.EvaluarEnPuente(evento, puente, Ahora, 0));

        evento.LugarId = 2;
        Assert.Null(ReglasEventos.EvaluarEnPuente(
            evento, puente, new DateTimeOffset(2026, 11, 16, 10, 45, 0, TimeSpan.FromHours(-4)), 0));
    }

    [Fact]
    public void Compara_el_dia_local_UTC_menos_cuatro_y_omite_puentes_ya_terminados()
    {
        var puenteLunes = PuenteEntreClases(new TimeOnly(10, 0), new TimeOnly(12, 0));
        var eventoDomingoLocal = EventoFijo(23, 0, 23, 45);
        var ahoraLunesLocal = new DateTimeOffset(2026, 11, 16, 0, 30, 0, TimeSpan.FromHours(-4));

        Assert.Null(ReglasEventos.EvaluarEnPuente(eventoDomingoLocal, puenteLunes, ahoraLunesLocal, 0));

        var eventoLunes = EventoFijo(10, 30, 11, 30);
        var puenteTerminado = PuenteEntreClases(new TimeOnly(8, 0), new TimeOnly(9, 0));
        Assert.Null(ReglasEventos.EvaluarEnPuente(eventoLunes, puenteTerminado, Ahora, 0));
    }
}
