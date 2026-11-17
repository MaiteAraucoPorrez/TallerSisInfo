"use client";

import { useCallback, useEffect, useState } from "react";
import FotoEvento from "@/components/eventos/FotoEvento";
import { Entrada, Pieza } from "@/components/app/Movimiento";
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
      <Pieza as="header" className={estilos.portada}>
        <span className={estilos.etiqueta}>Moderación</span>
        <h1>Eventos pendientes</h1>
        <p>Revisa el motivo y decide si publicar o rechazar cada evento. El autor recibirá una notificación con el resultado.</p>
      </Pieza>

      {error && <p className={estilos.error} role="alert">{error}</p>}
      {mensaje && <p className={estilos.mensaje} role="status">{mensaje}</p>}

      <section className={estilos.lista} aria-busy={cargando}>
        <div className={estilos.encabezado}>
          <div><h2>En revisión</h2><p>{eventos.length} {eventos.length === 1 ? "evento" : "eventos"}</p></div>
        </div>

        {cargando ? <p className={estilos.vacio}>Cargando eventos…</p> : eventos.length === 0 ? (
          <p className={estilos.vacio}>No hay eventos pendientes de revisión.</p>
        ) : eventos.map((evento) => (
          <article key={evento.id} className={estilos.evento}>
            {evento.fotoUrl && <FotoEvento ruta={evento.fotoUrl} titulo={evento.titulo} />}
            <div className={estilos.detalle}>
              <h3>{evento.titulo}</h3>
              <p className={estilos.fecha}>{formatearFechaHora(evento.inicio)} – {formatearFechaHora(evento.fin)}</p>
              <p><strong>Lugar:</strong> {evento.lugarId === null ? `Fuera del campus · ${evento.lugar}` : evento.lugar}</p>
              <p><strong>Asistencia:</strong> {evento.asistencia === "FIJA" ? "Hay que llegar a la hora" : `Libre, ${evento.visitaMin}–${evento.visitaMax} min`}</p>
              {evento.descripcion && <p className={estilos.descripcion}>{evento.descripcion}</p>}
              {evento.motivo && <p className={estilos.motivo}><strong>Motivo de revisión:</strong> {evento.motivo}</p>}
              {evento.estado === "PENDIENTE" && (
                <>
                  <label className={estilos.campo}>
                    <span>Motivo si lo rechazas <small>(obligatorio para rechazar)</small></span>
                    <textarea maxLength={500} rows={3} value={motivos[evento.id] ?? ""} onChange={(e) => setMotivos((actuales) => ({ ...actuales, [evento.id]: e.target.value }))} />
                  </label>
                  <div className={estilos.acciones}>
                    <button type="button" className={estilos.secundario} disabled={procesando !== null} onClick={() => void resolver(evento, "RECHAZADO")}>
                      {procesando === evento.id ? "Guardando…" : "Rechazar"}
                    </button>
                    <button type="button" className={estilos.primario} disabled={procesando !== null} onClick={() => void resolver(evento, "PUBLICADO")}>
                      {procesando === evento.id ? "Guardando…" : "Aprobar y publicar"}
                    </button>
                  </div>
                </>
              )}
            </div>
          </article>
        ))}
      </section>
    </Entrada>
  );
}
