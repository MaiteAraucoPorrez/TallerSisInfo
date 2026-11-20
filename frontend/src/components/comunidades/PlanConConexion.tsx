"use client";

import Link from "next/link";
import { useEffect, useState, useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { IconoCalendario, IconoCheck, IconoPersonas, IconoPin, IconoReloj } from "@/components/app/Iconos";
import { RESORTE_TOQUE, SALIDA } from "@/components/app/Movimiento";
import { Conexion, OpcionesPlan, crearPlan, invitarConexion, obtenerOpcionesPlan } from "@/lib/conexiones";
import { LugarViable, buscarLugaresViables } from "@/lib/lugares";
import estilos from "./PlanConConexion.module.css";

const sinSuscripcion = () => () => {};
type Pestana = "plan" | "invitar";
type Hecho = { mensaje: string; comunidadId?: number; eventoId?: number };

/**
 * «Armar plan» / «Invitar a…» con una conexión de «Libres contigo». Todo es de hoy, en el rato libre que comparten:
 * el plan es una comunidad privada para los dos en un lugar de las sugerencias; invitar suma a la conexión a una
 * comunidad tuya o le avisa de un evento de hoy.
 */
export default function PlanConConexion({ conexion, onCerrar }: { conexion: Conexion; onCerrar: () => void }) {
  const reducir = useReducedMotion();
  const enCliente = useSyncExternalStore(sinSuscripcion, () => true, () => false);
  const [opciones, setOpciones] = useState<OpcionesPlan | null>(null);
  const [error, setError] = useState("");
  const [pestana, setPestana] = useState<Pestana>("plan");
  const [tramo, setTramo] = useState<number | null>(null);
  const [lugares, setLugares] = useState<{ puenteId: number; lista: LugarViable[] | null } | null>(null);
  const [lugar, setLugar] = useState<number | null>(null);
  const [enviando, setEnviando] = useState<string | null>(null);
  const [invitados, setInvitados] = useState<Set<string>>(new Set());
  const [hecho, setHecho] = useState<Hecho | null>(null);
  const primerNombre = conexion.nombre.split(" ")[0];

  useEffect(() => {
    let activo = true;
    obtenerOpcionesPlan(conexion.id)
      .then((o) => {
        if (!activo) return;
        setOpciones(o);
        setTramo(o.tramos[0]?.puenteId ?? null);
        if (o.tramos.length === 0 && o.comunidades.length + o.eventos.length > 0) setPestana("invitar");
      })
      .catch((e: Error) => activo && setError(e.message));
    return () => {
      activo = false;
    };
  }, [conexion.id]);

  // Los lugares de las sugerencias para el rato elegido (los mismos de «Sugerencias rápidas»).
  useEffect(() => {
    if (tramo === null) return;
    let activo = true;
    buscarLugaresViables(tramo)
      .then((r) => activo && setLugares({ puenteId: tramo, lista: r.estado === "OK" ? r.lugares.slice(0, 6) : [] }))
      .catch(() => activo && setLugares({ puenteId: tramo, lista: [] }));
    return () => {
      activo = false;
    };
  }, [tramo]);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => e.key === "Escape" && !enviando && onCerrar();
    window.addEventListener("keydown", alTeclear);
    return () => window.removeEventListener("keydown", alTeclear);
  }, [onCerrar, enviando]);

  if (!enCliente) return null;

  const lugaresDelTramo = lugares && lugares.puenteId === tramo ? lugares.lista : null;
  const elegido = opciones?.tramos.find((t) => t.puenteId === tramo);
  const invitables = (opciones?.comunidades.length ?? 0) + (opciones?.eventos.length ?? 0);

  async function proponer() {
    if (tramo === null || lugar === null || enviando) return;
    setEnviando("plan");
    setError("");
    try {
      const r = await crearPlan(conexion.id, tramo, lugar);
      setHecho({ mensaje: r.mensaje, comunidadId: r.comunidadId });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setEnviando(null);
    }
  }

  async function invitar(clave: string, destino: { comunidadId: number } | { eventoId: number }) {
    if (enviando) return;
    setEnviando(clave);
    setError("");
    try {
      await invitarConexion(conexion.id, destino);
      setInvitados((s) => new Set(s).add(clave));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setEnviando(null);
    }
  }

  const botonInvitar = (clave: string, destino: { comunidadId: number } | { eventoId: number }) =>
    invitados.has(clave) ? (
      <span className={estilos.invitado}>
        <IconoCheck tamano={14} /> Invitado
      </span>
    ) : (
      <button type="button" className="hz-btn" disabled={enviando !== null} onClick={() => void invitar(clave, destino)}>
        {enviando === clave ? "Invitando…" : "Invitar"}
      </button>
    );

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
        aria-labelledby="titulo-plan-conexion"
        initial={reducir ? false : { opacity: 0, scale: 0.95, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={reducir ? { opacity: 0 } : { opacity: 0, scale: 0.97, y: 12 }}
        transition={{ type: "spring", stiffness: 380, damping: 32 }}
      >
        <header className={estilos.cabecera}>
          <span className={estilos.etiqueta}>
            <IconoPersonas tamano={14} /> Con {primerNombre}
          </span>
          <h2 id="titulo-plan-conexion">{hecho ? "¡Listo!" : "¿Qué hacen hoy?"}</h2>
          {!hecho && (
            <p>Todo es para hoy, en el rato libre que comparten. {primerNombre} recibe un aviso y elige si va.</p>
          )}
        </header>

        {hecho ? (
          <div className={estilos.hecho}>
            <motion.span
              className={estilos.hechoIcono}
              initial={reducir ? false : { scale: 0.6, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              transition={RESORTE_TOQUE}
              aria-hidden="true"
            >
              <IconoCheck tamano={28} />
            </motion.span>
            <p role="status">{hecho.mensaje}</p>
            <div className={estilos.acciones}>
              <button type="button" className="hz-btn" onClick={onCerrar}>
                Cerrar
              </button>
              {hecho.comunidadId && (
                <Link href={`/comunidades?pestana=descubrir&comunidad=${hecho.comunidadId}`} className="hz-btn hz-btn-primary" onClick={onCerrar}>
                  Ver el plan
                </Link>
              )}
            </div>
          </div>
        ) : !opciones && !error ? (
          <div className={estilos.cargando} role="status" aria-label="Buscando los ratos que comparten">
            <span />
            <span />
          </div>
        ) : (
          <>
            {opciones && (
              <div className={estilos.pestanas} role="tablist" aria-label="Qué quieres hacer">
                {(["plan", "invitar"] as const).map((p) => (
                  <button
                    key={p}
                    type="button"
                    role="tab"
                    aria-selected={pestana === p}
                    className={pestana === p ? estilos.pestanaActiva : estilos.pestana}
                    onClick={() => setPestana(p)}
                  >
                    {pestana === p && !reducir && <motion.span layoutId="pestana-plan" className={estilos.pestanaFondo} transition={RESORTE_TOQUE} />}
                    <span>{p === "plan" ? "Armar plan" : `Invitar a…${invitables > 0 ? ` (${invitables})` : ""}`}</span>
                  </button>
                ))}
              </div>
            )}

            <AnimatePresence mode="wait" initial={false}>
              {opciones && pestana === "plan" && (
                <motion.section
                  key="plan"
                  className={estilos.panel}
                  initial={reducir ? false : { opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -8 }}
                  transition={{ duration: 0.22, ease: SALIDA }}
                >
                  {opciones.tramos.length === 0 ? (
                    <p className={estilos.vacio}>
                      Hoy ya no les queda un rato libre en común (o alguno ya tiene algo a esa hora). Prueben otro día.
                    </p>
                  ) : (
                    <>
                      <h3 className={estilos.subtitulo}>Cuándo</h3>
                      <div className={estilos.tramos}>
                        {opciones.tramos.map((t) => (
                          <button
                            key={`${t.puenteId}-${t.horaInicio}`}
                            type="button"
                            className={tramo === t.puenteId ? estilos.tramoActivo : estilos.tramo}
                            aria-pressed={tramo === t.puenteId}
                            onClick={() => {
                              setTramo(t.puenteId);
                              setLugar(null);
                            }}
                          >
                            <IconoReloj tamano={15} /> Hoy {t.horaInicio}–{t.horaFin}
                            <small>{t.minutos} min</small>
                          </button>
                        ))}
                      </div>

                      <h3 className={estilos.subtitulo}>Dónde</h3>
                      {lugaresDelTramo === null ? (
                        <div className={estilos.cargando} aria-label="Buscando lugares">
                          <span />
                        </div>
                      ) : lugaresDelTramo.length === 0 ? (
                        <p className={estilos.vacio}>No encontramos lugares para ese rato. Prueben con otro horario.</p>
                      ) : (
                        <div className={estilos.lugares} role="radiogroup" aria-label="Lugar del plan">
                          {lugaresDelTramo.map((l) => (
                            <motion.button
                              key={l.lugarId}
                              type="button"
                              role="radio"
                              aria-checked={lugar === l.lugarId}
                              className={lugar === l.lugarId ? estilos.lugarActivo : estilos.lugar}
                              onClick={() => setLugar(l.lugarId)}
                              whileTap={reducir ? undefined : { scale: 0.97 }}
                              transition={RESORTE_TOQUE}
                            >
                              <strong>{l.nombre}</strong>
                              <small>
                                {l.categoria} · <IconoPin tamano={12} /> {l.distanciaM > 0 ? `${l.distanciaM} m` : "En el campus"}
                              </small>
                              {lugar === l.lugarId && (
                                <span className={estilos.marca} aria-hidden="true">
                                  <IconoCheck tamano={13} />
                                </span>
                              )}
                            </motion.button>
                          ))}
                        </div>
                      )}

                      <div className={estilos.pie}>
                        <small>Es un plan solo para ustedes dos: no aparece en Descubrir.</small>
                        <button
                          type="button"
                          className="hz-btn hz-btn-primary"
                          disabled={lugar === null || enviando !== null}
                          onClick={() => void proponer()}
                        >
                          {enviando === "plan" ? "Proponiendo…" : elegido ? `Proponer · ${elegido.horaInicio}` : "Proponer plan"}
                        </button>
                      </div>
                    </>
                  )}
                </motion.section>
              )}

              {opciones && pestana === "invitar" && (
                <motion.section
                  key="invitar"
                  className={estilos.panel}
                  initial={reducir ? false : { opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -8 }}
                  transition={{ duration: 0.22, ease: SALIDA }}
                >
                  {invitables === 0 ? (
                    <p className={estilos.vacio}>
                      No hay comunidades tuyas ni eventos de hoy en el rato que comparten. Pueden armar un plan.
                    </p>
                  ) : (
                    <>
                      {opciones.comunidades.length > 0 && (
                        <>
                          <h3 className={estilos.subtitulo}>Tus comunidades</h3>
                          <ul className={estilos.lista}>
                            {opciones.comunidades.map((c) => (
                              <li key={`c${c.id}`} className={estilos.fila}>
                                <span className={estilos.filaIcono} aria-hidden="true">
                                  <IconoPersonas tamano={18} />
                                </span>
                                <span className={estilos.filaTexto}>
                                  <strong>{c.actividad}</strong>
                                  <small>
                                    Hoy {c.horaInicio}–{c.horaFin} · {c.confirmados} de {c.maxParticipantes} van
                                  </small>
                                </span>
                                {botonInvitar(`c${c.id}`, { comunidadId: c.id })}
                              </li>
                            ))}
                          </ul>
                        </>
                      )}
                      {opciones.eventos.length > 0 && (
                        <>
                          <h3 className={estilos.subtitulo}>Eventos de hoy</h3>
                          <ul className={estilos.lista}>
                            {opciones.eventos.map((e) => (
                              <li key={`e${e.id}`} className={estilos.fila}>
                                <span className={estilos.filaIcono} aria-hidden="true">
                                  <IconoCalendario tamano={18} />
                                </span>
                                <span className={estilos.filaTexto}>
                                  <strong>{e.titulo}</strong>
                                  <small>
                                    Hoy {e.horaInicio}–{e.horaFin} · {e.lugar}
                                  </small>
                                </span>
                                {botonInvitar(`e${e.id}`, { eventoId: e.id })}
                              </li>
                            ))}
                          </ul>
                        </>
                      )}
                    </>
                  )}
                </motion.section>
              )}
            </AnimatePresence>
          </>
        )}

        {error && (
          <p className={estilos.error} role="alert">
            {error}
          </p>
        )}

        {!hecho && (
          <button type="button" className={estilos.cerrar} onClick={onCerrar} aria-label="Cerrar">
            ×
          </button>
        )}
      </motion.div>
    </motion.div>,
    document.body,
  );
}
