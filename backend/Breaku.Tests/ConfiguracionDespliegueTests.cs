using Breaku.Presentation.Despliegue;

namespace Breaku.Tests;

public class ConfiguracionDespliegueTests
{
    [Fact]
    public void Un_solo_origen_como_antes()
    {
        Assert.Equal(["http://localhost:3000"], ConfiguracionDespliegue.Origenes("http://localhost:3000"));
    }

    [Fact]
    public void Varios_origenes_separados_por_coma_o_punto_y_coma_sin_barra_final()
    {
        Assert.Equal(
            ["http://localhost:3000", "https://breaku.vercel.app", "https://otro.vercel.app"],
            ConfiguracionDespliegue.Origenes(" http://localhost:3000/ , https://breaku.vercel.app;https://otro.vercel.app "));
    }

    [Fact]
    public void Sin_repetidos_ni_vacios()
    {
        Assert.Equal(
            ["https://breaku.vercel.app"],
            ConfiguracionDespliegue.Origenes("https://breaku.vercel.app,, https://BREAKU.vercel.app/ ,"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ; ")]
    public void Vacio_no_da_ningun_origen(string? valor)
    {
        Assert.Empty(ConfiguracionDespliegue.Origenes(valor));
    }

    [Theory]
    [InlineData("true", null, true)]
    [InlineData(null, "TRUE", true)]
    [InlineData(null, null, false)]
    [InlineData("false", "", false)]
    [InlineData("1", null, false)]
    public void Proxy_solo_con_RENDER_o_DETRAS_DE_PROXY_en_true(string? render, string? detras, bool esperado)
    {
        Assert.Equal(esperado, ConfiguracionDespliegue.DetrasDeProxy(render, detras));
    }
}
