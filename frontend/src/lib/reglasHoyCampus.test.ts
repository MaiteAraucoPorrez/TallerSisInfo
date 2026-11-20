import assert from "node:assert/strict";
import { test } from "node:test";
import { cuandoEs, estaEnElCampus, estadoEnElDia, horaDe, iniciales, lineaDeHoy } from "./reglasHoyCampus.ts";

const AHORA = "2026-11-20T10:30";
const ev = (inicio: string, fin: string, titulo = "") => ({ titulo, inicio: `2026-11-20T${inicio}:00-04:00`, fin: `2026-11-20T${fin}:00-04:00` });

test("estado de un evento respecto a ahora", () => {
  assert.equal(estadoEnElDia(ev("10:00", "11:00"), AHORA), "en-curso");
  assert.equal(estadoEnElDia(ev("11:00", "12:00"), AHORA), "proximo");
  assert.equal(estadoEnElDia(ev("08:00", "10:30"), AHORA), "terminado");
  assert.equal(estadoEnElDia(ev("10:30", "11:00"), AHORA), "en-curso");
});

test("cuándo es: en curso, en minutos, en horas o a una hora fija", () => {
  assert.equal(cuandoEs(ev("10:00", "11:00"), AHORA), "En curso · hasta las 11:00");
  assert.equal(cuandoEs(ev("10:55", "12:00"), AHORA), "En 25 min");
  assert.equal(cuandoEs(ev("11:40", "12:00"), AHORA), "En 1 h 10 min");
  assert.equal(cuandoEs(ev("12:30", "13:00"), AHORA), "En 2 h");
  assert.equal(cuandoEs(ev("18:30", "20:00"), AHORA), "A las 18:30");
});

test("la línea de hoy saca los terminados, ordena y deja los en curso antes de «Ahora»", () => {
  const l = lineaDeHoy([ev("15:00", "16:00", "c"), ev("08:00", "09:00", "viejo"), ev("10:00", "11:00", "a"), ev("11:00", "12:00", "b")], AHORA);
  assert.deepEqual(l.items.map((x) => x.evento.titulo), ["a", "b", "c"]);
  assert.equal(l.antesDeAhora, 1);
  assert.equal(l.ocultos, 0);
});

test("la línea de hoy corta en el máximo y cuenta los ocultos", () => {
  const l = lineaDeHoy([ev("11:00", "12:00"), ev("12:00", "13:00"), ev("13:00", "14:00"), ev("14:00", "15:00"), ev("15:00", "16:00")], AHORA, 3);
  assert.equal(l.items.length, 3);
  assert.equal(l.ocultos, 2);
  assert.equal(l.antesDeAhora, 0);
});

test("hora e iniciales", () => {
  assert.equal(horaDe("2026-11-20T15:05:00-04:00"), "15:05");
  assert.equal(iniciales("Ana Q."), "AQ");
  assert.equal(iniciales("diego"), "D");
  assert.equal(iniciales("  "), "?");
  assert.equal(iniciales("Álvaro Ñ."), "ÁÑ");
});

test("un lugar a 0 m está en el campus", () => {
  assert.equal(estaEnElCampus(0), true);
  assert.equal(estaEnElCampus(120), false);
});
