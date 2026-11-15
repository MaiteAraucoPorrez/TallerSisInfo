import assert from "node:assert/strict";
import { test } from "node:test";
import { MAXIMO_PENDIENTES, cuerpoReporte, enlaceMisReportes, errorDescripcionReporte } from "./reglasReportes.ts";

test("la descripción va de 10 a 500 caracteres, sin contar espacios de los bordes", () => {
  assert.equal(errorDescripcionReporte("   corto   "), "Cuéntanos un poco más: al menos 10 caracteres.");
  assert.equal(errorDescripcionReporte("El afiche tiene insultos."), "");
  assert.equal(errorDescripcionReporte("a".repeat(500)), "");
  assert.equal(errorDescripcionReporte("a".repeat(501)), "Máximo 500 caracteres.");
});

test("reportar una comunidad envía COMUNIDAD_INAPROPIADA con su id y nada más", () => {
  assert.deepEqual(cuerpoReporte("COMUNIDAD_INAPROPIADA", "  El nombre es ofensivo.  ", { comunidadId: 31 }), {
    motivo: "COMUNIDAD_INAPROPIADA",
    descripcion: "El nombre es ofensivo.",
    lugarId: null,
    comunidadId: 31,
    eventoId: null,
  });
});

test("reportar un evento envía EVENTO_INAPROPIADO con su id", () => {
  const cuerpo = cuerpoReporte("EVENTO_INAPROPIADO", "El afiche tiene insultos.", { eventoId: 8 });
  assert.equal(cuerpo.motivo, "EVENTO_INAPROPIADO");
  assert.equal(cuerpo.eventoId, 8);
  assert.equal(cuerpo.comunidadId, null);
});

test("un objeto que no corresponde al motivo no se manda", () => {
  const cuerpo = cuerpoReporte("SUGERENCIA", "Una sugerencia útil.", { lugarId: 4, comunidadId: 31, eventoId: 8 });
  assert.deepEqual([cuerpo.lugarId, cuerpo.comunidadId, cuerpo.eventoId], [null, null, null]);
  assert.equal(cuerpoReporte("LUGAR_INCORRECTO", "Abre más tarde.", { lugarId: 4, eventoId: 8 }).eventoId, null);
});

test("el aviso lleva a Mis reportes con el reporte resaltado", () => {
  assert.equal(enlaceMisReportes(12), "/perfil?pestana=mis-reportes&reporte=12");
  assert.equal(enlaceMisReportes(null), "/perfil?pestana=mis-reportes");
});

test("el tope de pendientes es 5, igual que en el backend", () => {
  assert.equal(MAXIMO_PENDIENTES, 5);
});
