using Breaku.Domain.Rules;

namespace Breaku.Tests;

/// <summary>Planes con conexiones de «Libres contigo» (migración 032): ratos de hoy, horarios y avisos.</summary>
public class ReglasPlanTests
{
    private static ReglasMatching.Franja F(int dia, string inicio, string fin) => new(dia, TimeOnly.Parse(inicio), TimeOnly.Parse(fin));

    [Fact]
    public void El_tramo_es_el_cruce_de_hoy_desde_ahora_redondeado()
    {
        var tramos = ReglasPlan.TramosDeHoy([(10, F(5, "09:30", "12:00"))], [F(5, "10:00", "11:30")], 5, new TimeOnly(10, 12));

        var t = Assert.Single(tramos);
        Assert.Equal(10, t.MiPuenteId);
        Assert.Equal(new TimeOnly(10, 15), t.Inicio);
        Assert.Equal(new TimeOnly(11, 30), t.Fin);
        Assert.Equal(75, t.Minutos);
    }

    [Fact]
    public void Si_todavia_no_empieza_arranca_al_inicio_del_cruce()
    {
        var t = Assert.Single(ReglasPlan.TramosDeHoy([(1, F(2, "14:00", "16:00"))], [F(2, "14:30", "17:00")], 2, new TimeOnly(9, 0)));
        Assert.Equal((new TimeOnly(14, 30), new TimeOnly(16, 0)), (t.Inicio, t.Fin));
    }

    [Fact]
    public void Otro_dia_no_cuenta()
        => Assert.Empty(ReglasPlan.TramosDeHoy([(1, F(3, "10:00", "12:00"))], [F(3, "10:00", "12:00")], 2, new TimeOnly(9, 0)));

    [Fact]
    public void Menos_de_30_minutos_en_comun_no_es_tramo()
        => Assert.Empty(ReglasPlan.TramosDeHoy([(1, F(1, "10:00", "11:00"))], [F(1, "10:40", "12:00")], 1, new TimeOnly(8, 0)));

    [Fact]
    public void Si_queda_muy_poco_del_rato_ya_no_se_propone()
        => Assert.Empty(ReglasPlan.TramosDeHoy([(1, F(1, "10:00", "11:00"))], [F(1, "10:00", "11:00")], 1, new TimeOnly(10, 50)));

    [Fact]
    public void Un_rato_que_ya_paso_no_aparece()
        => Assert.Empty(ReglasPlan.TramosDeHoy([(1, F(1, "08:00", "09:00"))], [F(1, "08:00", "09:00")], 1, new TimeOnly(12, 0)));

    [Fact]
    public void Un_rato_largo_se_corta_en_cuatro_horas()
    {
        var t = Assert.Single(ReglasPlan.TramosDeHoy([(1, F(1, "08:00", "14:00"))], [F(1, "08:00", "14:00")], 1, new TimeOnly(7, 0)));
        Assert.Equal(ReglasPlan.MaximoMinutos, t.Minutos);
    }

    [Fact]
    public void Varios_ratos_salen_ordenados_por_hora()
    {
        var tramos = ReglasPlan.TramosDeHoy(
            [(2, F(1, "15:00", "17:00")), (1, F(1, "09:00", "11:00"))],
            [F(1, "09:00", "11:00"), F(1, "15:00", "17:00")], 1, new TimeOnly(7, 0));

        Assert.Equal([1, 2], tramos.Select(t => t.MiPuenteId));
    }

    [Fact]
    public void Lo_comprometido_parte_el_rato_y_quedan_los_pedazos_que_sirven()
    {
        var tramo = new ReglasPlan.Tramo(1, new TimeOnly(11, 15), new TimeOnly(13, 0));
        var libres = ReglasPlan.QuitarOcupados([tramo], [(new TimeOnly(12, 0), new TimeOnly(12, 30))]);

        Assert.Equal([(new TimeOnly(11, 15), new TimeOnly(12, 0)), (new TimeOnly(12, 30), new TimeOnly(13, 0))],
            libres.Select(t => (t.Inicio, t.Fin)));
    }

