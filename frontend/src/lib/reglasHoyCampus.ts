/**
 * Reglas puras de «Hoy en el campus» y «Libres contigo» en Inicio (sin red, para probarlas con `npm test`).
 * Las horas son «AAAA-MM-DDTHH:mm» de Bolivia, como las da `fechaBoliviaParaFormulario`: se comparan como texto.
 */

export type EstadoEnElDia = "en-curso" | "proximo" | "terminado";

type ConHoras = { inicio: string; fin: string };

const local = (valor: string) => valor.slice(0, 16);

function aMinutos(valor: string): number {
  const [fecha, hora = "00:00"] = local(valor).split("T");
  const [a, m, d] = fecha.split("-").map(Number);
  const [h, mi] = hora.split(":").map(Number);
  return Date.UTC(a, m - 1, d, h, mi) / 60_000;
}

/** «2026-11-20T15:00…» → «15:00». */
export const horaDe = (valor: string) => local(valor).slice(11, 16);

export function estadoEnElDia(e: ConHoras, ahora: string): EstadoEnElDia {
  const ya = local(ahora);
  if (local(e.fin) <= ya) return "terminado";
  return local(e.inicio) <= ya ? "en-curso" : "proximo";
}

/** «En curso · hasta las 11:00», «En 25 min», «En 1 h 10 min» o «A las 18:30» (si falta más de 3 horas). */
export function cuandoEs(e: ConHoras, ahora: string): string {
  if (estadoEnElDia(e, ahora) === "en-curso") return `En curso · hasta las ${horaDe(e.fin)}`;
  const faltan = Math.max(0, Math.round(aMinutos(e.inicio) - aMinutos(ahora)));
  if (faltan > 180) return `A las ${horaDe(e.inicio)}`;
  if (faltan < 60) return `En ${Math.max(1, faltan)} min`;
  const h = Math.floor(faltan / 60);
  const m = faltan % 60;
  return m === 0 ? `En ${h} h` : `En ${h} h ${m} min`;
}

/**
 * La línea de tiempo de hoy: sin los que ya terminaron, por hora de inicio, como máximo `maximo`.
 * `antesDeAhora` = cuántos van antes de la marca «Ahora» (los que están en curso).
 */
export function lineaDeHoy<T extends ConHoras>(eventos: readonly T[], ahora: string, maximo = 4) {
  const vigentes = eventos
    .map((evento) => ({ evento, estado: estadoEnElDia(evento, ahora) }))
    .filter((x) => x.estado !== "terminado")
    .sort((a, b) => local(a.evento.inicio).localeCompare(local(b.evento.inicio)));
  const items = vigentes.slice(0, maximo);
  return {
    items,
    antesDeAhora: items.filter((x) => x.estado === "en-curso").length,
    ocultos: vigentes.length - items.length,
  };
}

/** «Ana Q.» → «AQ»; «diego» → «D». Para los avatares de «Libres contigo». */
export function iniciales(nombre: string): string {
  const partes = nombre.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return "?";
  const primera = Array.from(partes[0])[0] ?? "";
  const ultima = partes.length > 1 ? Array.from(partes[partes.length - 1])[0] ?? "" : "";
  return (primera + ultima).toLocaleUpperCase("es");
}

/** Lugar en la misma coordenada del campus: en vez de «0 m · 0 min», «En el campus». */
export const estaEnElCampus = (distanciaM: number) => distanciaM <= 0;
