/**
 * HU30: reglas puras de las etiquetas de tipo de actividad en el admin (las mismas del backend, para avisar antes de
 * guardar). Un lugar ACTIVO y recomendable necesita al menos una: sin eso la personalización no sabe qué tipo de lugar es.
 */
export const LARGO_MAXIMO_ETIQUETA = 50;

export const MENSAJE_SIN_ETIQUETA = "Elige al menos un tipo de actividad: sin eso el lugar no se puede activar ni recomendar.";

/** Si el lugar va a quedar activo y recomendable: los nuevos siempre (se crean ACTIVO); los existentes, si ya lo están. */
export function quedaActivoYRecomendable(lugar: { estado?: string; recomendable?: boolean } | null): boolean {
  if (!lugar) return true;
  return lugar.estado === "ACTIVO" && lugar.recomendable !== false;
}

/** El problema de la selección, o "" si está bien. */
export function errorEtiquetas(seleccion: readonly number[], requiereUna: boolean): string {
  return requiereUna && seleccion.length === 0 ? MENSAJE_SIN_ETIQUETA : "";
}

/** Si se puede pasar el lugar a ACTIVO desde la lista (los de referencia no necesitan etiqueta). */
export function puedeActivarse(lugar: { recomendable: boolean; etiquetas?: readonly string[] }): boolean {
  return !lugar.recomendable || (lugar.etiquetas?.length ?? 0) > 0;
}

/** Marca o desmarca una etiqueta, sin repetir y en orden. */
export function alternar(seleccion: readonly number[], id: number): number[] {
  return seleccion.includes(id) ? seleccion.filter((x) => x !== id) : [...seleccion, id].sort((a, b) => a - b);
}

/** El nombre de una etiqueta nueva sin espacios de más, o "" si no sirve. */
export function limpiarNombreEtiqueta(nombre: string): string {
  const limpio = nombre.replace(/\s+/g, " ").trim();
  return limpio.length > LARGO_MAXIMO_ETIQUETA ? "" : limpio;
}
