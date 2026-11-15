"use client";

import Link from "next/link";
import { useEffect, useState, useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { IconoBandera, IconoCheck } from "@/components/app/Iconos";
import { LARGO_MAXIMO_REPORTE, MOTIVO_DE_OBJETO, enviarReporte, errorDescripcionReporte } from "@/lib/reportes";
import estilos from "./ReportarObjeto.module.css";

const sinSuscripcion = () => () => {};

const TEXTOS = {
  COMUNIDAD: {
    titulo: "Reportar comunidad",
    ejemplo: "Ej: el nombre de la actividad tiene insultos.",
  },
  EVENTO: {
    titulo: "Reportar evento",
    ejemplo: "Ej: el afiche tiene contenido ofensivo o el evento es falso.",
  },
} as const;

type Props = {
  tipo: keyof typeof MOTIVO_DE_OBJETO;
  /** Id de la comunidad o del evento. */
  id: number;
  /** Actividad de la comunidad o título del evento (se muestra en el diálogo). */
  nombre: string;
  className?: string;
};

/**
 * HU27: botón «Reportar» de una comunidad o un evento. Envía COMUNIDAD_INAPROPIADA con `comunidadId` o EVENTO_INAPROPIADO
 * con `eventoId`; los límites (uno pendiente por objeto, 5 pendientes en total) los dice el backend y se muestran tal cual.
 */
export default function ReportarObjeto({ tipo, id, nombre, className }: Props) {
  const [abierto, setAbierto] = useState(false);

  return (
    <>
      <button
        type="button"
        className={`${estilos.disparador} ${className ?? ""}`}
        onClick={() => setAbierto(true)}
        aria-haspopup="dialog"
      >
        <IconoBandera tamano={16} />
        Reportar
      </button>
      <AnimatePresence>
        {abierto && <Dialogo tipo={tipo} id={id} nombre={nombre} onCerrar={() => setAbierto(false)} />}
      </AnimatePresence>
    </>
  );
}

function Dialogo({ tipo, id, nombre, onCerrar }: Omit<Props, "className"> & { onCerrar: () => void }) {
  const reducir = useReducedMotion();
  const enCliente = useSyncExternalStore(sinSuscripcion, () => true, () => false);
  const [texto, setTexto] = useState("");
  const [error, setError] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [enviadoId, setEnviadoId] = useState<number | null>(null);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => e.key === "Escape" && !enviando && onCerrar();
    window.addEventListener("keydown", alTeclear);
    return () => window.removeEventListener("keydown", alTeclear);
  }, [onCerrar, enviando]);

  if (!enCliente) return null;

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    if (enviando) return;
    const problema = errorDescripcionReporte(texto);
    if (problema) return setError(problema);
    setError("");
    setEnviando(true);
    try {
      const creado = await enviarReporte(
        MOTIVO_DE_OBJETO[tipo],
        texto.trim(),
        tipo === "COMUNIDAD" ? { comunidadId: id } : { eventoId: id },
      );
      setEnviadoId(creado.id);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setEnviando(false);
    }
  }

  const textos = TEXTOS[tipo];

  return createPortal(
    <motion.div
      className="hz-overlay"
      onMouseDown={(e) => e.target === e.currentTarget && !enviando && onCerrar()}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
    >
      <motion.div
        className={`hz-modal ${estilos.modal}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-reportar"
        initial={reducir ? false : { opacity: 0, scale: 0.95, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={reducir ? { opacity: 0 } : { opacity: 0, scale: 0.97, y: 12 }}
        transition={{ type: "spring", stiffness: 380, damping: 32 }}
      >
        {enviadoId === null ? (
          <form className={estilos.formulario} onSubmit={enviar} noValidate>
            <div className="hz-modal-cabecera">
              <span className={`hz-modal-icono ${estilos.icono}`} aria-hidden="true">
                <IconoBandera tamano={20} />
              </span>
              <div className={estilos.titulos}>
                <h2 id="titulo-reportar">{textos.titulo}</h2>
                <p className={estilos.nombre}>{nombre}</p>
              </div>
            </div>
            <p className={estilos.ayuda}>
              Cuéntanos qué no cumple las <Link href="/normas">normas de la comunidad</Link>. Lo revisa una persona del equipo
              y quien lo creó no sabe que fuiste tú.
            </p>
            <label className={estilos.campo}>
              Qué pasó
              <textarea
                rows={4}
                value={texto}
                maxLength={LARGO_MAXIMO_REPORTE}
                placeholder={textos.ejemplo}
                aria-invalid={!!error}
                aria-describedby="contador-reportar"
                autoFocus
                onChange={(e) => {
                  setTexto(e.target.value);
                  if (error) setError("");
                }}
              />
              <span className={estilos.pie}>
                {error ? (
                  <motion.small
                    key={error}
                    className={estilos.error}
                    role="alert"
                    animate={reducir ? undefined : { x: [0, -6, 6, -3, 3, 0] }}
                    transition={{ duration: 0.4 }}
                  >
                    {error}
                  </motion.small>
                ) : (
                  <small>Entre 10 y {LARGO_MAXIMO_REPORTE} caracteres.</small>
                )}
                <small id="contador-reportar" className={estilos.contador}>
                  {texto.length}/{LARGO_MAXIMO_REPORTE}
                </small>
              </span>
            </label>
            <div className="hz-acciones">
              <button type="button" className="hz-btn" onClick={onCerrar} disabled={enviando}>
                Cancelar
              </button>
              <span className="hz-espacio" />
              <button type="submit" className="hz-btn hz-btn-primary" disabled={enviando}>
                {enviando ? "Enviando…" : "Enviar reporte"}
              </button>
            </div>
          </form>
        ) : (
          <div className={estilos.listo} role="status">
            <motion.span
              className={estilos.listoIcono}
              aria-hidden="true"
              initial={reducir ? false : { scale: 0.6, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              transition={{ type: "spring", stiffness: 420, damping: 18 }}
            >
              <IconoCheck tamano={26} />
            </motion.span>
            <h2 id="titulo-reportar">¡Gracias por avisar!</h2>
            <p>Lo vamos a revisar. Te avisaremos en la campanita cuando haya una respuesta.</p>
            <div className="hz-acciones">
              <Link className="hz-btn" href={`/perfil?pestana=mis-reportes&reporte=${enviadoId}`}>
                Ver en Mis reportes
              </Link>
              <span className="hz-espacio" />
              <button type="button" className="hz-btn hz-btn-primary" onClick={onCerrar} autoFocus>
                Listo
              </button>
            </div>
          </div>
        )}
      </motion.div>
    </motion.div>,
    document.body,
  );
}
