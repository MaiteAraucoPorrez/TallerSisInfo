/** Reglas puras de la pantalla de recomendaciones (sin red), para poder probarlas con `npm test`. */

/** «deporte», «aire libre» y «música» → «deporte, aire libre y música». */
function unir(partes: string[]): string {
  if (partes.length <= 1) return partes.join("");
  return `${partes.slice(0, -1).join(", ")} y ${partes[partes.length - 1]}`;
}

/** Primera letra en minúscula, salvo siglas («UCB») o nombres que van en mayúscula a propósito. */
function enFrase(etiqueta: string): string {
  const limpia = etiqueta.trim();
  if (limpia.length > 1 && limpia === limpia.toUpperCase()) return limpia;
  return limpia.charAt(0).toLocaleLowerCase("es") + limpia.slice(1);
}

/**
 * HU30: «Porque te gustan los planes de deporte y aire libre». Sale de `porQueTeGusta` (las etiquetas del lugar que el
 * estudiante suele valorar bien). Sin motivos devuelve null: no se muestra nada en vez de inventar uno.
 */
export function textoPorQueTeGusta(motivos: readonly string[] | null | undefined): string | null {
  const limpios = (motivos ?? []).map((m) => m.trim()).filter((m) => m.length > 0);
  if (limpios.length === 0) return null;
  return `Porque te gustan los planes de ${unir(limpios.map(enFrase))}`;
}
