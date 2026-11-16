using Breaku.Domain.Rules;

namespace Breaku.Domain;

public static class ReglasEventos
{
    private static readonly TimeSpan DesfaseBolivia = TimeSpan.FromHours(-4);
    public const int MargenEntreClasesMin = 10;

    public sealed record Resultado(
        int? MinutosPuedeQuedarse,
        string Explicacion);

    public static Resultado? EvaluarEnPuente(
        Evento evento,
        Puente puente,
        DateTimeOffset ahoraUtc,
        int trasladoIdaVueltaMin)
    {
        if (evento.Estado != Evento.EstadoPublicado || evento.LugarId is null || trasladoIdaVueltaMin < 0)
            return null;
        if (puente.HoraFin <= puente.HoraInicio)
            return null;

        var inicio = AHoraBolivia(evento.Inicio);
        var fin = AHoraBolivia(evento.Fin);
        var ahora = ahoraUtc.ToOffset(DesfaseBolivia);
        if (inicio.Date != fin.Date || inicio.Date != ahora.Date || fin <= ahora)
            return null;
        if (puente.DiaSemana != (short)ahora.DayOfWeek)
            return null;

        var inicioEventoMin = Minutos(inicio);
        var finEventoMin = Minutos(fin);
        var ahoraMin = Minutos(ahora);
        var finPuenteMin = Minutos(puente.HoraFin);
        if (finPuenteMin <= ahoraMin)
            return null;

        var trasladoIdaMin = (trasladoIdaVueltaMin + 1) / 2;
        var trasladoVueltaMin = trasladoIdaVueltaMin / 2;
        var inicioVentanaMin = Minutos(puente.HoraInicio) + trasladoIdaMin;
        var despuesDeClases = puente.Tipo == Puente.TipoDespuesDeClases;
        var finVentanaMin = despuesDeClases
            ? Math.Min(finPuenteMin, Minutos(ReglasHorario.FinMasTarde))
            : finPuenteMin - trasladoVueltaMin - MargenEntreClasesMin;

        if (inicioVentanaMin >= finVentanaMin)
            return null;

        if (evento.Asistencia == ReglasEvento.AsistenciaFija)
        {
            if (evento.VisitaMin is not null || evento.VisitaMax is not null
                || inicioEventoMin < inicioVentanaMin
                || inicioEventoMin < ahoraMin
                || finEventoMin > finVentanaMin)
                return null;

            var horaFin = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(finEventoMin));
            return new Resultado(null, $"Hay que llegar a la hora ({inicio:HH:mm}); puedes quedarte hasta las {horaFin:HH:mm}.");
        }

        if (evento.Asistencia != ReglasEvento.AsistenciaLibre
            || evento.VisitaMin is not short visitaMin
            || evento.VisitaMax is not short visitaMax
            || visitaMin < 1
            || visitaMax < visitaMin)
            return null;

        var desdeMin = Math.Max(Math.Max(inicioEventoMin, inicioVentanaMin), ahoraMin);
        var hastaMin = Math.Min(finEventoMin, finVentanaMin);
        var minutosInterseccion = hastaMin - desdeMin;
        var minutosPuedeQuedarse = Math.Min(minutosInterseccion, visitaMax);
        if (minutosPuedeQuedarse < visitaMin)
            return null;

        var horaFinDisponible = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(hastaMin));
        var horaFinEstancia = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(desdeMin + minutosPuedeQuedarse));
        return new Resultado(
            minutosPuedeQuedarse,
            $"Sigue hasta las {fin:HH:mm}; puedes quedarte unos {minutosPuedeQuedarse} min, hasta las {horaFinEstancia:HH:mm} (límite del puente: {horaFinDisponible:HH:mm}).");
    }

    private static DateTimeOffset AHoraBolivia(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(DesfaseBolivia);

    private static int Minutos(DateTimeOffset fecha) => fecha.Hour * 60 + fecha.Minute;

    private static int Minutos(TimeOnly hora) => (int)hora.ToTimeSpan().TotalMinutes;
}
