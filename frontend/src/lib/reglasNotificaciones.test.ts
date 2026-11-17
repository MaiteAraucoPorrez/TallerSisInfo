import assert from "node:assert/strict";
import { test } from "node:test";
import { PRESENTACION_GENERICA, presentacionNotificacion } from "./reglasNotificaciones.ts";

test("un evento aprobado se muestra con su título, ícono de calendario y estilo de éxito", () => {
  assert.deepEqual(presentacionNotificacion("EVENTO_APROBADO"), {
    titulo: "Tu evento fue aprobado",
    clase: "reporte",
    icono: "calendario",
  });
});

test("un evento rechazado se muestra con el estilo de aviso", () => {
  assert.deepEqual(presentacionNotificacion("EVENTO_RECHAZADO"), {
    titulo: "Tu evento fue rechazado",
    clase: "advertencia",
    icono: "calendario",
  });
});

test("la advertencia de sanción conserva su presentación", () => {
  assert.deepEqual(presentacionNotificacion("SANCION_ADVERTENCIA"), {
    titulo: "Advertencia del administrador",
    clase: "advertencia",
    icono: "bandera",
  });
});

test("un tipo desconocido usa la presentación genérica y no lanza error", () => {
  assert.deepEqual(presentacionNotificacion("EVENTO_BAJADO"), PRESENTACION_GENERICA);
  assert.deepEqual(presentacionNotificacion("TIPO_NUEVO_DE_OTRA_HU"), PRESENTACION_GENERICA);
  assert.deepEqual(presentacionNotificacion(""), PRESENTACION_GENERICA);
  assert.equal(PRESENTACION_GENERICA.titulo, "Nuevo aviso");
});

test("los nombres heredados de Object no se toman por tipos conocidos", () => {
  assert.deepEqual(presentacionNotificacion("constructor"), PRESENTACION_GENERICA);
  assert.deepEqual(presentacionNotificacion("toString"), PRESENTACION_GENERICA);
});
