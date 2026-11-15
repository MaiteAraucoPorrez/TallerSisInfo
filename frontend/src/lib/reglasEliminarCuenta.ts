// HU29: reglas de la interfaz de «Eliminar mi cuenta». Funciones puras, sin React ni red, para poder probarlas.

export const TEXTO_CONFIRMACION = "ELIMINAR";
export const MENSAJE_CUENTA_ELIMINADA = "Tu cuenta fue eliminada.";

export const TEXTO_ADVERTENCIA =
  "Esta acción es permanente: se borrarán tu cuenta, tus eventos y tus comunidades. Escribe ELIMINAR para confirmar.";

/** El botón final solo se habilita con el texto exacto (el servidor también lo exige). */
export const confirmacionValida = (escrito: string): boolean => escrito === TEXTO_CONFIRMACION;

export type FormasDeReautenticar = { contrasena: boolean; google: boolean };

/** Cómo puede confirmar que es él: con contraseña, con Google, o con cualquiera si tiene ambas. */
export function formasDeReautenticar(perfil: { tieneContrasena?: boolean; conGoogle?: boolean }): FormasDeReautenticar {
  const contrasena = perfil.tieneContrasena !== false;
  const google = perfil.conGoogle === true;
  // Si el perfil no dice nada (cuenta antigua), se asume contraseña.
  return { contrasena: contrasena || !google, google };
}

/** Se puede enviar con contraseña: texto exacto, contraseña escrita y sin otra petición en curso. */
export const puedeEliminarConContrasena = (escrito: string, contrasena: string, enviando: boolean): boolean =>
  !enviando && confirmacionValida(escrito) && contrasena.length > 0;

/** Con Google no hay botón final: el login de Google envía, así que también exige el texto y que no haya otra petición. */
export const puedeEliminarConGoogle = (escrito: string, enviando: boolean): boolean => !enviando && confirmacionValida(escrito);

export const MENSAJES_ELIMINAR_CUENTA: Record<string, string> = {
  CONTRASENA_INCORRECTA: "La contraseña no es correcta. Inténtalo de nuevo.",
  GOOGLE_INCORRECTO: "No pudimos confirmar tu cuenta de Google. Entra con la cuenta que tienes enlazada.",
  SANCION_ACTIVA: "Tienes una sanción activa; contacta al administrador",
  NO_ELIMINABLE: "Esta cuenta no se puede eliminar desde la app.",
  DEMASIADAS_SOLICITUDES: "Hiciste muchos intentos seguidos. Espera un minuto y vuelve a intentarlo.",
};

/** El mensaje para el estudiante según el código del servidor; si no lo conoce, el mensaje del servidor o uno genérico. */
export function mensajeEliminarCuenta(codigo: string | undefined, mensajeServidor: string | undefined): string {
  return (
    (codigo ? MENSAJES_ELIMINAR_CUENTA[codigo] : undefined) ??
    (mensajeServidor?.trim() || "No pudimos eliminar tu cuenta. Inténtalo de nuevo en unos minutos.")
  );
}

export const puedeVerEliminarCuenta = (rol: string | undefined): boolean => rol !== "ADMIN";

export type ResultadoEliminacion = { ok: true } | { ok: false; error: unknown };

/**
 * Pide la eliminación y, solo si el servidor respondió 204, deja el aviso para el login y cierra la sesión local
 * (en ese orden, para que el login ya lo encuentre). Si falla, la sesión no se toca.
 */
export async function eliminarYCerrarSesion(
  pedir: () => Promise<unknown>,
  guardarAviso: (mensaje: string) => void,
  cerrarSesion: () => void,
): Promise<ResultadoEliminacion> {
  try {
    await pedir();
  } catch (error) {
    return { ok: false, error };
  }
  guardarAviso(MENSAJE_CUENTA_ELIMINADA);
  cerrarSesion();
  return { ok: true };
}
