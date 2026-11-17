// HU24 parte 2: cómo se prellena el formulario de HU23 con el borrador que leyó la IA.
// Funciones puras, sin React ni red ni navegador, para poder probarlas con `node --test`.
import { FOTO_EVENTO_MAX_BYTES, type CamposFormularioEvento } from "./reglasEventos.ts";
import { camposFaltantes, imagenBorrosa, type EventoLeido } from "./reglasFlyer.ts";

export type CamposEvento = CamposFormularioEvento & { descripcion: string };

/** Campos del formulario que se pueden marcar con «falta». */
export type CampoFormulario = "titulo" | "inicio" | "fin" | "lugar" | "asistencia" | "visitaMin" | "visitaMax";

export type LugarCatalogo = { id: number; nombre: string };

export const AVISO_IMAGEN_BORROSA = "La imagen se ve borrosa. Revisa los datos con cuidado o vuelve a subirla con más calidad.";
export const AVISO_LUGAR_FUERA_DEL_CATALOGO = "El evento quedará en revisión porque el lugar no está en el catálogo.";

const mismoNombre = (a: string, b: string) =>
  a.trim() !== "" && a.localeCompare(b, "es", { sensitivity: "base" }) === 0;

/**
 * Convierte el borrador en los valores del formulario. Lo que no se pudo leer (`faltantes`, o cualquier campo
 * obligatorio que llegue vacío) queda vacío y se devuelve en `marcados` para pintarlo como «falta».
 * La asistencia nula NO se convierte en FIJA: queda sin elegir y marcada.
 */
export function borradorACampos(
  evento: EventoLeido,
  faltantes: readonly string[],
  lugares: readonly LugarCatalogo[],
): { campos: CamposEvento; marcados: CampoFormulario[] } {
  const faltan = camposFaltantes(faltantes, evento);
  const marcados: CampoFormulario[] = [];

  const titulo = faltan.has("titulo") ? "" : evento.titulo ?? "";
  const inicio = faltan.has("inicio") ? "" : evento.inicio ?? "";
  const fin = faltan.has("fin") ? "" : evento.fin ?? "";
  if (!titulo) marcados.push("titulo");
  if (!inicio) marcados.push("inicio");
  if (!fin) marcados.push("fin");

  let lugarId = "";
  let lugarTexto = "";
  const lugarLeido = faltan.has("lugar") ? "" : (evento.lugarTexto ?? "").trim();
  if (lugarLeido) {
    const coincidencia = lugares.find((l) => mismoNombre(l.nombre, lugarLeido));
    if (coincidencia) lugarId = String(coincidencia.id);
    else lugarTexto = lugarLeido;
  } else {
    marcados.push("lugar");
  }

  const asistencia = evento.asistencia ?? "";
  if (!asistencia) marcados.push("asistencia");

  let visitaMin = "";
  let visitaMax = "";
  if (asistencia === "LIBRE") {
    visitaMin = evento.visitaMin == null ? "" : String(evento.visitaMin);
    visitaMax = evento.visitaMax == null ? "" : String(evento.visitaMax);
    if (!visitaMin) marcados.push("visitaMin");
    if (!visitaMax) marcados.push("visitaMax");
  }

  return {
    campos: {
      titulo,
      descripcion: evento.descripcion ?? "",
      inicio,
      fin,
      lugarId,
      lugarTexto,
      asistencia,
      visitaMin,
      visitaMax,
    },
    marcados,
  };
}

/** Los campos marcados que siguen vacíos: la marca desaparece sola cuando el estudiante corrige el campo. */
export function camposQueFaltan(marcados: readonly CampoFormulario[], campos: CamposEvento): Set<CampoFormulario> {
  const vacio: Record<CampoFormulario, boolean> = {
    titulo: !campos.titulo.trim(),
    inicio: !campos.inicio,
    fin: !campos.fin,
    lugar: !campos.lugarId && !campos.lugarTexto.trim(),
    asistencia: campos.asistencia === "",
    visitaMin: campos.asistencia === "LIBRE" && !campos.visitaMin,
    visitaMax: campos.asistencia === "LIBRE" && !campos.visitaMax,
  };
  return new Set(marcados.filter((campo) => vacio[campo]));
}

