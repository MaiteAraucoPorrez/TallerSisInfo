import assert from "node:assert/strict";
import { test } from "node:test";
import {
  AVISO_IMAGEN_BORROSA,
  AVISO_LUGAR_FUERA_DEL_CATALOGO,
  INTENTOS_REDUCCION,
  avisoFotoDescartada,
  avisoFotoReducida,
  avisoLugarFueraDelCatalogo,
  avisosDelBorrador,
  borradorACampos,
  buscarReduccion,
  camposQueFaltan,
  dimensionesReducidas,
  nombreJpeg,
  planFoto,
  type CamposEvento,
} from "./reglasBorradorFlyer.ts";
import { FOTO_EVENTO_MAX_BYTES, formDataEvento, validarFormularioEvento } from "./reglasEventos.ts";
import { MENSAJE_BORROSA, type EventoLeido } from "./reglasFlyer.ts";

const LUGARES = [
  { id: 3, nombre: "Biblioteca Central" },
  { id: 7, nombre: "Auditorio" },
];

const COMPLETO: EventoLeido = {
  titulo: "Feria de ciencias",
  descripcion: "Stands de todas las carreras",
  inicio: "2026-11-20T08:00",
  fin: "2026-11-20T13:00",
  lugarTexto: "Auditorio",
  asistencia: "LIBRE",
  visitaMin: 15,
  visitaMax: 30,
};

const MB = 1024 * 1024;

// ---- campos «falta» ----

test("un borrador completo prellena todo y no marca nada", () => {
  const { campos, marcados } = borradorACampos(COMPLETO, [], LUGARES);

  assert.deepEqual(marcados, []);
  assert.equal(campos.titulo, "Feria de ciencias");
  assert.equal(campos.inicio, "2026-11-20T08:00");
  assert.equal(campos.lugarId, "7");
  assert.equal(campos.lugarTexto, "");
  assert.equal(campos.asistencia, "LIBRE");
  assert.equal(campos.visitaMin, "15");
  assert.equal(campos.visitaMax, "30");
});

test("los campos de `faltantes` quedan vacíos y marcados, aunque el borrador traiga algo", () => {
  const { campos, marcados } = borradorACampos(COMPLETO, ["titulo", "fin"], LUGARES);

  assert.equal(campos.titulo, "");
  assert.equal(campos.fin, "");
  assert.equal(campos.inicio, "2026-11-20T08:00");
  assert.deepEqual(marcados, ["titulo", "fin"]);
});

test("un obligatorio que llega vacío se marca aunque el servidor no lo liste", () => {
  const { campos, marcados } = borradorACampos({ ...COMPLETO, lugarTexto: null, inicio: null }, [], LUGARES);

  assert.equal(campos.inicio, "");
  assert.equal(campos.lugarId, "");
  assert.equal(campos.lugarTexto, "");
  assert.deepEqual(marcados.sort(), ["inicio", "lugar"]);
});

test("un lugar que no está en el catálogo va como texto y el del catálogo se elige sin importar tildes ni mayúsculas", () => {
  assert.deepEqual(
    (({ lugarId, lugarTexto }) => ({ lugarId, lugarTexto }))(
      borradorACampos({ ...COMPLETO, lugarTexto: "Patio junto al coliseo" }, [], LUGARES).campos,
    ),
    { lugarId: "", lugarTexto: "Patio junto al coliseo" },
  );
  assert.equal(borradorACampos({ ...COMPLETO, lugarTexto: "  biblioteca central " }, [], LUGARES).campos.lugarId, "3");
  assert.equal(borradorACampos({ ...COMPLETO, lugarTexto: "AUDITÓRIO" }, [], LUGARES).campos.lugarId, "7");
});

test("LIBRE sin rango de visita marca el mínimo y el máximo; FIJA no pide rango", () => {
  const libre = borradorACampos({ ...COMPLETO, visitaMin: null, visitaMax: null }, [], LUGARES);
  assert.deepEqual(libre.marcados, ["visitaMin", "visitaMax"]);

  const fija = borradorACampos({ ...COMPLETO, asistencia: "FIJA", visitaMin: null, visitaMax: null }, [], LUGARES);
  assert.deepEqual(fija.marcados, []);
  assert.equal(fija.campos.asistencia, "FIJA");
});

