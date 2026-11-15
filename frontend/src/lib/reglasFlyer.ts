// HU24-FE: reglas de la pantalla «Subir flyer». Funciones puras, sin React ni red, para poder probarlas.
// El frontend NO cuenta las páginas del PDF ni valida la firma real: lo decide el servidor.

export const FLYER_BYTES_MAX = 8 * 1024 * 1024;
export const FLYER_TIPOS = ["image/jpeg", "image/png", "application/pdf"];
/** Para el atributo accept de los inputs (algunos navegadores dan type vacío, por eso también extensiones). */
export const FLYER_ACCEPT = "image/jpeg,image/png,application/pdf,.jpg,.jpeg,.png,.pdf";
export const FLYER_TEXTO_MAX = 4000;

export const ESTADOS_FLYER = ["COMPLETO", "INCOMPLETO", "NO_ENTENDIDO"] as const;
export type EstadoFlyer = (typeof ESTADOS_FLYER)[number];

export const ETIQUETA_CAMPO: Record<string, string> = {
  titulo: "Título",
  descripcion: "Descripción",
  inicio: "Inicio",
  fin: "Fin",
  lugar: "Lugar",
  asistencia: "Asistencia",
};

export const MENSAJE_BORROSA = "La imagen se ve borrosa; revisa los datos o súbela con más calidad";
export const MENSAJE_NO_ENTENDIDO = "No encontramos un evento en lo que enviaste. Puedes subir otro archivo o llenar el formulario a mano.";

const EXTENSION = /\.(jpe?g|png|pdf)$/i;

const MB = 1024 * 1024;
const formatoMB = (bytes: number) => (bytes / MB).toFixed(1).replace(/\.0$/, "");

export type ArchivoBasico = { name: string; type: string; size: number };

/** Devuelve el mensaje de error, o null si el archivo se puede enviar. El servidor vuelve a validar. */
export function validarArchivoFlyer(archivo: ArchivoBasico): string | null {
  const tipoValido = FLYER_TIPOS.includes(archivo.type) || (archivo.type === "" && EXTENSION.test(archivo.name));
  if (!tipoValido) return "El archivo tiene que ser JPG, PNG o PDF.";
  if (archivo.size === 0) return "El archivo está vacío.";
  if (archivo.size > FLYER_BYTES_MAX) return `El archivo pesa ${formatoMB(archivo.size)} MB y el máximo es 8 MB.`;
  return null;
}

/** Texto pegado: devuelve el mensaje de error, o null si se puede enviar. */
export function validarTextoFlyer(texto: string): string | null {
  const limpio = texto.trim();
  if (limpio.length === 0) return "Pega el texto del evento.";
  if (limpio.length < 10) return "El texto es muy corto para reconocer un evento. Agrega más detalles.";
  if (limpio.length > FLYER_TEXTO_MAX) return `El texto puede tener como máximo ${FLYER_TEXTO_MAX} caracteres (tiene ${limpio.length}).`;
  return null;
}

export const esPdf = (archivo: Pick<ArchivoBasico, "name" | "type">) =>
  archivo.type === "application/pdf" || (archivo.type === "" && /\.pdf$/i.test(archivo.name));

export const MENSAJES_FLYER: Record<string, string> = {
  VALIDACION: "No pudimos usar lo que enviaste. Revisa que sea un solo archivo (o un texto) de hasta 8 MB.",
  ARCHIVO_NO_ADMITIDO: "El archivo no es una imagen JPG, PNG ni un PDF válido. Prueba con otro.",
  PDF_MUCHAS_PAGINAS: "El PDF tiene más de 2 páginas. Sube solo el afiche (máximo 2 páginas).",
  PDF_PROTEGIDO: "El PDF tiene contraseña. Quítasela y vuelve a subirlo.",
  PDF_INVALIDO: "No pudimos leer el PDF: parece dañado. Prueba con otro archivo o una foto del afiche.",
  SOLICITUD_NO_PERMITIDA: "No podemos leer ese contenido. Revisa el texto o llena el formulario a mano.",
  DEMASIADAS_SOLICITUDES: "Hiciste muchas consultas seguidas. Espera un minuto y vuelve a intentarlo.",
  LECTOR_NO_DISPONIBLE: "La lectura de afiches no está disponible en este momento. Llena el formulario a mano o inténtalo más tarde.",
};

/** El mensaje para el estudiante según el código del servidor; si no lo conoce, el del servidor o uno genérico. */
export function mensajeErrorFlyer(codigo: string | undefined, mensajeServidor: string | undefined): string {
  return (
    (codigo ? MENSAJES_FLYER[codigo] : undefined) ??
    (mensajeServidor?.trim() || "No pudimos leer el afiche. Inténtalo de nuevo o llena el formulario a mano.")
  );
}

export type EventoLeido = {
  titulo: string | null;
  descripcion: string | null;
  inicio: string | null;
  fin: string | null;
  lugarTexto: string | null;
  asistencia: "FIJA" | "LIBRE" | null;
  visitaMin: number | null;
  visitaMax: number | null;
};

/** Qué campos del resumen faltan: lo que dice el servidor, más cualquier obligatorio que llegue vacío. */
export function camposFaltantes(faltantes: readonly string[], evento: EventoLeido): Set<string> {
  const f = new Set(faltantes);
  if (!evento.titulo) f.add("titulo");
  if (!evento.inicio) f.add("inicio");
  if (!evento.fin) f.add("fin");
  if (!evento.lugarTexto) f.add("lugar");
  return f;
}

/** «2026-11-20T08:00» → «20/11/2026 08:00». No usa Date para no mover la hora por zona horaria. */
export function formatearFechaHora(valor: string | null): string {
  const m = valor?.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : "";
}

export const textoVisita = (e: EventoLeido): string =>
  e.asistencia === "LIBRE" && e.visitaMin != null && e.visitaMax != null ? `${e.visitaMin} a ${e.visitaMax} minutos` : "";

export const textoAsistencia = (a: EventoLeido["asistencia"]): string =>
  a === "FIJA" ? "Fija: hay que llegar a la hora" : a === "LIBRE" ? "Libre: puedes pasar un rato" : "";

/** La imagen se ve borrosa: el servidor la marca con INCOMPLETO y ese mensaje exacto. */
export const imagenBorrosa = (estado: string, mensaje: string | null): boolean => estado === "INCOMPLETO" && mensaje === MENSAJE_BORROSA;

/** Con NO_ENTENDIDO no hay borrador que usar; con los demás sí (aunque falten campos, el formulario los completa). */
export const sePuedeUsarBorrador = (estado: string): boolean => estado === "COMPLETO" || estado === "INCOMPLETO";
