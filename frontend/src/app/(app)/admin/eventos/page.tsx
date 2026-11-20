"use client";

import { useCallback, useEffect, useState } from "react";
import FotoEvento from "@/components/eventos/FotoEvento";
import FondoVivo from "@/components/app/FondoVivo";
import fotoEventos from "@/components/app/fotos/eventos.webp";
import { IconoCalendario, IconoCheck, IconoPin, IconoReloj } from "@/components/app/Iconos";
import { Entrada, Odometro, Pieza, seguirPuntero } from "@/components/app/Movimiento";
import { ErrorApi } from "@/lib/api";
import { EventoCampus, listarEventosPendientes, moderarEvento } from "@/lib/eventos";
import { formatearFechaHora } from "@/lib/reglasFlyer";
import { useRefrescoAutomatico } from "@/lib/useRefrescoAutomatico";
import estilos from "./eventos.module.css";

export default function EventosPendientesPage() {
  const [eventos, setEventos] = useState<EventoCampus[]>([]);
  const [motivos, setMotivos] = useState<Record<number, string>>({});
  const [cargando, setCargando] = useState(true);
  const [procesando, setProcesando] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  // Sin botón «Actualizar»: un evento nuevo en revisión (aviso en vivo) o volver a la pestaña trae la lista al día, sin parpadeo.
  const refrescar = useCallback(() => {
    listarEventosPendientes()
      .then((pendientes) => {
        setEventos(pendientes);
        setError("");
      })
      .catch(() => undefined);
  }, []);
  useRefrescoAutomatico(refrescar);

  useEffect(() => {
    let activo = true;
    listarEventosPendientes()
      .then((pendientes) => {
        if (activo) setEventos(pendientes);
      })
      .catch((e: unknown) => {
        if (activo) setError(e instanceof Error ? e.message : "No se pudieron cargar los eventos pendientes.");
      })
      .finally(() => {
        if (activo) setCargando(false);
      });
    return () => { activo = false; };
  }, []);

  async function resolver(evento: EventoCampus, estado: "PUBLICADO" | "RECHAZADO") {
    const motivo = motivos[evento.id]?.trim() ?? "";
    if (estado === "RECHAZADO" && !motivo) {
      setError(`Escribe el motivo del rechazo de «${evento.titulo}».`);
      return;
    }

    setProcesando(evento.id);
    setError("");
    setMensaje("");
    try {
      await moderarEvento(evento.id, { estado, motivo: estado === "RECHAZADO" ? motivo : null });
      setEventos((actuales) => actuales.filter((e) => e.id !== evento.id));
      setMensaje(estado === "PUBLICADO" ? "Evento aprobado y estudiante notificado." : "Evento rechazado y estudiante notificado.");
      setMotivos((actuales) => {
        const nuevos = { ...actuales };
        delete nuevos[evento.id];
        return nuevos;
      });
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : e instanceof Error ? e.message : "No se pudo revisar el evento.");
    } finally {
      setProcesando(null);
    }
  }

  return (
    <Entrada as="main" className={estilos.pagina}>
      {/* Misma portada que Eventos del estudiante: foto, luz que sigue al cursor y pastilla con ícono. */}
      <Pieza as="header">
        <div className={estilos.portada} onPointerMove={seguirPuntero}>
          <FondoVivo foto={fotoEventos} enfoque="72% 45%" />
          <span className={estilos.etiqueta}>
            <IconoCalendario tamano={14} /> Moderación
          </span>
          <h1>Eventos pendientes</h1>
          <p>Revisa el motivo y decide si publicar o rechazar cada evento. Quien lo creó recibe un aviso con el resultado.</p>
          {!cargando && eventos.length > 0 && (
            <div className={estilos.cifrasPortada} aria-live="polite">
              <span>
                <b><Odometro valor={eventos.length} /></b> {eventos.length === 1 ? "evento en revisión" : "eventos en revisión"}
              </span>
            </div>
          )}
        </div>
      </Pieza>

      {error && <p className={`hz-banner ${estilos.banner}`} role="alert">{error}</p>}
      {mensaje && <p className={estilos.mensaje} role="status"><IconoCheck tamano={16} /> {mensaje}</p>}

      <Pieza as="section" className={estilos.lista}>
        <h2 className={estilos.tituloLista}>En revisión</h2>

        {cargando ? (
          <div className={estilos.cargando} role="status" aria-label="Cargando eventos">
            <span className="hz-skeleton" />
            <span className="hz-skeleton" />
          </div>
        ) : eventos.length === 0 ? (
          <div className={estilos.vacio}>
            <span className={estilos.vacioIcono} aria-hidden="true"><IconoCheck tamano={26} /></span>
            <strong>Todo al día</strong>
            <span>No hay eventos pendientes de revisión. Cuando alguien publique uno que necesite revisión, aparece aquí solo.</span>
          </div>
        ) : eventos.map((evento) => (
          <article key={evento.id} className={estilos.evento}>
            {evento.fotoUrl && <FotoEvento ruta={evento.fotoUrl} titulo={evento.titulo} />}
            <div className={estilos.detalle}>
              <span className={estilos.estado}>En revisión</span>
              <h3>{evento.titulo}</h3>
              <ul className={estilos.datos}>
                <li><IconoReloj tamano={15} /> {formatearFechaHora(evento.inicio)} – {formatearFechaHora(evento.fin)}</li>
                <li><IconoPin tamano={15} /> {evento.lugarId === null ? `Fuera del campus · ${evento.lugar}` : evento.lugar}</li>
                <li><IconoCalendario tamano={15} /> {evento.asistencia === "FIJA" ? "Hay que llegar a la hora" : `Asistencia libre, ${evento.visitaMin}–${evento.visitaMax} min`}</li>
              </ul>
              {evento.descripcion && <p className={estilos.descripcion}>{evento.descripcion}</p>}
              {evento.motivo && <p className={estilos.motivo}><strong>Por qué está en revisión:</strong> {evento.motivo}</p>}
              {evento.estado === "PENDIENTE" && (
                <>
                  <label className={`hz-campo ${estilos.campo}`}>
                    <span>Motivo si lo rechazas <em>(obligatorio para rechazar)</em></span>
                    <textarea maxLength={500} rows={3} value={motivos[evento.id] ?? ""} onChange={(e) => setMotivos((actuales) => ({ ...actuales, [evento.id]: e.target.value }))} />
                  </label>
                  <div className={estilos.acciones}>
                    <button type="button" className="hz-btn hz-btn-peligro" disabled={procesando !== null} onClick={() => void resolver(evento, "RECHAZADO")}>
                      {procesando === evento.id ? "Guardando…" : "Rechazar"}
                    </button>
                    <button type="button" className="hz-btn hz-btn-primary" disabled={procesando !== null} onClick={() => void resolver(evento, "PUBLICADO")}>
                      {procesando === evento.id ? "Guardando…" : "Aprobar y publicar"}
                    </button>
                  </div>
                </>
              )}
            </div>
          </article>
        ))}
      </Pieza>
    </Entrada>
  );
}