test("la marca desaparece cuando se corrige el campo y vuelve si se vacía", () => {
  const { campos, marcados } = borradorACampos({ ...COMPLETO, titulo: null }, ["titulo"], LUGARES);
  assert.deepEqual([...camposQueFaltan(marcados, campos)], ["titulo"]);

  const corregido: CamposEvento = { ...campos, titulo: "Feria" };
  assert.equal(camposQueFaltan(marcados, corregido).size, 0);

  assert.equal(camposQueFaltan(marcados, { ...corregido, titulo: "   " }).has("titulo"), true);
});

test("la marca de lugar se quita con el catálogo o con texto, y las de visita al elegir FIJA", () => {
  const sinLugar = borradorACampos({ ...COMPLETO, lugarTexto: null, visitaMin: null, visitaMax: null }, ["lugar"], LUGARES);
  assert.equal(camposQueFaltan(sinLugar.marcados, sinLugar.campos).has("lugar"), true);
  assert.equal(camposQueFaltan(sinLugar.marcados, { ...sinLugar.campos, lugarId: "3" }).has("lugar"), false);
  assert.equal(camposQueFaltan(sinLugar.marcados, { ...sinLugar.campos, lugarTexto: "Patio" }).has("lugar"), false);

  assert.equal(camposQueFaltan(sinLugar.marcados, sinLugar.campos).has("visitaMin"), true);
  assert.equal(camposQueFaltan(sinLugar.marcados, { ...sinLugar.campos, asistencia: "FIJA" }).has("visitaMin"), false);
});

// ---- asistencia nula ----

test("una asistencia nula NO se convierte en FIJA: queda sin elegir y marcada", () => {
  const { campos, marcados } = borradorACampos({ ...COMPLETO, asistencia: null, visitaMin: null, visitaMax: null }, [], LUGARES);

  assert.equal(campos.asistencia, "");
  assert.deepEqual(marcados, ["asistencia"]);
  assert.equal(campos.visitaMin, "");
  assert.equal(campos.visitaMax, "");
  assert.equal(camposQueFaltan(marcados, campos).has("asistencia"), true);
  assert.equal(camposQueFaltan(marcados, { ...campos, asistencia: "FIJA" }).has("asistencia"), false);
});

test("sin elegir la asistencia no se puede enviar; al elegirla sí", () => {
  const { campos } = borradorACampos({ ...COMPLETO, asistencia: null, visitaMin: null, visitaMax: null }, [], LUGARES);

  assert.match(validarFormularioEvento(campos) ?? "", /Elige si hay que llegar a la hora/);
  assert.equal(validarFormularioEvento({ ...campos, asistencia: "FIJA" }), null);
});

// ---- avisos ----

test("la imagen borrosa (INCOMPLETO con el mensaje del servidor) se avisa y se suman las advertencias sin repetir", () => {
  const avisos = avisosDelBorrador({
    estado: "INCOMPLETO",
    mensaje: MENSAJE_BORROSA,
    advertencias: ["Hay varios eventos en el afiche", "  ", "Hay varios eventos en el afiche"],
  });

  assert.deepEqual(avisos, [AVISO_IMAGEN_BORROSA, "Hay varios eventos en el afiche"]);
});

test("INCOMPLETO por otra causa muestra la explicación del servidor; COMPLETO sin advertencias no muestra nada", () => {
  assert.deepEqual(
    avisosDelBorrador({ estado: "INCOMPLETO", mensaje: "La fecha del afiche ya pasó.", advertencias: [] }),
    ["La fecha del afiche ya pasó."],
  );
  assert.deepEqual(avisosDelBorrador({ estado: "COMPLETO", mensaje: null, advertencias: [] }), []);
  assert.deepEqual(avisosDelBorrador({ estado: "COMPLETO", mensaje: MENSAJE_BORROSA, advertencias: [] }), []);
});

test("el aviso del lugar aparece solo mientras el lugar no coincida con el catálogo", () => {
  assert.equal(avisoLugarFueraDelCatalogo({ lugarId: "", lugarTexto: "Patio" }), AVISO_LUGAR_FUERA_DEL_CATALOGO);
  assert.equal(avisoLugarFueraDelCatalogo({ lugarId: "3", lugarTexto: "" }), null);
  assert.equal(avisoLugarFueraDelCatalogo({ lugarId: "", lugarTexto: "  " }), null);
});

