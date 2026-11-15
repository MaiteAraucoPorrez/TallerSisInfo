"use client";

import { useId, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { SALIDA } from "@/components/app/Movimiento";
import { LARGO_MAXIMO_RESPUESTA } from "@/lib/admin";
import estilos from "./RevisarReporte.module.css";

type Decision = "RESUELTO" | "DESCARTADO";

type Props = {
  /** Para los aria-label: «el reporte de …». */
  objeto: string;
  ocupado: boolean;
  onRevisar: (decision: Decision, respuesta: string) => Promise<boolean>;
  claseResolver: string;
  claseDescartar: string;
};

/**
 * HU27: resolver o descartar un reporte pendiente con una respuesta opcional para el estudiante (hasta 500 caracteres).
 * La respuesta queda oculta tras «Responder al estudiante» para que la bandeja siga siendo fácil de recorrer.
 */
export default function RevisarReporte({ objeto, ocupado, onRevisar, claseResolver, claseDescartar }: Props) {
  const reducir = useReducedMotion();
  const idCampo = useId();
  const [conRespuesta, setConRespuesta] = useState(false);
  const [respuesta, setRespuesta] = useState("");
  const larga = respuesta.trim().length > LARGO_MAXIMO_RESPUESTA;

  async function revisar(decision: Decision) {
    if (larga) return;
    const ok = await onRevisar(decision, conRespuesta ? respuesta : "");
    if (ok) setRespuesta("");
  }

  return (
    <div className={estilos.contenedor}>
      <AnimatePresence initial={false}>
        {conRespuesta && (
          <motion.label
            className={estilos.campo}
            htmlFor={idCampo}
            initial={reducir ? false : { opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={reducir ? { opacity: 0 } : { opacity: 0, y: -6 }}
            transition={{ duration: 0.2, ease: SALIDA }}
          >
            <span>
              Respuesta para el estudiante <em>(opcional)</em>
            </span>
            <textarea
              id={idCampo}
              rows={3}
              value={respuesta}
              maxLength={LARGO_MAXIMO_RESPUESTA}
              placeholder="Ej: Gracias por avisar, ya corregimos el horario."
              aria-invalid={larga}
              autoFocus
              onChange={(e) => setRespuesta(e.target.value)}
            />
            <span className={estilos.pie}>
              <small>La ve en Mis reportes y, resumida, en su campanita.</small>
              <small className={respuesta.length >= LARGO_MAXIMO_RESPUESTA * 0.9 ? estilos.limite : estilos.contador}>
                {respuesta.length}/{LARGO_MAXIMO_RESPUESTA}
              </small>
            </span>
          </motion.label>
        )}
      </AnimatePresence>
      <span className={estilos.acciones}>
        <button
          type="button"
          className={claseResolver}
          disabled={ocupado || larga}
          onClick={() => void revisar("RESUELTO")}
          aria-label={`Marcar como resuelto el reporte de ${objeto}`}
        >
          Resolver
        </button>
        <button
          type="button"
          className={claseDescartar}
          disabled={ocupado || larga}
          onClick={() => void revisar("DESCARTADO")}
          aria-label={`Descartar el reporte de ${objeto}`}
        >
          Descartar
        </button>
        {!conRespuesta && (
          <button type="button" className={estilos.responder} onClick={() => setConRespuesta(true)}>
            Responder al estudiante
          </button>
        )}
      </span>
    </div>
  );
}
