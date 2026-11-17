// HU24 parte 2: deja la imagen del flyer lista para ser la foto del evento (JPG/PNG de hasta 2 MB).
// Usa el canvas del navegador; las decisiones (cuándo reducir, con qué calidad) viven en reglasBorradorFlyer.ts.
import {
  avisoFotoDescartada,
  avisoFotoReducida,
  buscarReduccion,
  dimensionesReducidas,
  nombreJpeg,
  planFoto,
  type IntentoReduccion,
} from "./reglasBorradorFlyer";

export type FotoPreparada = { foto: File | null; aviso: string | null };

async function cargarImagen(archivo: File): Promise<ImageBitmap | HTMLImageElement> {
  if (typeof createImageBitmap === "function") return createImageBitmap(archivo);
  const url = URL.createObjectURL(archivo);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    return img;
  } finally {
    URL.revokeObjectURL(url);
  }
}

function aBlobJpeg(canvas: HTMLCanvasElement, calidad: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, "image/jpeg", calidad));
}

/** Reduce la imagen a JPEG probando tamaños y calidades; null si ninguna combinación cabe en 2 MB. */
async function reducirFoto(archivo: File): Promise<File | null> {
  const imagen = await cargarImagen(archivo);
  const anchoOriginal = "naturalWidth" in imagen ? imagen.naturalWidth : imagen.width;
  const altoOriginal = "naturalHeight" in imagen ? imagen.naturalHeight : imagen.height;
  if (!anchoOriginal || !altoOriginal) return null;

  return buscarReduccion(async ({ ladoMax, calidad }: IntentoReduccion) => {
    const { ancho, alto } = dimensionesReducidas(anchoOriginal, altoOriginal, ladoMax);
    const canvas = document.createElement("canvas");
    canvas.width = ancho;
    canvas.height = alto;
    const contexto = canvas.getContext("2d");
    if (!contexto) return null;
    contexto.fillStyle = "#ffffff"; // un PNG con transparencia no debe quedar negro en JPEG
    contexto.fillRect(0, 0, ancho, alto);
    contexto.drawImage(imagen, 0, 0, ancho, alto);
    const blob = await aBlobJpeg(canvas, calidad);
    return blob ? new File([blob], nombreJpeg(archivo.name), { type: "image/jpeg" }) : null;
  });
}

/**
 * Prepara la imagen del borrador: si ya cabe en 2 MB se usa tal cual; si no, se intenta reducir en el navegador;
 * si no se logra, se descarta y se explica por qué. Nunca lanza: ante cualquier fallo devuelve foto null con aviso.
 */
export async function prepararFotoDelBorrador(archivo: File): Promise<FotoPreparada> {
  const plan = planFoto(archivo);
  if (plan === "usar") return { foto: archivo, aviso: null };
  if (plan === "descartar") return { foto: null, aviso: avisoFotoDescartada(archivo.size) };

  try {
    const reducida = await reducirFoto(archivo);
    if (reducida) return { foto: reducida, aviso: avisoFotoReducida(archivo.size, reducida.size) };
  } catch {
    // formato que el navegador no puede decodificar: se descarta
  }
  return { foto: null, aviso: avisoFotoDescartada(archivo.size) };
}
