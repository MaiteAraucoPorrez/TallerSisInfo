"use client";

import { useId, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { IconoCheck, IconoMas } from "@/components/app/Iconos";
import { SALIDA } from "@/components/app/Movimiento";
import { EtiquetaAdmin, crearEtiquetaAdmin } from "@/lib/admin";
import { LARGO_MAXIMO_ETIQUETA, alternar, limpiarNombreEtiqueta } from "@/lib/reglasEtiquetas";
import estilos from "./SelectorEtiquetas.module.css";

type Props = {
  catalogo: EtiquetaAdmin[];
  seleccion: number[];
  onCambio: (seleccion: number[]) => void;
  /** Una etiqueta nueva entra al catálogo y queda marcada. */
  onNueva: (etiqueta: EtiquetaAdmin) => void;
  /** El mensaje de «falta al menos una» (lo decide el formulario). */
  error?: string;
  requerido: boolean;
  deshabilitado?: boolean;
};

/**
 * HU30: selector múltiple de etiquetas de tipo de actividad. Pastillas que se marcan y desmarcan (botones con
 * aria-pressed), y una más para agregar una etiqueta al catálogo sin salir del formulario.
 */
export default function SelectorEtiquetas({ catalogo, seleccion, onCambio, onNueva, error, requerido, deshabilitado }: Props) {
  const reducir = useReducedMotion();
  const idTitulo = useId();
  const idAyuda = useId();
  const [agregando, setAgregando] = useState(false);
  const [nombre, setNombre] = useState("");
  const [errorNueva, setErrorNueva] = useState("");
  const [guardando, setGuardando] = useState(false);

  async function agregar() {
    const limpio = limpiarNombreEtiqueta(nombre);
    if (!limpio) return setErrorNueva(`Escribe un nombre de hasta ${LARGO_MAXIMO_ETIQUETA} caracteres.`);
    setGuardando(true);
    try {
      const nueva = await crearEtiquetaAdmin(limpio);
      onNueva(nueva);
      setNombre("");
      setAgregando(false);
      setErrorNueva("");
    } catch (e) {
      setErrorNueva((e as Error).message);
    } finally {
      setGuardando(false);
    }
  }

  return (
    <div className={estilos.contenedor} role="group" aria-labelledby={idTitulo} aria-describedby={idAyuda}>
      <span id={idTitulo} className={estilos.titulo}>
        Tipo de actividad {requerido ? null : <em>(opcional mientras no esté activo)</em>}
      </span>
      <p id={idAyuda} className={error ? estilos.ayudaError : estilos.ayuda} role={error ? "alert" : undefined}>
        {error || "Con esto se personalizan las recomendaciones según lo que le gustó a cada estudiante. Puedes elegir varias."}
      </p>

      <div className={estilos.pastillas}>
        {catalogo.map((e) => {
          const marcada = seleccion.includes(e.id);
          return (
            <button
              key={e.id}
              type="button"
              className={marcada ? estilos.marcada : estilos.pastilla}
              aria-pressed={marcada}
              disabled={deshabilitado}
              onClick={() => onCambio(alternar(seleccion, e.id))}
            >
              <AnimatePresence initial={false}>
                {marcada && (
                  <motion.span
                    className={estilos.check}
                    aria-hidden="true"
                    initial={reducir ? false : { opacity: 0, scale: 0.6 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.6 }}
                    transition={{ duration: 0.16, ease: SALIDA }}
                  >
                    <IconoCheck tamano={14} />
                  </motion.span>
                )}
              </AnimatePresence>
              {e.nombre}
            </button>
          );
        })}

        {!agregando && (
          <button type="button" className={estilos.agregar} disabled={deshabilitado} onClick={() => setAgregando(true)}>
            <IconoMas tamano={14} /> Nueva etiqueta
          </button>
        )}
      </div>

      {agregando && (
        <div className={estilos.nueva}>
          <input
            value={nombre}
            maxLength={LARGO_MAXIMO_ETIQUETA}
            placeholder="Ej: Voluntariado"
            aria-label="Nombre de la etiqueta nueva"
            aria-invalid={!!errorNueva}
            autoFocus
            onChange={(e) => {
              setNombre(e.target.value);
              if (errorNueva) setErrorNueva("");
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                void agregar();
              }
              if (e.key === "Escape") {
                e.stopPropagation();
                setAgregando(false);
              }
            }}
          />
          <button type="button" className="hz-btn" disabled={guardando} onClick={() => void agregar()}>
            {guardando ? "Agregando…" : "Agregar"}
          </button>
          <button type="button" className={estilos.cancelar} disabled={guardando} onClick={() => setAgregando(false)}>
            Cancelar
          </button>
          {errorNueva && (
            <small className={estilos.errorNueva} role="alert">
              {errorNueva}
            </small>
          )}
        </div>
      )}
    </div>
  );
}