// ---- foto ----

test("la foto que cabe en 2 MB se usa tal cual; la más grande se intenta reducir", () => {
  assert.equal(planFoto({ type: "image/jpeg", size: FOTO_EVENTO_MAX_BYTES }), "usar");
  assert.equal(planFoto({ type: "image/png", size: 500 }), "usar");
  assert.equal(planFoto({ type: "image/jpeg", size: FOTO_EVENTO_MAX_BYTES + 1 }), "reducir");
  assert.equal(planFoto({ type: "image/png", size: 8 * MB }), "reducir");
});

test("una imagen vacía o de otro tipo se descarta", () => {
  assert.equal(planFoto({ type: "image/jpeg", size: 0 }), "descartar");
  assert.equal(planFoto({ type: "image/webp", size: 100 }), "descartar");
  assert.equal(planFoto({ type: "application/pdf", size: 100 }), "descartar");
});

test("las dimensiones se reducen sin deformar y nunca se agrandan", () => {
  assert.deepEqual(dimensionesReducidas(4000, 3000, 2000), { ancho: 2000, alto: 1500 });
  assert.deepEqual(dimensionesReducidas(3000, 6000, 1500), { ancho: 750, alto: 1500 });
  assert.deepEqual(dimensionesReducidas(800, 600, 2048), { ancho: 800, alto: 600 });
  assert.deepEqual(dimensionesReducidas(10000, 1, 100), { ancho: 100, alto: 1 });
});

test("los intentos de reducción bajan en tamaño y calidad", () => {
  for (let i = 1; i < INTENTOS_REDUCCION.length; i++) {
    assert.ok(INTENTOS_REDUCCION[i].ladoMax < INTENTOS_REDUCCION[i - 1].ladoMax);
    assert.ok(INTENTOS_REDUCCION[i].calidad < INTENTOS_REDUCCION[i - 1].calidad);
  }
});

test("la reducción se queda con el primer intento que cabe en 2 MB", async () => {
  const llamados: number[] = [];
  const tamanos = [5 * MB, 3 * MB, 1.5 * MB, 1 * MB];
  const resultado = await buscarReduccion(async ({ ladoMax }) => {
    llamados.push(ladoMax);
    return { size: tamanos[llamados.length - 1] };
  });

  assert.deepEqual(resultado, { size: 1.5 * MB });
  assert.equal(llamados.length, 3);
});

test("si ningún intento cabe, o el navegador no puede codificar, la foto se descarta (null)", async () => {
  let intentos = 0;
  assert.equal(
    await buscarReduccion(async () => {
      intentos++;
      return { size: 9 * MB };
    }),
    null,
  );
  assert.equal(intentos, INTENTOS_REDUCCION.length);
  assert.equal(await buscarReduccion(async () => null), null);
  assert.equal(await buscarReduccion(async () => ({ size: 0 })), null);
});

test("el nombre de la foto reducida pasa a .jpg y los avisos dicen cuánto pesaba", () => {
  assert.equal(nombreJpeg("afiche.png"), "afiche.jpg");
  assert.equal(nombreJpeg("mi.afiche.final.PNG"), "mi.afiche.final.jpg");
  assert.equal(nombreJpeg("sinextension"), "sinextension.jpg");
  assert.equal(nombreJpeg(""), "afiche.jpg");
  assert.match(avisoFotoReducida(6.5 * MB, 1.9 * MB), /6\.5 MB.*1\.9 MB/);
  assert.match(avisoFotoDescartada(7 * MB), /7 MB.*no se pudo reducir a 2 MB/);
  assert.match(avisoFotoDescartada(0), /No pudimos usar la imagen/);
});

// ---- origen ----

test("el formulario envía Origen solo cuando se le indica (al crear)", () => {
  const campos = { ...borradorACampos(COMPLETO, [], LUGARES).campos };

  assert.equal(formDataEvento(campos, null, "Auditorio", "FLYER").get("Origen"), "FLYER");
  assert.equal(formDataEvento(campos, null, "Auditorio", "FORMULARIO").get("Origen"), "FORMULARIO");
  assert.equal(formDataEvento(campos, null, "Auditorio").get("Origen"), null);
});