/** Avisos visibles del borrador: imagen borrosa, lo que explica el servidor y las advertencias del lector. */
export function avisosDelBorrador(lectura: {
  estado: string;
  mensaje: string | null;
  advertencias: readonly string[];
}): string[] {
  const avisos: string[] = [];
  const borrosa = imagenBorrosa(lectura.estado, lectura.mensaje);
  if (borrosa) avisos.push(AVISO_IMAGEN_BORROSA);
  else if (lectura.estado === "INCOMPLETO" && lectura.mensaje?.trim()) avisos.push(lectura.mensaje.trim());
  for (const advertencia of lectura.advertencias) {
    const texto = advertencia.trim();
    if (texto && !avisos.includes(texto)) avisos.push(texto);
  }
  return avisos;
}

/** Aviso mientras el lugar escrito no coincida con el catálogo (el evento quedará pendiente). */
export function avisoLugarFueraDelCatalogo(campos: Pick<CamposEvento, "lugarId" | "lugarTexto">): string | null {
  return !campos.lugarId && campos.lugarTexto.trim() ? AVISO_LUGAR_FUERA_DEL_CATALOGO : null;
}

// ---- Foto del flyer como foto del evento (el flyer admite 8 MB; la foto del evento, 2 MB) ----

export type IntentoReduccion = { ladoMax: number; calidad: number };

/** De más a menos calidad: se usa el primero que deje la foto en 2 MB o menos. */
export const INTENTOS_REDUCCION: readonly IntentoReduccion[] = [
  { ladoMax: 2048, calidad: 0.85 },
  { ladoMax: 1600, calidad: 0.8 },
  { ladoMax: 1280, calidad: 0.75 },
  { ladoMax: 1024, calidad: 0.7 },
  { ladoMax: 800, calidad: 0.6 },
  { ladoMax: 640, calidad: 0.5 },
];

export type PlanFoto = "usar" | "reducir" | "descartar";

const TIPOS_FOTO = ["image/jpeg", "image/png"];

/** Qué hacer con la imagen del borrador: usarla tal cual, intentar reducirla o descartarla. */
export function planFoto(foto: { type: string; size: number }, maxBytes = FOTO_EVENTO_MAX_BYTES): PlanFoto {
  if (foto.size === 0 || !TIPOS_FOTO.includes(foto.type)) return "descartar";
  return foto.size <= maxBytes ? "usar" : "reducir";
}

/** Tamaño reducido para que el lado mayor no pase de `ladoMax`; nunca agranda. */
export function dimensionesReducidas(ancho: number, alto: number, ladoMax: number): { ancho: number; alto: number } {
  const mayor = Math.max(ancho, alto);
  if (mayor <= ladoMax) return { ancho, alto };
  const escala = ladoMax / mayor;
  return { ancho: Math.max(1, Math.round(ancho * escala)), alto: Math.max(1, Math.round(alto * escala)) };
}

/** Prueba los intentos en orden y devuelve el primero que cabe; null si ninguno. `codificar` es quien toca el navegador. */
export async function buscarReduccion<T extends { size: number }>(
  codificar: (intento: IntentoReduccion) => Promise<T | null>,
  maxBytes = FOTO_EVENTO_MAX_BYTES,
): Promise<T | null> {
  for (const intento of INTENTOS_REDUCCION) {
    const resultado = await codificar(intento);
    if (resultado && resultado.size > 0 && resultado.size <= maxBytes) return resultado;
  }
  return null;
}

/** «afiche.png» → «afiche.jpg». */
export const nombreJpeg = (nombre: string): string => `${nombre.replace(/\.[^./\\]+$/, "") || "afiche"}.jpg`;

const mb = (bytes: number) => (bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, "");

export const avisoFotoReducida = (antes: number, despues: number): string =>
  `La foto del afiche pesaba ${mb(antes)} MB y la reducimos a ${mb(despues)} MB para usarla en el evento. Puedes quitarla.`;

export const avisoFotoDescartada = (antes: number): string =>
  antes === 0
    ? "No pudimos usar la imagen del afiche como foto del evento. Puedes subir otra."
    : `La foto del afiche pesa ${mb(antes)} MB y no se pudo reducir a 2 MB, así que no se usará como foto del evento. Puedes subir otra.`;
