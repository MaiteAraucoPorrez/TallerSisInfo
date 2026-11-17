"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import FotoEvento from "@/components/eventos/FotoEvento";
import { EventoPublico, obtenerEventoDelCampus } from "@/lib/eventos";
import { ahoraBoliviaParaFormulario, fechaBoliviaParaFormulario, rangoLegible } from "@/lib/reglasEventos";
import estilos from "./detalle.module.css";

type Respuesta = { clave: string; evento: EventoPublico | null; error: string };

const textoAsistencia = (e: EventoPublico) =>
  e.asistencia === "FIJA"
    ? "Asistencia fija: hay que llegar a la hora."
    : e.visitaMin != null && e.visitaMax != null
      ? `Asistencia libre: puedes pasar entre ${e.visitaMin} y ${e.visitaMax} minutos.`
      : "Asistencia libre: puedes pasar un rato.";

/** HU22: detalle de un evento publicado. Si no existe o no está publicado, el servidor responde 404 y se muestra el mensaje. */
export default function DetalleEvento() {
  const { id } = useParams<{ id: string }>();
  const numero = /^\d+$/.test(id) ? Number(id) : null;
  const [intento, setIntento] = useState(0);
  const [respuesta, setRespuesta] = useState<Respuesta | null>(null);
  const clave = `${id}:${intento}`;

  useEffect(() => {
    if (numero === null) return;
    let activo = true;
    obtenerEventoDelCampus(numero)
      .then((evento) => {
        if (activo) setRespuesta({ clave, evento, error: "" });
      })
      .catch((e: unknown) => {
        if (activo) {
          setRespuesta({
            clave,
            evento: null,
            error: e instanceof Error ? e.message : "No se pudo cargar el evento.",
          });
        }
      });
    return () => {
      activo = false;
    };
  }, [numero, clave]);

  const actual = numero === null ? { clave, evento: null, error: "El evento no existe." } : respuesta?.clave === clave ? respuesta : null;
  const hoy = ahoraBoliviaParaFormulario().slice(0, 10);

  return (
    <div className={estilos.pagina}>
      <Link href="/eventos" className={estilos.volver}>
        ← Volver a eventos
      </Link>

      {!actual ? (
        <p className={estilos.estado} role="status">Cargando el evento…</p>
      ) : actual.error || !actual.evento ? (
        <div className={estilos.error} role="alert">
          <span>{actual.error || "El evento no existe."}</span>
          {numero !== null && (
            <button type="button" className={estilos.reintentar} onClick={() => setIntento((n) => n + 1)}>
              Reintentar
            </button>
          )}
        </div>
      ) : (
        <article className={estilos.tarjeta}>
          {actual.evento.fotoUrl && <FotoEvento ruta={actual.evento.fotoUrl} titulo={actual.evento.titulo} />}
          <h1>{actual.evento.titulo}</h1>
          <p className={estilos.hora}>
            {rangoLegible(
              fechaBoliviaParaFormulario(actual.evento.inicio),
              fechaBoliviaParaFormulario(actual.evento.fin),
              hoy,
            )}
          </p>
          <p className={estilos.lugar}>
            {actual.evento.lugar}
            {actual.evento.fueraDelCampus && <span className={estilos.fuera}>Fuera del campus</span>}
          </p>
          {actual.evento.descripcion && <p className={estilos.descripcion}>{actual.evento.descripcion}</p>}
          <p className={estilos.asistencia}>{textoAsistencia(actual.evento)}</p>
        </article>
      )}
    </div>
  );
}
