import { pedirApi } from "./api";
import { MotivoReporte, ObjetoDeReporte, cuerpoReporte } from "./reglasReportes";

export {
  LARGO_MAXIMO_REPORTE,
  LARGO_MINIMO_REPORTE,
  MAXIMO_PENDIENTES,
  enlaceMisReportes,
  errorDescripcionReporte,
} from "./reglasReportes";
export type { MotivoReporte } from "./reglasReportes";

/**
 * Reportes del estudiante (HU27): desde el perfil (algo no funciona, sugerencias, información de un lugar) y con el botón
 * «Reportar» de una comunidad o un evento. Los revisa un administrador; el estudiante los sigue en «Mis reportes».
 */
export type EstadoReporte = "PENDIENTE" | "RESUELTO" | "DESCARTADO";

export type ObjetoReporte = "LUGAR" | "COMUNIDAD" | "EVENTO";

export type LugarReportable = { id: number; nombre: string };

export type MiReporte = {
  id: number;
  motivo: MotivoReporte;
  objetoTipo: ObjetoReporte | null;
  objetoId: number | null;
  /** Nombre del lugar, actividad de la comunidad o título del evento. */
  objeto: string | null;
  descripcion: string | null;
  estado: EstadoReporte;
  creadoEn: string;
  /** null mientras siga pendiente. */
  revisadoEn: string | null;
  /** La respuesta del administrador, si dejó una. */
  respuesta: string | null;
};

export const ETIQUETA_MOTIVO: Record<MotivoReporte, string> = {
  ERROR_APP: "Algo no funciona",
  SUGERENCIA: "Sugerencia",
  LUGAR_INCORRECTO: "Información de un lugar",
  COMUNIDAD_INAPROPIADA: "Comunidad inapropiada",
  EVENTO_INAPROPIADO: "Evento inapropiado",
};

export const ETIQUETA_ESTADO: Record<EstadoReporte, string> = {
  PENDIENTE: "En revisión",
  RESUELTO: "Resuelto",
  DESCARTADO: "Descartado",
};

/** El motivo que corresponde a cada objeto que se reporta con el botón «Reportar». */
export const MOTIVO_DE_OBJETO = { COMUNIDAD: "COMUNIDAD_INAPROPIADA", EVENTO: "EVENTO_INAPROPIADO" } as const;

export const listarLugaresReportables = () => pedirApi<LugarReportable[]>("/api/reportes/lugares");

export const listarMisReportes = () => pedirApi<MiReporte[]>("/api/reportes/mios");

/** Cada motivo va solo con su objeto: el lugar, la comunidad o el evento (los generales, sin ninguno). */
export const enviarReporte = (motivo: MotivoReporte, descripcion: string, objeto: ObjetoDeReporte = {}) =>
  pedirApi<{ id: number; estado: string }>("/api/reportes", {
    method: "POST",
    body: JSON.stringify(cuerpoReporte(motivo, descripcion, objeto)),
  });
