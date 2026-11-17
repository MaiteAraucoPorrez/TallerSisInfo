import assert from "node:assert/strict";
import { test } from "node:test";
import {
  ahoraBoliviaParaFormulario,
  fechaBoliviaParaApi,
  formDataEvento,
  presentacionEvento,
  validarFormularioEvento,
  validarFotoEvento,
} from "./reglasEventos.ts";

const CAMPOS = {
  titulo: "Feria de ciencias",
  inicio: "2026-11-17T13:00",
  fin: "2026-11-17T14:00",
  lugarId: "3",
  lugarTexto: "",
  asistencia: "FIJA" as const,
  visitaMin: "",
  visitaMax: "",
};

test("valida los obligatorios del formulario sin bloquear fechas que el servidor enviará a revisión", () => {
  assert.equal(validarFormularioEvento(CAMPOS), null);
  assert.match(validarFormularioEvento({ ...CAMPOS, titulo: " " }) ?? "", /título/i);
  assert.match(validarFormularioEvento({ ...CAMPOS, lugarId: "", lugarTexto: "" }) ?? "", /lugar/i);
});

test("exige un rango coherente de visita solo para asistencia libre", () => {
  assert.match(validarFormularioEvento({ ...CAMPOS, asistencia: "LIBRE" }) ?? "", /tiempo mínimo/i);
  assert.equal(validarFormularioEvento({ ...CAMPOS, asistencia: "LIBRE", visitaMin: "15", visitaMax: "60" }), null);
  assert.match(validarFormularioEvento({ ...CAMPOS, asistencia: "LIBRE", visitaMin: "60", visitaMax: "15" }) ?? "", /tiempo de visita/i);
});

test("adjunta el offset fijo de Bolivia y formatea la hora actual en ese huso", () => {
  assert.equal(fechaBoliviaParaApi(CAMPOS.inicio), "2026-11-17T13:00:00-04:00");
  assert.equal(ahoraBoliviaParaFormulario(new Date("2026-11-17T16:00:00.000Z")), "2026-11-17T12:00");
});

test("comprueba tipo, tamaño y contenido opcional de la foto", () => {
  assert.equal(validarFotoEvento({ name: "afiche.jpg", type: "image/jpeg", size: 10 }), null);
  assert.match(validarFotoEvento({ name: "afiche.gif", type: "image/gif", size: 10 }) ?? "", /JPG o PNG/);
  assert.match(validarFotoEvento({ name: "afiche.png", type: "image/png", size: 2 * 1024 * 1024 + 1 }) ?? "", /2 MB/);
});

test("construye multipart con valores completos y omite fotos vacías", () => {
  const datos = formDataEvento({ ...CAMPOS, descripcion: "Descripción" }, null, "Biblioteca");

  assert.equal(datos.get("Titulo"), CAMPOS.titulo);
  assert.equal(datos.get("Inicio"), "2026-11-17T13:00:00-04:00");
  assert.equal(datos.get("LugarTexto"), "Biblioteca");
  assert.equal(datos.get("Foto"), null);
});

const AHORA = Date.parse("2026-11-17T12:00:00-04:00");
const FUTURO = { inicio: "2026-11-18T10:00:00-04:00", fin: "2026-11-18T12:00:00-04:00" };
const PASADO = { inicio: "2026-11-17T08:00:00-04:00", fin: "2026-11-17T10:00:00-04:00" };

test("solo se puede editar un evento PENDIENTE o PUBLICADO que no empezó", () => {
  assert.equal(presentacionEvento("PENDIENTE", FUTURO.inicio, FUTURO.fin, AHORA).editable, true);
  assert.equal(presentacionEvento("PUBLICADO", FUTURO.inicio, FUTURO.fin, AHORA).editable, true);
  assert.equal(presentacionEvento("RECHAZADO", FUTURO.inicio, FUTURO.fin, AHORA).editable, false);
  assert.equal(presentacionEvento("CANCELADO", FUTURO.inicio, FUTURO.fin, AHORA).editable, false);
});

test("un evento que ya empezó no se edita ni se cancela", () => {
  const p = presentacionEvento("PUBLICADO", "2026-11-17T10:00:00-04:00", "2026-11-17T14:00:00-04:00", AHORA);
  assert.equal(p.editable, false);
  assert.equal(p.cancelable, false);
  assert.equal(p.etiqueta, "Publicado");
});

test("cancelar sigue disponible salvo en CANCELADO o si ya empezó", () => {
  assert.equal(presentacionEvento("RECHAZADO", FUTURO.inicio, FUTURO.fin, AHORA).cancelable, true);
  assert.equal(presentacionEvento("CANCELADO", FUTURO.inicio, FUTURO.fin, AHORA).cancelable, false);
});

test("un PENDIENTE cuyo fin ya pasó se muestra como Vencido", () => {
  const p = presentacionEvento("PENDIENTE", PASADO.inicio, PASADO.fin, AHORA);
  assert.equal(p.clave, "VENCIDO");
  assert.equal(p.etiqueta, "Vencido");
  assert.equal(p.editable, false);
  assert.equal(p.cancelable, false);
});

test("un PENDIENTE vigente sigue en revisión y los demás estados no se marcan Vencido", () => {
  assert.equal(presentacionEvento("PENDIENTE", FUTURO.inicio, FUTURO.fin, AHORA).etiqueta, "En revisión");
  assert.equal(presentacionEvento("PUBLICADO", PASADO.inicio, PASADO.fin, AHORA).clave, "PUBLICADO");
  assert.equal(presentacionEvento("RECHAZADO", PASADO.inicio, PASADO.fin, AHORA).etiqueta, "Rechazado");
});
