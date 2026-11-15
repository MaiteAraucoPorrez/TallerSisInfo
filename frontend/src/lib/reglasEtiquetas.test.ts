import assert from "node:assert/strict";
import { test } from "node:test";
import {
  MENSAJE_SIN_ETIQUETA,
  alternar,
  errorEtiquetas,
  limpiarNombreEtiqueta,
  puedeActivarse,
  quedaActivoYRecomendable,
} from "./reglasEtiquetas.ts";

test("un lugar nuevo o activo y recomendable necesita al menos una etiqueta", () => {
  assert.equal(quedaActivoYRecomendable(null), true);
  assert.equal(quedaActivoYRecomendable({ estado: "ACTIVO", recomendable: true }), true);
  assert.equal(quedaActivoYRecomendable({ estado: "EN_REVISION", recomendable: true }), false);
  assert.equal(quedaActivoYRecomendable({ estado: "ACTIVO", recomendable: false }), false);
  assert.equal(errorEtiquetas([], true), MENSAJE_SIN_ETIQUETA);
  assert.equal(errorEtiquetas([3], true), "");
  assert.equal(errorEtiquetas([], false), "");
});

test("desde la lista no se activa un recomendable sin etiquetas; uno de referencia sí", () => {
  assert.equal(puedeActivarse({ recomendable: true, etiquetas: [] }), false);
  assert.equal(puedeActivarse({ recomendable: true }), false);
  assert.equal(puedeActivarse({ recomendable: true, etiquetas: ["Comer"] }), true);
  assert.equal(puedeActivarse({ recomendable: false, etiquetas: [] }), true);
});

test("el selector marca y desmarca sin repetir", () => {
  assert.deepEqual(alternar([3], 1), [1, 3]);
  assert.deepEqual(alternar([1, 3], 3), [1]);
});

test("el nombre de una etiqueta nueva se limpia y tiene hasta 50 caracteres", () => {
  assert.equal(limpiarNombreEtiqueta("  Juegos   de mesa "), "Juegos de mesa");
  assert.equal(limpiarNombreEtiqueta("a".repeat(51)), "");
});
