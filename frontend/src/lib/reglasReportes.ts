/**
 * Reglas puras de los reportes (HU27), sin llamadas a la API para poder probarlas con `node --test`.
 * Repiten lo que valida el backend para avisar antes de enviar; el backend siempre tiene la última palabra.
 */
export type MotivoReporte = "ERROR_APP" | "SUGERENCIA" | "LUGAR_INCORRECTO" | "COMUNIDAD_INAPROPIADA" | "EVENTO_INAPROPIADO";

export const LARGO_MINIMO_REPORTE = 10;
export const LARGO_MAXIMO_REPORTE = 500;
export const MAXIMO_PENDIENTES = 5;

/** El primer problema del texto antes de enviarlo, o "" si está bien. */
export function errorDescripcionReporte(texto: string): string {
  const largo = texto.trim().length;
  if (largo < LARGO_MINIMO_REPORTE) return "Cuéntanos un poco más: al menos 10 caracteres.";
  if (largo > LARGO_MAXIMO_REPORTE) return `Máximo ${LARGO_MAXIMO_REPORTE} caracteres.`;
  return "";
}

export type ObjetoDeReporte = { lugarId?: number | null; comunidadId?: number | null; eventoId?: number | null };

/**
 * El cuerpo de POST /api/reportes. Cada motivo lleva solo su objeto (el backend responde 400 si llega otro): el lugar con
 * LUGAR_INCORRECTO, la comunidad con COMUNIDAD_INAPROPIADA y el evento con EVENTO_INAPROPIADO; los generales, ninguno.
 */
export function cuerpoReporte(motivo: MotivoReporte, descripcion: string, objeto: ObjetoDeReporte = {}) {
  return {
    motivo,
    descripcion: descripcion.trim(),
    lugarId: motivo === "LUGAR_INCORRECTO" ? (objeto.lugarId ?? null) : null,
    comunidadId: motivo === "COMUNIDAD_INAPROPIADA" ? (objeto.comunidadId ?? null) : null,
    eventoId: motivo === "EVENTO_INAPROPIADO" ? (objeto.eventoId ?? null) : null,
  };
}

/** A dónde lleva el aviso de un reporte revisado: «Mis reportes», con ese reporte resaltado si se sabe cuál es. */
export function enlaceMisReportes(reporteId?: number | null): string {
  return reporteId ? `/perfil?pestana=mis-reportes&reporte=${reporteId}` : "/perfil?pestana=mis-reportes";
}
