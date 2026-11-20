"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { IconoCheck, IconoFlechaDerecha } from "@/components/app/Iconos";
import IconoDuo from "@/components/app/IconosDuo";
import { RESORTE_TOQUE, SALIDA } from "@/components/app/Movimiento";
import { ErrorApi } from "@/lib/api";
import { saludar } from "@/lib/conexiones";
import { CompaneroCompatible, ResultadoMatching, buscarCompaneros } from "@/lib/matching";
import { Puente } from "@/lib/puentes";
import { iniciales } from "@/lib/reglasHoyCampus";
import estilos from "./LibresContigoAhora.module.css";

const LIBRES = "/comunidades?pestana=libres";
/** Tonos de los avatares: se elige por el id, así cada compañero conserva su color. */
const TONOS = ["tonoA", "tonoB", "tonoC", "tonoD"] as const;

type Props = {
  puente: Puente | undefined;
  /** «hoy de 10:30 a 12:00» o «el martes de 09:30 a 11:00»: a qué puente se refiere. */
  cuando: string;
  className?: string;
};

/**
 * «Libres contigo»: hasta 3 compañeros de tu sede libres a la misma hora (matching de HU12), con su compatibilidad y
 * un «Saludar» rápido. Sin test o sin consentimiento invita a activarlo; sin sede no se muestra (Sugerencias ya lo pide).
 */
export default function LibresContigoAhora({ puente, cuando, className = "" }: Props) {
  const reducir = useReducedMotion();
  const [resultado, setResultado] = useState<{ puenteId: number; datos: ResultadoMatching | null } | null>(null);
  const [saludados, setSaludados] = useState<Record<number, "enviando" | "enviado">>({});
  const [error, setError] = useState("");

  useEffect(() => {
    if (!puente) return;
    const puenteId = puente.id;
    let activo = true;
    buscarCompaneros(puenteId)
      .then((datos) => {
        if (activo) setResultado({ puenteId, datos });
      })
      .catch(() => {
        if (activo) setResultado({ puenteId, datos: null });
      });
    return () => {
      activo = false;
    };
  }, [puente]);

  const datos = puente && resultado?.puenteId === puente.id ? resultado.datos : undefined;
  if (!puente || datos === null || datos?.estado === "SIN_ZONA") return null;

  async function enviarSaludo(c: CompaneroCompatible) {
    if (!puente || saludados[c.usuarioId]) return;
    setError("");
    setSaludados((s) => ({ ...s, [c.usuarioId]: "enviando" }));
    try {
      await saludar(c.usuarioId, puente.id);
      setSaludados((s) => ({ ...s, [c.usuarioId]: "enviado" }));
    } catch (e) {
      setSaludados((s) => {
        const resto = { ...s };
        delete resto[c.usuarioId];
        return resto;
      });
      setError(e instanceof ErrorApi || e instanceof Error ? e.message : "No se pudo enviar el saludo.");
    }
  }

  function accion(c: CompaneroCompatible) {
    const local = saludados[c.usuarioId];
    if (local === "enviado" || (c.conexion?.estado === "PENDIENTE" && c.conexion.enviadaPorMi))
      return (
        <span className={estilos.estado}>
          <IconoCheck tamano={13} /> Saludo enviado
        </span>
      );
    if (c.conexion?.estado === "ACEPTADA") return <span className={estilos.estadoConectado}>Conectados</span>;
    if (c.conexion?.estado === "PENDIENTE")
      return (
        <Link href={LIBRES} className={estilos.responder}>
          Te saludó
        </Link>
      );
    if (c.conexion?.estado === "RECHAZADA") return null;
    return (
      <motion.button
        type="button"
        className={estilos.saludar}
        onClick={() => void enviarSaludo(c)}
        disabled={local === "enviando"}
        whileTap={reducir ? undefined : { scale: 0.95 }}
        transition={RESORTE_TOQUE}
      >
        {local === "enviando" ? "…" : "Saludar"}
      </motion.button>
    );
  }

  const invitacion = (titulo: string, texto: string, boton: string) => (
    <div className={estilos.invitacion}>
      <span className={estilos.invitacionIcono} aria-hidden="true">
        <IconoDuo nombre="charla" tamano={28} vivo />
      </span>
      <span>
        <strong>{titulo}</strong>
        <small>{texto}</small>
      </span>
      <Link href={LIBRES} className={`hz-btn hz-btn-primary ${estilos.invitacionBoton}`}>
        {boton}
      </Link>
    </div>
  );

  return (
    <motion.section
      className={`${estilos.tarjeta} ${className}`}
      aria-labelledby="titulo-libres-contigo"
      initial={reducir ? false : { opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, ease: SALIDA, delay: 0.08 }}
    >
      <div className={estilos.encabezado}>
        <div>
          <h2 id="titulo-libres-contigo">Libres contigo</h2>
          <p className={estilos.cuando}>{cuando}</p>
        </div>
        {datos?.estado === "OK" && (
          <Link href={LIBRES} className={estilos.verTodo}>
            Ver todos <IconoFlechaDerecha tamano={14} />
          </Link>
        )}
      </div>

      {datos === undefined ? (
        <div className={estilos.cargando} role="status" aria-label="Buscando compañeros libres">
          <span />
          <span />
        </div>
      ) : datos.estado === "SIN_TEST" ? (
        invitacion("Descubre quién es como tú", "Responde el test de afinidad (11 preguntas) y te mostramos a quién le sobra el mismo rato.", "Hacer el test")
      ) : datos.estado === "NO_VISIBLE" ? (
        invitacion("Activa «Libres contigo»", "Así ves quién está libre a tu misma hora, y ellos a ti. Lo puedes apagar cuando quieras.", "Activar")
      ) : datos.estado !== "OK" || datos.companeros.length === 0 ? (
        <p className={estilos.vacio}>Nadie más de tu sede está libre a esa hora todavía. Te lo mostramos apenas aparezca alguien.</p>
      ) : (
        <ul className={estilos.lista}>
          <AnimatePresence initial={false}>
            {datos.companeros.slice(0, 3).map((c) => (
              <motion.li key={c.usuarioId} className={estilos.companero} layout={!reducir}>
                <span className={`${estilos.avatar} ${estilos[TONOS[c.usuarioId % TONOS.length]]}`} aria-hidden="true">
                  {iniciales(c.nombre)}
                </span>
                <span className={estilos.datos}>
                  <strong>{c.nombre}</strong>
                  <small>
                    {c.carrera ?? "Estudiante UCB"} · {c.minutosEnComun} min en común
                  </small>
                </span>
                <span className={estilos.compatibilidad} title="Compatibilidad: 60 % test y 40 % tiempo libre">
                  {c.compatibilidad}%
                </span>
                <span className={estilos.accion}>{accion(c)}</span>
              </motion.li>
            ))}
          </AnimatePresence>
        </ul>
      )}

      {error && (
        <p className={estilos.error} role="alert">
          {error}
        </p>
      )}
    </motion.section>
  );
}
