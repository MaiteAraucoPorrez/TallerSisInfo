import { pedirApi } from "./api";

export type Notificacion = {
  id: number;
  tipo:
    | "PUENTE_DISPONIBLE"
    | "DESPUES_DE_CLASES"
    | "INVITACION_COMUNIDAD"
    | "COMUNIDAD_CERRADA"
    | "SANCION_ADVERTENCIA"
    | "CONEXION_SOLICITADA"
    | "CONEXION_ACEPTADA"
    | "REPORTE_RESUELTO"
    | "EVENTO_APROBADO"
    | "EVENTO_RECHAZADO"
    // Para el administrador (migración 030): quedan leídos para todos los admins cuando uno los resuelve.
    | "REPORTE_NUEVO"
    | "EVENTO_POR_REVISAR";
  mensaje: string;
  fecha: string | null;
  horaInicio: string | null;
  horaFin: string | null;
  duracionMinutos: number | null;
  leida: boolean;
  creadaEn: string;
  comunidadId?: number | null;
  /** HU27: en REPORTE_RESUELTO, el reporte revisado (la campanita lleva a «Mis reportes»); en REPORTE_NUEVO, el reporte por revisar. */
  reporteId?: number | null;
};

export type ConfiguracionNotificaciones = { notificacionesActivas: boolean };

export const INTERVALO_CONSULTA_MS = 15_000;

export const listarNotificaciones = () => pedirApi<Notificacion[]>("/api/notificaciones");
export const marcarLeida = (id: number) => pedirApi<void>(`/api/notificaciones/${id}/leida`, { method: "PATCH" });
export const obtenerConfiguracion = () => pedirApi<ConfiguracionNotificaciones>("/api/notificaciones/configuracion");
export const guardarConfiguracion = (c: ConfiguracionNotificaciones) =>
  pedirApi<ConfiguracionNotificaciones>("/api/notificaciones/configuracion", { method: "PUT", body: JSON.stringify(c) });
