import assert from "node:assert/strict";
import { test } from "node:test";
import { textoPorQueTeGusta } from "./reglasRecomendaciones.ts";

test("con un motivo arma la frase en minúscula", () => {
  assert.equal(textoPorQueTeGusta(["Deporte"]), "Porque te gustan los planes de deporte");
});

test("con dos motivos los une con «y»", () => {
  assert.equal(textoPorQueTeGusta(["Deporte", "Aire libre"]), "Porque te gustan los planes de deporte y aire libre");
});

test("las siglas se quedan en mayúscula", () => {
  assert.equal(textoPorQueTeGusta(["UCB", "Música"]), "Porque te gustan los planes de UCB y música");
});

test("sin motivos no se muestra nada", () => {
  assert.equal(textoPorQueTeGusta([]), null);
  assert.equal(textoPorQueTeGusta(undefined), null);
  assert.equal(textoPorQueTeGusta(null), null);
  assert.equal(textoPorQueTeGusta(["  "]), null);
});
