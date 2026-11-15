import assert from "node:assert/strict";
import { test } from "node:test";
import {
  MENSAJE_CUENTA_ELIMINADA,
  TEXTO_ADVERTENCIA,
  confirmacionValida,
  eliminarYCerrarSesion,
  formasDeReautenticar,
  mensajeEliminarCuenta,
  puedeEliminarConContrasena,
  puedeEliminarConGoogle,
  puedeVerEliminarCuenta,
} from "./reglasEliminarCuenta.ts";

test("la confirmación exige ELIMINAR exacto: sin mayúsculas ni espacios de más no vale", () => {
  assert.equal(confirmacionValida("ELIMINAR"), true);
  for (const mal of ["", "eliminar", "Eliminar", " ELIMINAR", "ELIMINAR ", "ELIMINA", "ELIMINAR!"]) {
    assert.equal(confirmacionValida(mal), false, `«${mal}»`);
  }
});

test("el botón con contraseña se habilita solo con el texto exacto, la contraseña escrita y sin petición en curso", () => {
  assert.equal(puedeEliminarConContrasena("ELIMINAR", "secreta1", false), true);
  assert.equal(puedeEliminarConContrasena("eliminar", "secreta1", false), false);
  assert.equal(puedeEliminarConContrasena("ELIMINAR", "", false), false);
  assert.equal(puedeEliminarConContrasena("ELIMINAR", "secreta1", true), false);
});

test("con Google también hace falta el texto exacto y que no haya otra petición", () => {
  assert.equal(puedeEliminarConGoogle("ELIMINAR", false), true);
  assert.equal(puedeEliminarConGoogle("ELIMINA", false), false);
  assert.equal(puedeEliminarConGoogle("ELIMINAR", true), false);
});

test("la forma de reautenticar sale de la cuenta: contraseña, Google o ambas", () => {
  assert.deepEqual(formasDeReautenticar({ tieneContrasena: true, conGoogle: false }), { contrasena: true, google: false });
  assert.deepEqual(formasDeReautenticar({ tieneContrasena: false, conGoogle: true }), { contrasena: false, google: true });
  assert.deepEqual(formasDeReautenticar({ tieneContrasena: true, conGoogle: true }), { contrasena: true, google: true });
  // Un perfil sin esos datos se trata como cuenta con contraseña.
  assert.deepEqual(formasDeReautenticar({}), { contrasena: true, google: false });
});

test("cada código del servidor tiene su mensaje claro", () => {
  assert.match(mensajeEliminarCuenta("CONTRASENA_INCORRECTA", "x"), /contraseña no es correcta/);
  assert.match(mensajeEliminarCuenta("GOOGLE_INCORRECTO", "x"), /Google/);
  assert.equal(mensajeEliminarCuenta("SANCION_ACTIVA", "x"), "Tienes una sanción activa; contacta al administrador");
  assert.match(mensajeEliminarCuenta("NO_ELIMINABLE", "x"), /no se puede eliminar desde la app/);
  assert.match(mensajeEliminarCuenta("DEMASIADAS_SOLICITUDES", "x"), /Espera un minuto/);
});

test("un código desconocido usa el mensaje del servidor y, sin él, uno genérico", () => {
  assert.equal(mensajeEliminarCuenta("VALIDACION", "Escribe ELIMINAR para confirmar."), "Escribe ELIMINAR para confirmar.");
  assert.match(mensajeEliminarCuenta(undefined, undefined), /No pudimos eliminar tu cuenta/);
  assert.match(mensajeEliminarCuenta(undefined, "  "), /No pudimos eliminar tu cuenta/);
});

test("el administrador no ve la zona de eliminar cuenta", () => {
  assert.equal(puedeVerEliminarCuenta("ADMIN"), false);
  assert.equal(puedeVerEliminarCuenta("ESTUDIANTE"), true);
});

test("los textos del issue están tal cual", () => {
  assert.equal(
    TEXTO_ADVERTENCIA,
    "Esta acción es permanente: se borrarán tu cuenta, tus eventos y tus comunidades. Escribe ELIMINAR para confirmar.",
  );
  assert.equal(MENSAJE_CUENTA_ELIMINADA, "Tu cuenta fue eliminada.");
});

test("tras el 204 se guarda el aviso y se cierra la sesión, en ese orden", async () => {
  const pasos: string[] = [];
  const r = await eliminarYCerrarSesion(
    async () => void pasos.push("pedir"),
    (m) => pasos.push(`aviso:${m}`),
    () => pasos.push("cerrar"),
  );
  assert.deepEqual(r, { ok: true });
  assert.deepEqual(pasos, ["pedir", "aviso:Tu cuenta fue eliminada.", "cerrar"]);
});

test("si el servidor rechaza, la sesión no se cierra ni se deja aviso", async () => {
  const pasos: string[] = [];
  const falla = new Error("rechazada");
  const r = await eliminarYCerrarSesion(
    async () => {
      throw falla;
    },
    () => pasos.push("aviso"),
    () => pasos.push("cerrar"),
  );
  assert.deepEqual(r, { ok: false, error: falla });
  assert.deepEqual(pasos, []);
});
