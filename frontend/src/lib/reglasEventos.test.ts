import assert from "node:assert/strict";
import { test } from "node:test";
import {
  ahoraBoliviaParaFormulario,
  fechaBoliviaParaApi,
  formDataEvento,
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
