"use client";

import { useEffect, useState } from "react";
import { pedirArchivoApi } from "@/lib/api";
import estilos from "./FotoEvento.module.css";

export default function FotoEvento({ ruta, titulo }: { ruta: string; titulo: string }) {
  const [resultado, setResultado] = useState<{ ruta: string; url: string | null; error: string }>({
    ruta: "",
    url: null,
    error: "",
  });

  useEffect(() => {
    let activo = true;
    let temporal: string | null = null;

    pedirArchivoApi(ruta)
      .then((foto) => {
        if (!activo) return;
        temporal = URL.createObjectURL(foto);
        setResultado({ ruta, url: temporal, error: "" });
      })
      .catch((e: unknown) => {
        if (activo) {
          setResultado({
            ruta,
            url: null,
            error: e instanceof Error ? e.message : "No se pudo cargar la foto del evento.",
          });
        }
      });

    return () => {
      activo = false;
      if (temporal) URL.revokeObjectURL(temporal);
    };
  }, [ruta]);

  const { url, error } = resultado.ruta === ruta ? resultado : { url: null, error: "" };
  if (error) return <p className={estilos.error} role="status">{error}</p>;
  if (!url) return <div className={estilos.cargando} aria-label="Cargando foto del evento" />;

  return (
    // eslint-disable-next-line @next/next/no-img-element -- imagen privada servida con token de sesión, no puede usar el optimizador.
    <img className={estilos.foto} src={url} alt={`Foto de ${titulo}`} />
  );
}
