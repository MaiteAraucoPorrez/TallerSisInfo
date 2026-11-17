// Estado de la foto del evento en el formulario. Funciones puras, sin React ni red, para poder probarlas.
// Al crear solo hay «sinTocar» o «nueva». Al editar, además se puede «quitar» la foto actual (y deshacerlo
// antes de guardar). Al guardar se envía la foto nueva, la orden de quitar, o nada.

export type EstadoFoto<T> =
  | { tipo: "sinTocar" }
  | { tipo: "nueva"; archivo: T }
  | { tipo: "quitar" };

export const FOTO_SIN_TOCAR = { tipo: "sinTocar" } as const;

/** Elegir un archivo (o null para dejar de tener uno nuevo): la foto nueva reemplaza a lo anterior, incluso a «quitar». */
export function elegirFoto<T>(archivo: T | null): EstadoFoto<T> {
  return archivo === null ? FOTO_SIN_TOCAR : { tipo: "nueva", archivo };
}

/** El archivo nuevo elegido, si hay. */
export const archivoNuevo = <T>(estado: EstadoFoto<T>): T | null => (estado.tipo === "nueva" ? estado.archivo : null);

/** «Quitar foto»: marca que se quitará la actual. Si ya había una nueva elegida, solo se descarta la nueva. */
export function quitarFotoActual<T>(estado: EstadoFoto<T>): EstadoFoto<T> {
  return estado.tipo === "nueva" ? FOTO_SIN_TOCAR : { tipo: "quitar" };
}

/** «Deshacer»: vuelve a conservar la foto actual (solo tiene efecto si estaba marcada para quitar). */
export const deshacerQuitar = <T>(estado: EstadoFoto<T>): EstadoFoto<T> =>
  estado.tipo === "quitar" ? FOTO_SIN_TOCAR : estado;

/** Lo que hay que enviar al guardar: la foto nueva, la orden de quitar, o nada. */
export function datosDeFoto<T>(estado: EstadoFoto<T>): { foto: T | null; quitarFoto: boolean } {
  return { foto: archivoNuevo(estado), quitarFoto: estado.tipo === "quitar" };
}

export type AccionesFoto = {
  /** Mostrar «Quitar foto»: hay una foto actual que todavía no se marcó para quitar. */
  quitar: boolean;
  /** Mostrar «Deshacer»: la foto actual está marcada para quitarse. */
  deshacer: boolean;
  /** Mostrar «Descartar la nueva»: se eligió un archivo nuevo. */
  descartarNueva: boolean;
};

/** Qué botones corresponden en la edición según el estado y si el evento tiene foto actual. */
export function accionesFoto<T>(estado: EstadoFoto<T>, tieneFotoActual: boolean): AccionesFoto {
  return {
    quitar: tieneFotoActual && estado.tipo === "sinTocar",
    deshacer: estado.tipo === "quitar",
    descartarNueva: estado.tipo === "nueva",
  };
}

export type VistaFoto = "actual" | "nueva" | "quitada" | "ninguna";

/** Qué se muestra en la vista previa: la foto actual, la nueva elegida, el aviso de que se quitará, o nada. */
export function vistaFoto<T>(estado: EstadoFoto<T>, tieneFotoActual: boolean): VistaFoto {
  if (estado.tipo === "nueva") return "nueva";
  if (estado.tipo === "quitar") return "quitada";
  return tieneFotoActual ? "actual" : "ninguna";
}
