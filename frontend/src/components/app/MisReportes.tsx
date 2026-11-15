"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { motion, useReducedMotion } from "motion/react";
import { SALIDA } from "@/components/app/Movimiento";
import {
  ETIQUETA_ESTADO,
  ETIQUETA_MOTIVO,
  MAXIMO_PENDIENTES,
  MiReporte,
  ObjetoReporte,
  listarMisReportes,
} from "@/lib/reportes";
import { useEventoEnVivo } from "@/lib/tiempoReal";
import estilos from "./MisReportes.module.css";

const NOMBRE_OBJETO: Record<ObjetoReporte, string> = { LUGAR: "Lugar", COMUNIDAD: "Comunidad", EVENTO: "Evento" };

const FECHA = new Intl.DateTimeFormat("es-BO", { day: "numeric", month: "short", timeZone: "America/La_Paz" });
const fecha = (iso: string) => FECHA.format(new Date(iso)).replace(".", "");

type Props = {
  /** El reporte al que llevó el aviso de la campanita: se resalta y se desplaza hasta él. */
  destacado?: number | null;
  /** Lleva al formulario de «Reportar» desde el estado vacío. */
  onReportar?: () => void;
};

/**
 * HU27: «Mis reportes». Cada reporte con su motivo, a qué apunta, la fecha, el estado y la respuesta del administrador;
 * los pendientes dicen que siguen en revisión, sin respuesta. Se actualiza en vivo cuando llega el aviso de la revisión.
 */
export default function MisReportes({ destacado = null, onReportar }: Props) {
  const reducir = useReducedMotion();
  const [reportes, setReportes] = useState<MiReporte[] | null>(null);
  const [error, setError] = useState("");
  const yaDesplazo = useRef(false);

  const cargar = useCallback(() => {
    listarMisReportes()
      .then((lista) => {
        setReportes(lista);
        setError("");
      })
      .catch((e: Error) => setError(e.message));
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  // Si el admin responde mientras está mirando, la lista cambia sola.
  useEventoEnVivo("avisosCambiaron", cargar);

  useEffect(() => {
    if (!reportes || !destacado || yaDesplazo.current) return;
    const tarjeta = document.getElementById(`reporte-${destacado}`);
    if (!tarjeta) return;
    yaDesplazo.current = true;
    tarjeta.scrollIntoView({ behavior: reducir ? "auto" : "smooth", block: "center" });
  }, [reportes, destacado, reducir]);

  const pendientes = reportes?.filter((r) => r.estado === "PENDIENTE").length ?? 0;

  return (
    <div className={estilos.contenedor}>
      <div className={estilos.cabecera}>
        <h2 className={estilos.titulo}>Mis reportes</h2>
        {reportes && reportes.length > 0 && (
          <span className={pendientes >= MAXIMO_PENDIENTES ? estilos.cupoLleno : estilos.cupo}>
            {pendientes} de {MAXIMO_PENDIENTES} en revisión
          </span>
        )}
      </div>

      {error && (
        <div className={estilos.error} role="alert">
          <p>{error}</p>
          <button type="button" className={estilos.reintentar} onClick={cargar}>
            Reintentar
          </button>
        </div>
      )}

      {!error && reportes === null && (
        <ul className={estilos.lista} aria-busy="true" aria-label="Cargando tus reportes">
          {[0, 1].map((i) => (
            <li key={i} className={estilos.esqueleto} />
          ))}
        </ul>
      )}

      {reportes && reportes.length === 0 && (
        <div className={estilos.vacio}>
          <p className={estilos.vacioTitulo}>Todavía no enviaste reportes</p>
          <p>Si algo no funciona, un lugar tiene datos incorrectos o una comunidad o un evento no cumple las normas, cuéntanos.</p>
          {onReportar && (
            <button type="button" className={estilos.reintentar} onClick={onReportar}>
              Reportar algo
            </button>
          )}
        </div>
      )}

      {reportes && reportes.length > 0 && (
        <motion.ul
          className={estilos.lista}
          initial={reducir ? false : "oculto"}
          animate="visible"
          variants={{ oculto: {}, visible: { transition: { staggerChildren: 0.05 } } }}
        >
          {reportes.map((r) => {
            const pendiente = r.estado === "PENDIENTE";
            return (
              <motion.li
                key={r.id}
                id={`reporte-${r.id}`}
                className={`${estilos.tarjeta} ${estilos[r.estado.toLowerCase()]} ${r.id === destacado ? estilos.destacado : ""}`}
                variants={{
                  oculto: { opacity: 0, y: 10 },
                  visible: { opacity: 1, y: 0, transition: { duration: 0.3, ease: SALIDA } },
                }}
              >
                <div className={estilos.fila}>
                  <div className={estilos.queEs}>
                    <strong className={estilos.motivo}>{ETIQUETA_MOTIVO[r.motivo] ?? r.motivo}</strong>
                    {r.objetoTipo && (
                      <span className={estilos.objeto}>
                        {NOMBRE_OBJETO[r.objetoTipo]}
                        {r.objeto ? ` · ${r.objeto}` : ""}
                      </span>
                    )}
                  </div>
                  <span className={estilos.estado}>{ETIQUETA_ESTADO[r.estado]}</span>
                </div>

                {r.descripcion && <p className={estilos.descripcion}>{r.descripcion}</p>}

                {pendiente ? (
                  <p className={estilos.espera}>Sin respuesta todavía: te avisaremos en la campanita.</p>
                ) : r.respuesta ? (
                  <div className={estilos.respuesta}>
                    <span className={estilos.respuestaTitulo}>Respuesta del equipo</span>
                    <p>{r.respuesta}</p>
                  </div>
                ) : (
                  <p className={estilos.espera}>
                    {r.estado === "RESUELTO" ? "Lo revisamos y lo resolvimos." : "Lo revisamos y no hizo falta cambiar nada."}
                  </p>
                )}

                <p className={estilos.fechas}>
                  <time dateTime={r.creadoEn}>Enviado el {fecha(r.creadoEn)}</time>
                  {r.revisadoEn && (
                    <>
                      <span aria-hidden="true"> · </span>
                      <time dateTime={r.revisadoEn}>Revisado el {fecha(r.revisadoEn)}</time>
                    </>
                  )}
                </p>
              </motion.li>
            );
          })}
        </motion.ul>
      )}
    </div>
  );
}
