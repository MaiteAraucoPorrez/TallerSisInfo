export const FOTO_EVENTO_MAX_BYTES = 2 * 1024 * 1024;
const FOTO_EVENTO_TIPOS = ["image/jpeg", "image/png"];

export type CamposFormularioEvento = {
  titulo: string;
  inicio: string;
  fin: string;
  lugarId: string;
  lugarTexto: string;
  /** «» = sin elegir (un borrador de flyer sin asistencia leída): hay que elegirla antes de enviar. */
  asistencia: "FIJA" | "LIBRE" | "";
  visitaMin: string;
  visitaMax: string;
};

export function validarFormularioEvento(campos: CamposFormularioEvento): string | null {
  if (!campos.titulo.trim()) return "Escribe el título del evento.";
  if (campos.titulo.trim().length > 120) return "El título no puede superar 120 caracteres.";
  if (!campos.inicio) return "Indica la fecha y hora de inicio.";
  if (!campos.fin) return "Indica la fecha y hora de fin.";
  if (!campos.lugarId && !campos.lugarTexto.trim()) return "Elige un lugar o escribe dónde será el evento.";
  if (campos.lugarTexto.trim().length > 150) return "El lugar no puede superar 150 caracteres.";
  if (campos.asistencia === "") return "Elige si hay que llegar a la hora o si se puede pasar un rato.";

  if (campos.asistencia === "LIBRE") {
    const minimo = Number(campos.visitaMin);
    const maximo = Number(campos.visitaMax);
    if (!campos.visitaMin || !campos.visitaMax) return "Indica el tiempo mínimo y máximo de visita.";
    if (!Number.isInteger(minimo) || !Number.isInteger(maximo) || minimo < 1 || maximo < minimo || maximo > 825)
      return "El tiempo de visita debe estar entre 1 y 825 minutos y el máximo no puede ser menor al mínimo.";
  }

  return null;
}

export function validarFotoEvento(archivo: Pick<File, "name" | "type" | "size">): string | null {
  const extensionValida = /\.(jpe?g|png)$/i.test(archivo.name);
  if (!FOTO_EVENTO_TIPOS.includes(archivo.type) && !(archivo.type === "" && extensionValida))
    return "La foto del evento debe ser JPG o PNG.";
  if (archivo.size === 0) return "La foto seleccionada está vacía.";
  if (archivo.size > FOTO_EVENTO_MAX_BYTES) return "La foto no puede superar los 2 MB.";
  return null;
}

export function fechaBoliviaParaApi(valor: string): string {
  return valor ? `${valor}:00-04:00` : "";
}

export function fechaBoliviaParaFormulario(valor: string): string {
  return valor.slice(0, 16);
}

export function ahoraBoliviaParaFormulario(ahora = new Date()): string {
  return new Date(ahora.getTime() - 4 * 60 * 60 * 1000).toISOString().slice(0, 16);
}

export function formDataEvento(
  campos: CamposFormularioEvento & { descripcion: string },
  foto: File | null,
  nombreLugarSeleccionado?: string,
  /** Solo al crear: FLYER si el formulario salió de un borrador de afiche. Al editar no se envía. */
  origen?: "FORMULARIO" | "FLYER",
): FormData {
  const datos = new FormData();
  if (origen) datos.set("Origen", origen);
  datos.set("Titulo", campos.titulo.trim());
  datos.set("Descripcion", campos.descripcion.trim());
  datos.set("Inicio", fechaBoliviaParaApi(campos.inicio));
  datos.set("Fin", fechaBoliviaParaApi(campos.fin));
  datos.set("LugarId", campos.lugarId);
  datos.set("LugarTexto", campos.lugarId ? nombreLugarSeleccionado ?? "" : campos.lugarTexto.trim());
  datos.set("Asistencia", campos.asistencia);
  datos.set("VisitaMin", campos.asistencia === "LIBRE" ? campos.visitaMin : "");
  datos.set("VisitaMax", campos.asistencia === "LIBRE" ? campos.visitaMax : "");
  if (foto) datos.set("Foto", foto);
  return datos;
}

export type EstadoEvento = "PENDIENTE" | "PUBLICADO" | "RECHAZADO" | "CANCELADO";

export type PresentacionEvento = {
  clave: EstadoEvento | "VENCIDO";
  etiqueta: string;
  /** Solo se edita un evento PENDIENTE o PUBLICADO que todavía no empezó. */
  editable: boolean;
  /** Se puede cancelar mientras no esté cancelado ni haya empezado. */
  cancelable: boolean;
};

const ETIQUETAS_EVENTO: Record<EstadoEvento, string> = {
  PENDIENTE: "En revisión",
  PUBLICADO: "Publicado",
  RECHAZADO: "Rechazado",
  CANCELADO: "Cancelado",
};

/**
 * Cómo se muestra un evento propio. Un PENDIENTE cuyo fin ya pasó se ve como «Vencido»:
 * es solo de presentación, el estado en el servidor no cambia.
 */
export function presentacionEvento(
  estado: EstadoEvento,
  inicio: string,
  fin: string,
  ahoraMs: number,
): PresentacionEvento {
  const empezo = new Date(inicio).getTime() <= ahoraMs;
  const vencido = estado === "PENDIENTE" && new Date(fin).getTime() <= ahoraMs;
  return {
    clave: vencido ? "VENCIDO" : estado,
    etiqueta: vencido ? "Vencido" : ETIQUETAS_EVENTO[estado],
    editable: (estado === "PENDIENTE" || estado === "PUBLICADO") && !empezo,
    cancelable: estado !== "CANCELADO" && !empezo,
  };
}
