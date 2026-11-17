"use client";
import { useEffect, useRef } from "react";
import { useAlReconectar, useEventoEnVivo } from "./tiempoReal";

/**
 * Mantiene una pantalla al día sin botón «Actualizar» (como Gmail o GitHub): vuelve a pedir los datos cuando llega un
 * aviso en vivo para este usuario (un reporte nuevo, un evento aprobado…), cuando se recupera la conexión y cuando la
 * persona vuelve a la pestaña después de un rato en otra. `refrescar` no debería mostrar «Cargando…»: los datos se
 * reemplazan sin parpadeo.
 */
export function useRefrescoAutomatico(refrescar: () => void) {
  const actual = useRef(refrescar);
  useEffect(() => {
    actual.current = refrescar;
  }, [refrescar]);

  useEventoEnVivo("avisosCambiaron", () => actual.current());
  useAlReconectar(() => actual.current());

  useEffect(() => {
    const alVolver = () => {
      if (document.visibilityState === "visible") actual.current();
    };
    document.addEventListener("visibilitychange", alVolver);
    return () => document.removeEventListener("visibilitychange", alVolver);
  }, []);
}
