import assert from "node:assert/strict";
import { test } from "node:test";
import {
  FOTO_SIN_TOCAR,
  accionesFoto,
  archivoNuevo,
  datosDeFoto,
  deshacerQuitar,
  elegirFoto,
  quitarFotoActual,
  vistaFoto,
  type EstadoFoto,
} from "./reglasFotoEvento.ts";
import { formDataEvento } from "./reglasEventos.ts";

const NUEVA = { name: "nueva.jpg" };

test("sin tocar la foto no se envía nada", () => {
  assert.deepEqual(datosDeFoto(FOTO_SIN_TOCAR), { foto: null, quitarFoto: false });
  assert.equal(archivoNuevo(FOTO_SIN_TOCAR), null);
});

test("elegir un archivo lo manda como foto nueva y no pide quitar", () => {
  const estado = elegirFoto(NUEVA);

  assert.deepEqual(datosDeFoto(estado), { foto: NUEVA, quitarFoto: false });
  assert.equal(archivoNuevo(estado), NUEVA);
});

test("quitar la foto actual envía la orden de quitar y ninguna foto", () => {
  const estado = quitarFotoActual<typeof NUEVA>(FOTO_SIN_TOCAR);

  assert.equal(estado.tipo, "quitar");
  assert.deepEqual(datosDeFoto(estado), { foto: null, quitarFoto: true });
});

test("deshacer vuelve a conservar la foto actual antes de guardar", () => {
  const quitada = quitarFotoActual<typeof NUEVA>(FOTO_SIN_TOCAR);

  assert.deepEqual(deshacerQuitar(quitada), FOTO_SIN_TOCAR);
  assert.deepEqual(datosDeFoto(deshacerQuitar(quitada)), { foto: null, quitarFoto: false });
  // deshacer sin haber quitado no cambia nada, ni siquiera una foto nueva
  assert.deepEqual(deshacerQuitar(FOTO_SIN_TOCAR), FOTO_SIN_TOCAR);
  assert.equal(archivoNuevo(deshacerQuitar(elegirFoto(NUEVA))), NUEVA);
});

test("elegir un archivo después de quitar manda solo la foto nueva (nunca las dos órdenes)", () => {
  const quitada = quitarFotoActual<typeof NUEVA>(FOTO_SIN_TOCAR);
  const estado: EstadoFoto<typeof NUEVA> = elegirFoto(NUEVA);

  assert.equal(quitada.tipo, "quitar");
  assert.deepEqual(datosDeFoto(estado), { foto: NUEVA, quitarFoto: false });
});

test("«quitar» con una foto nueva elegida solo la descarta y conserva la actual", () => {
  assert.deepEqual(quitarFotoActual(elegirFoto(NUEVA)), FOTO_SIN_TOCAR);
  assert.equal(elegirFoto<typeof NUEVA>(null).tipo, "sinTocar");
});

test("los botones de la edición dependen del estado y de si hay foto actual", () => {
  assert.deepEqual(accionesFoto(FOTO_SIN_TOCAR, true), { quitar: true, deshacer: false, descartarNueva: false });
  assert.deepEqual(accionesFoto(FOTO_SIN_TOCAR, false), { quitar: false, deshacer: false, descartarNueva: false });
  assert.deepEqual(accionesFoto(quitarFotoActual(FOTO_SIN_TOCAR), true), { quitar: false, deshacer: true, descartarNueva: false });
  assert.deepEqual(accionesFoto(elegirFoto(NUEVA), true), { quitar: false, deshacer: false, descartarNueva: true });
  assert.deepEqual(accionesFoto(elegirFoto(NUEVA), false), { quitar: false, deshacer: false, descartarNueva: true });
});

test("la vista previa muestra la actual, la nueva, el aviso de quitar o nada", () => {
  assert.equal(vistaFoto(FOTO_SIN_TOCAR, true), "actual");
  assert.equal(vistaFoto(FOTO_SIN_TOCAR, false), "ninguna");
  assert.equal(vistaFoto(elegirFoto(NUEVA), true), "nueva");
  assert.equal(vistaFoto(elegirFoto(NUEVA), false), "nueva");
  assert.equal(vistaFoto(quitarFotoActual(FOTO_SIN_TOCAR), true), "quitada");
});

test("el formulario envía QuitarFoto solo cuando se pidió quitarla", () => {
  const campos = {
    titulo: "Feria",
    descripcion: "",
    inicio: "2026-11-20T08:00",
    fin: "2026-11-20T13:00",
    lugarId: "3",
    lugarTexto: "",
    asistencia: "FIJA" as const,
    visitaMin: "",
    visitaMax: "",
  };

  assert.equal(formDataEvento(campos, null, "Auditorio", undefined, true).get("QuitarFoto"), "true");
  assert.equal(formDataEvento(campos, null, "Auditorio", undefined, false).get("QuitarFoto"), null);
  assert.equal(formDataEvento(campos, null, "Auditorio").get("QuitarFoto"), null);
  assert.equal(formDataEvento(campos, null, "Auditorio", undefined, true).get("Foto"), null);
});
