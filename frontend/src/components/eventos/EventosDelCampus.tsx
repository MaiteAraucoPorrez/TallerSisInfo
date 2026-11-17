"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import FotoEvento from "@/components/eventos/FotoEvento";
import { EventoPublico, listarEventosDelCampus, RangoEventos } from "@/lib/eventos";
import { ahoraBoliviaParaFormulario, fechaBoliviaParaFormulario, rangoLegible } from "@/lib/reglasEventos";
import estilos from "./EventosDelCampus.module.css";

const RANGOS: { valor: RangoEventos; etiqueta: string; vacio: string }[] = [
  { valor: "hoy", etiqueta: "Hoy", vacio: "No hay eventos publicados para hoy." },
  { valor: "semana", etiqueta: "Esta semana", vacio: "No hay eventos publicados para esta semana." },
];

type Respuesta = { clave: string; eventos: EventoPublico[]; error: string };

/** HU22: eventos publicados y vigentes del campus, con las pestañas Hoy y Esta semana. */
export default function EventosDelCampus() {
  const [rango, setRango] = useState<RangoEventos>("hoy");
  const [intento, setIntento] = useState(0);
  const [respuesta, setRespuesta] = useState<Respuesta | null>(null);
  const clave = `${rango}:${intento}`;

  useEffect(() => {
    let activo = true;
    listarEventosDelCampus(rango)
      .then((eventos) => {
        if (activo) setRespuesta({ clave, eventos, error: "" });
      })
      .catch((e: unknown) => {
        if (activo) {
          setRespuesta({
            clave,
            eventos: [],
            error: e instanceof Error ? e.message : "No se pudieron cargar los eventos.",
          });
        }
      });
    return () => {
      activo = false;
    };
  }, [rango, intento, clave]);

  // Mientras llega la respuesta de la pestaña actual no se muestra la de la anterior.
  const actual = respuesta?.clave === clave ? respuesta : null;
  const hoy = ahoraBoliviaParaFormulario().slice(0, 10);
  const vacio = RANGOS.find((r) => r.valor === rango)?.vacio ?? "";

  return (
    <section className={estilos.seccion} aria-busy={!actual}>
      <div className={estilos.cabecera}>
        <div>
          <h2>Eventos del campus</h2>
          <p>Lo que pasa en la universidad: solo eventos publicados que aún no terminaron.</p>
        </div>
        <div className={estilos.pestanas} role="tablist" aria-label="Rango de eventos">
          {RANGOS.map((r) => (
            <button
              key={r.valor}
              type="button"
              role="tab"
              aria-selected={rango === r.valor}
              className={rango === r.valor ? estilos.pestanaActiva : estilos.pestana}
              onClick={() => setRango(r.valor)}
            >
              {r.etiqueta}
            </button>
          ))}
        </div>
      </div>

      {!actual ? (
        <p className={estilos.vacio} role="status">Cargando eventos…</p>
      ) : actual.error ? (
        <div className={estilos.error} role="alert">
          <span>{actual.error}</span>
          <button type="button" className={estilos.reintentar} onClick={() => setIntento((n) => n + 1)}>
            Reintentar
          </button>
        </div>
      ) : actual.eventos.length === 0 ? (
        <div className={estilos.vacio}>
          <strong>{vacio}</strong>
          <span>Cuando se publique alguno, aparecerá aquí.</span>
        </div>
      ) : (
        <ul className={estilos.lista}>
          {actual.eventos.map((evento) => (
            <li key={evento.id} className={estilos.tarjeta}>
              {evento.fotoUrl && <FotoEvento ruta={evento.fotoUrl} titulo={evento.titulo} />}
              <div className={estilos.cuerpo}>
                <h3>{evento.titulo}</h3>
                <p className={estilos.hora}>
                  {rangoLegible(fechaBoliviaParaFormulario(evento.inicio), fechaBoliviaParaFormulario(evento.fin), hoy)}
                </p>
                <p className={estilos.lugar}>
                  {evento.lugar}
                  {evento.fueraDelCampus && <span className={estilos.fuera}>Fuera del campus</span>}
                </p>
              </div>
              <Link href={`/eventos/${evento.id}`} className={estilos.detalles}>
                Ver detalles
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
