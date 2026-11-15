import { pedirApi } from "./api";
import { EstadoFlyer, EventoLeido, FLYER_BYTES_MAX, FLYER_TIPOS } from "./reglasFlyer";

export { FLYER_BYTES_MAX, FLYER_TIPOS };
export type { EstadoFlyer };

/** Borrador de evento que devuelve el servidor. inicio y fin van en hora de Bolivia, sin zona: «yyyy-MM-ddTHH:mm». */
export type BorradorEvento = EventoLeido;

/** Página 1 del PDF en JPEG; solo existe si el origen fue un PDF y cabe en 2 MB. */
export type ImagenFlyer = { tipoMime: string; contenidoBase64: string; bytes: number };

export type LecturaFlyer = {
  consultaId: number;
  estado: EstadoFlyer;
  mensaje: string | null;
  evento: BorradorEvento;
  faltantes: string[];
  advertencias: string[];
  imagen: ImagenFlyer | null;
};

/** HU24: lee un afiche (JPG, PNG, PDF) o un texto pegado. No crea nada: solo devuelve el borrador. */
export function leerFlyer(entrada: File | string): Promise<LecturaFlyer> {
  const datos = new FormData();
  if (typeof entrada === "string") datos.append("texto", entrada);
  else datos.append("archivo", entrada);
  return pedirApi<LecturaFlyer>("/api/eventos/extraer", { method: "POST", body: datos });
}
