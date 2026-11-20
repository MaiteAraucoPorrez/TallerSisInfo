import { pedirApi } from "./api";

/** HU12: saludos entre compañeros de «Libres contigo». */

export type EstadoConexion = "PENDIENTE" | "ACEPTADA" | "RECHAZADA";

export type ConexionResumen = { id: number; estado: EstadoConexion; enviadaPorMi: boolean };

/** Antes de aceptarse, solo el nombre corto y sin correo; aceptada, nombre completo y correo. */
export type Conexion = {
  id: number;
  estado: EstadoConexion;
  enviadaPorMi: boolean;
  nombre: string;
  carrera: string | null;
  semestre: number | null;
  /** La del test: es la misma vista desde los dos lados. */
  afinidad: number;
  enComun: string[];
  diaSemana: number;
  horaInicio: string;
  horaFin: string;
  correo: string | null;
  creadaEn: string;
};

export type Conexiones = { recibidas: Conexion[]; enviadas: Conexion[]; aceptadas: Conexion[] };

export const listarConexiones = () => pedirApi<Conexiones>("/api/conexiones");

export const saludar = (usuarioId: number, puenteId: number) =>
  pedirApi<Conexion>("/api/conexiones", { method: "POST", body: JSON.stringify({ usuarioId, puenteId }) });

export const responderSaludo = (id: number, aceptar: boolean) =>
  pedirApi<Conexion>(`/api/conexiones/${id}`, { method: "PUT", body: JSON.stringify({ aceptar }) });

/* ---------- Planes con una conexión (migración 032) ---------- */

/** Un rato libre que los dos comparten hoy. `puenteId` es el tuyo: sirve para pedir los lugares viables de ese rato. */
export type TramoPlan = { puenteId: number; horaInicio: string; horaFin: string; minutos: number };
export type ComunidadInvitable = { id: number; actividad: string; horaInicio: string; horaFin: string; confirmados: number; maxParticipantes: number };
export type EventoInvitable = { id: number; titulo: string; lugar: string; horaInicio: string; horaFin: string };
export type OpcionesPlan = {
  conexionId: number;
  nombre: string;
  tramos: TramoPlan[];
  comunidades: ComunidadInvitable[];
  eventos: EventoInvitable[];
};
export type PlanCreado = { comunidadId: number; actividad: string; horaInicio: string; horaFin: string; mensaje: string };

export const obtenerOpcionesPlan = (conexionId: number) => pedirApi<OpcionesPlan>(`/api/conexiones/${conexionId}/plan`);

/** Comunidad privada para los dos (cupo 2) en ese rato, en un lugar de las sugerencias. A la conexión le llega «Voy / No voy». */
export const crearPlan = (conexionId: number, puenteId: number, lugarId: number) =>
  pedirApi<PlanCreado>(`/api/conexiones/${conexionId}/plan`, { method: "POST", body: JSON.stringify({ puenteId, lugarId }) });

/** Invita a la conexión a una comunidad tuya o a un evento de hoy (una de las dos cosas). */
export const invitarConexion = (conexionId: number, destino: { comunidadId: number } | { eventoId: number }) =>
  pedirApi<{ mensaje: string }>(`/api/conexiones/${conexionId}/invitaciones`, { method: "POST", body: JSON.stringify(destino) });