    [Fact]
    public void Un_pedazo_de_menos_de_15_minutos_no_queda()
    {
        var tramo = new ReglasPlan.Tramo(1, new TimeOnly(11, 0), new TimeOnly(12, 0));
        var libres = ReglasPlan.QuitarOcupados([tramo], [(new TimeOnly(11, 10), new TimeOnly(12, 0))]);
        Assert.Empty(libres);
    }

    [Fact]
    public void Lo_comprometido_fuera_del_rato_no_lo_cambia()
    {
        var tramo = new ReglasPlan.Tramo(1, new TimeOnly(11, 0), new TimeOnly(12, 0));
        Assert.Equal([tramo], ReglasPlan.QuitarOcupados([tramo], [(new TimeOnly(9, 0), new TimeOnly(11, 0))]));
    }

    [Theory]
    [InlineData(10, 31, 10, 35)]
    [InlineData(10, 35, 10, 35)]
    [InlineData(10, 58, 11, 0)]
    [InlineData(23, 58, 23, 55)]
    public void Redondea_a_los_5_minutos_hacia_arriba(int h, int m, int hEsperada, int mEsperado)
        => Assert.Equal(new TimeOnly(hEsperada, mEsperado), ReglasPlan.RedondearArriba(new TimeOnly(h, m), 5));

    [Fact]
    public void Se_superpone_con_un_minuto_en_comun_y_no_si_solo_se_tocan()
    {
        var tramo = new ReglasPlan.Tramo(1, new TimeOnly(10, 0), new TimeOnly(11, 0));
        Assert.True(ReglasPlan.SeSuperpone(new TimeOnly(10, 59), new TimeOnly(12, 0), tramo));
        Assert.False(ReglasPlan.SeSuperpone(new TimeOnly(11, 0), new TimeOnly(12, 0), tramo));
        Assert.False(ReglasPlan.SeSuperpone(new TimeOnly(9, 0), new TimeOnly(10, 0), tramo));
    }

    [Fact]
    public void Actividad_y_texto_original_del_plan()
    {
        Assert.Equal("Plan en Coffee Break", ReglasPlan.ActividadDelPlan(" Coffee Break "));
        Assert.True(ReglasPlan.ActividadDelPlan(new string('x', 200)).Length <= ReglasPlan.MaximoLargoActividad);
        // Nunca vacío: un texto original vacío marca a un organizador heredado (HU28).
        Assert.Equal("Plan con Ana Quiroga desde Libres contigo", ReglasPlan.TextoOriginal("Ana Quiroga"));
    }

    [Fact]
    public void Los_avisos_dicen_quien_que_y_cuando()
    {
        Assert.Equal("Maite A. te propone un plan: Coffee Break, hoy de 10:30 a 11:15. ¿Vas?",
            ReglasPlan.MensajePlan("Maite A.", "Coffee Break", new TimeOnly(10, 30), new TimeOnly(11, 15)));
        Assert.Equal("Maite A. te invita a «jugar vóley», hoy de 10:00 a 11:00.",
            ReglasPlan.MensajeInvitacionComunidad("Maite A.", "jugar vóley", new TimeOnly(10, 0), new TimeOnly(11, 0)));
        Assert.Equal("Maite A. te invita al evento «Noche acústica», hoy de 18:00 a 20:00.",
            ReglasPlan.MensajeInvitacionEvento("Maite A.", "Noche acústica", new TimeOnly(18, 0), new TimeOnly(20, 0)));
    }

    [Fact]
    public void Un_aviso_largo_recorta_solo_el_nombre()
    {
        var aviso = ReglasPlan.MensajeInvitacionEvento("Maite A.", new string('x', 300), new TimeOnly(18, 0), new TimeOnly(20, 0));
        Assert.True(aviso.Length <= ReglasPlan.MaximoLargoAviso);
        Assert.Contains("…»", aviso);
        Assert.EndsWith("hoy de 18:00 a 20:00.", aviso);
    }
}
