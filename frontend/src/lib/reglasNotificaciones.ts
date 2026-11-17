// Cómo se ve cada tipo de notificación en la campana. Función pura, sin React, para poder probarla.
// Un tipo que la campana no conoce (por ejemplo uno que agregue otra HU) usa una presentación genérica
// en vez de romper el panel.

export type IconoNotificacion = "rayo" | "personas" | "bandera" | "calendario";
export type ClaseNotificacion = "puente" | "invitacion" | "cerrada" | "advertencia" | "reporte";

export type PresentacionNotificacion = {
  titulo: string;
  clase: ClaseNotificacion;
  icono: IconoNotificacion;
};

const PRESENTACIONES: Record<string, PresentacionNotificacion> = {
  PUENTE_DISPONIBLE: { titulo: "Puente disponible", clase: "puente", icono: "rayo" },
  DESPUES_DE_CLASES: { titulo: "Terminaste tus clases", clase: "puente", icono: "rayo" },
  INVITACION_COMUNIDAD: { titulo: "Invitación a una comunidad", clase: "invitacion", icono: "personas" },
  COMUNIDAD_CERRADA: { titulo: "El grupo se llenó", clase: "cerrada", icono: "personas" },
  // HU28: la advertencia del administrador. No lleva a otra pantalla: el aviso ya dice el motivo.
  SANCION_ADVERTENCIA: { titulo: "Advertencia del administrador", clase: "advertencia", icono: "bandera" },
  // HU12: saludos de «Libres contigo». Llevan a esa pestaña, donde se responden o se ve el correo.
  CONEXION_SOLICITADA: { titulo: "Alguien quiere conocerte", clase: "invitacion", icono: "personas" },
  CONEXION_ACEPTADA: { titulo: "Aceptaron tu saludo", clase: "invitacion", icono: "personas" },
  // HU27: un administrador revisó tu reporte. Lleva a «Mis reportes», donde está la respuesta completa.
  REPORTE_RESUELTO: { titulo: "Revisamos tu reporte", clase: "reporte", icono: "bandera" },
  // HU23: el administrador revisó un evento pendiente. Lleva a «Mis eventos».
  EVENTO_APROBADO: { titulo: "Tu evento fue aprobado", clase: "reporte", icono: "calendario" },
  EVENTO_RECHAZADO: { titulo: "Tu evento fue rechazado", clase: "advertencia", icono: "calendario" },
};

export const PRESENTACION_GENERICA: PresentacionNotificacion = {
  titulo: "Nuevo aviso",
  clase: "cerrada",
  icono: "rayo",
};

export function presentacionNotificacion(tipo: string): PresentacionNotificacion {
  return Object.hasOwn(PRESENTACIONES, tipo) ? PRESENTACIONES[tipo] : PRESENTACION_GENERICA;
}
