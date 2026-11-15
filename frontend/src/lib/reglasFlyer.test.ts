import assert from "node:assert/strict";
import { test } from "node:test";
import {
  FLYER_BYTES_MAX,
  FLYER_TEXTO_MAX,
  MENSAJE_BORROSA,
  camposFaltantes,
  esPdf,
  formatearFechaHora,
  imagenBorrosa,
  mensajeErrorFlyer,
  sePuedeUsarBorrador,
  textoAsistencia,
  textoVisita,
  validarArchivoFlyer,
  validarTextoFlyer,
  type EventoLeido,
} from "./reglasFlyer.ts";

const evento = (cambios: Partial<EventoLeido> = {}): EventoLeido => ({
  titulo: "Feria de Ciencias",
  descripcion: null,
  inicio: "2026-11-20T08:00",
  fin: "2026-11-20T13:00",
  lugarTexto: "Patio central",
  asistencia: "LIBRE",
  visitaMin: 15,
  visitaMax: 30,
  ...cambios,
});

test("acepta JPG, PNG y PDF de hasta 8 MB exactos", () => {
  for (const type of ["image/jpeg", "image/png", "application/pdf"]) {
    assert.equal(validarArchivoFlyer({ name: "a", type, size: 1000 }), null, type);
  }
  assert.equal(validarArchivoFlyer({ name: "a.png", type: "image/png", size: FLYER_BYTES_MAX }), null);
});

test("rechaza WebP, otros tipos, vacíos y más de 8 MB antes de enviar", () => {
  assert.match(validarArchivoFlyer({ name: "a.webp", type: "image/webp", size: 10 })!, /JPG, PNG o PDF/);
  assert.match(validarArchivoFlyer({ name: "a.docx", type: "application/msword", size: 10 })!, /JPG, PNG o PDF/);
  assert.match(validarArchivoFlyer({ name: "a.png", type: "image/png", size: 0 })!, /vacío/);
  assert.match(validarArchivoFlyer({ name: "a.png", type: "image/png", size: FLYER_BYTES_MAX + 1 })!, /máximo es 8 MB/);
});

test("un tipo vacío se acepta solo si la extensión es de un formato permitido", () => {
  assert.equal(validarArchivoFlyer({ name: "afiche.PDF", type: "", size: 10 }), null);
  assert.equal(validarArchivoFlyer({ name: "foto.jpeg", type: "", size: 10 }), null);
  assert.notEqual(validarArchivoFlyer({ name: "foto.webp", type: "", size: 10 }), null);
  assert.equal(esPdf({ name: "x.pdf", type: "" }), true);
  assert.equal(esPdf({ name: "x.png", type: "image/png" }), false);
});

test("el texto pegado exige contenido, un mínimo y un máximo", () => {
  assert.match(validarTextoFlyer("   ")!, /Pega/);
  assert.match(validarTextoFlyer("hola")!, /muy corto/);
  assert.equal(validarTextoFlyer("Charla de IA el viernes a las 18:00"), null);
  assert.equal(validarTextoFlyer("a".repeat(FLYER_TEXTO_MAX)), null);
  assert.match(validarTextoFlyer("a".repeat(FLYER_TEXTO_MAX + 1))!, /máximo/);
});

test("cada código de error del servidor tiene un mensaje propio", () => {
  for (const codigo of ["VALIDACION", "ARCHIVO_NO_ADMITIDO", "PDF_MUCHAS_PAGINAS", "PDF_PROTEGIDO", "PDF_INVALIDO", "SOLICITUD_NO_PERMITIDA", "DEMASIADAS_SOLICITUDES", "LECTOR_NO_DISPONIBLE"]) {
    assert.notEqual(mensajeErrorFlyer(codigo, "mensaje del servidor"), "mensaje del servidor", codigo);
  }
  assert.match(mensajeErrorFlyer("PDF_MUCHAS_PAGINAS", undefined), /2 páginas/);
  assert.match(mensajeErrorFlyer("PDF_PROTEGIDO", undefined), /contraseña/);
});

test("un código desconocido usa el mensaje del servidor y, sin él, uno genérico", () => {
  assert.equal(mensajeErrorFlyer("OTRO", "Hubo un problema"), "Hubo un problema");
  assert.match(mensajeErrorFlyer(undefined, undefined), /llena el formulario a mano/);
  assert.match(mensajeErrorFlyer(undefined, "   "), /llena el formulario a mano/);
});

test("los campos faltantes juntan lo que dice el servidor y lo obligatorio que llegó vacío", () => {
  assert.deepEqual([...camposFaltantes([], evento())], []);
  assert.deepEqual([...camposFaltantes(["fin"], evento({ fin: null }))], ["fin"]);
  assert.deepEqual([...camposFaltantes([], evento({ titulo: null, lugarTexto: null }))].sort(), ["lugar", "titulo"]);
  assert.equal(camposFaltantes([], evento({ descripcion: null })).has("descripcion"), false);
});

test("las fechas se muestran sin pasar por Date (no se corre la hora)", () => {
  assert.equal(formatearFechaHora("2026-11-20T08:00"), "20/11/2026 08:00");
  assert.equal(formatearFechaHora("2026-01-05T23:59:00"), "05/01/2026 23:59");
  assert.equal(formatearFechaHora(null), "");
  assert.equal(formatearFechaHora("mañana"), "");
});

test("la visita solo se muestra con asistencia LIBRE y rango completo", () => {
  assert.equal(textoVisita(evento()), "15 a 30 minutos");
  assert.equal(textoVisita(evento({ asistencia: "FIJA", visitaMin: null, visitaMax: null })), "");
  assert.equal(textoVisita(evento({ visitaMax: null })), "");
  assert.match(textoAsistencia("FIJA"), /Fija/);
  assert.match(textoAsistencia("LIBRE"), /Libre/);
  assert.equal(textoAsistencia(null), "");
});

test("la imagen borrosa se reconoce por INCOMPLETO con el mensaje del servidor", () => {
  assert.equal(imagenBorrosa("INCOMPLETO", MENSAJE_BORROSA), true);
  assert.equal(imagenBorrosa("INCOMPLETO", "No pudimos leer todos los datos del evento. Completa los que faltan."), false);
  assert.equal(imagenBorrosa("COMPLETO", MENSAJE_BORROSA), false);
});

test("NO_ENTENDIDO no ofrece usar el borrador; COMPLETO e INCOMPLETO sí", () => {
  assert.equal(sePuedeUsarBorrador("NO_ENTENDIDO"), false);
  assert.equal(sePuedeUsarBorrador("COMPLETO"), true);
  assert.equal(sePuedeUsarBorrador("INCOMPLETO"), true);
});
