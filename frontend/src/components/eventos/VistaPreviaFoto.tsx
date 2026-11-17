"use client";

import { useEffect, useState } from "react";
import estilos from "./FotoEvento.module.css";

/** Vista previa de una foto elegida en el navegador (aún no subida; pesa 2 MB como máximo). */
export default function VistaPreviaFoto({ archivo, titulo }: { archivo: File; titulo: string }) {
  const [vista, setVista] = useState<{ archivo: File; url: string } | null>(null);

  useEffect(() => {
    let activo = true;
    const lector = new FileReader();
    lector.onload = () => {
      if (activo && typeof lector.result === "string") setVista({ archivo, url: lector.result });
    };
    lector.readAsDataURL(archivo);
    return () => {
      activo = false;
      if (lector.readyState === FileReader.LOADING) lector.abort();
    };
  }, [archivo]);

  if (!vista || vista.archivo !== archivo) return <div className={estilos.cargando} aria-label="Cargando vista previa" />;

  return (
    // eslint-disable-next-line @next/next/no-img-element -- vista previa local (data:), no puede usar el optimizador.
    <img className={estilos.foto} src={vista.url} alt={`Vista previa de la foto de ${titulo}`} />
  );
}
