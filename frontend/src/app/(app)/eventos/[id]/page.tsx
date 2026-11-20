"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import FondoVivo from "@/components/app/FondoVivo";
import fotoEventos from "@/components/app/fotos/eventos.webp";
import { IconoCalendario, IconoFlechaIzquierda, IconoPin, IconoReloj } from "@/components/app/Iconos";
import { Entrada, Pieza, seguirPuntero } from "@/components/app/Movimiento";
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
    <Entrada as="main" className={estilos.pagina}>
      <Pieza>
        <Link href="/eventos" className={estilos.volver}>
          <IconoFlechaIzquierda tamano={18} /> Volver a eventos
        </Link>
      </Pieza>

      {!actual ? (
        <Pieza>
          <div className={estilos.cargando} role="status" aria-label="Cargando el evento">
            <span className="hz-skeleton" />
          </div>
        </Pieza>
      ) : actual.error || !actual.evento ? (
        <Pieza>
          <div className="hz-banner" role="alert">
            <span>{actual.error || "El evento no existe."}</span>
            {numero !== null && (
              <button type="button" className="hz-btn" onClick={() => setIntento((n) => n + 1)}>
                Reintentar
              </button>
            )}
          </div>
        </Pieza>
      ) : (
        <>
          {/* Portada como la de Eventos: foto con velo marino y luz que sigue al cursor. */}
          <Pieza as="header" className={estilos.portada} onPointerMove={seguirPuntero}>
            <FondoVivo foto={fotoEventos} enfoque="72% 45%" />
            <span className={estilos.etiqueta}>
              <IconoCalendario tamano={14} /> Evento del campus
            </span>
            <h1>{actual.evento.titulo}</h1>
            <div className={estilos.chips}>
              <span>
                <IconoReloj tamano={15} />
                {rangoLegible(
                  fechaBoliviaParaFormulario(actual.evento.inicio),
                  fechaBoliviaParaFormulario(actual.evento.fin),
                  hoy,
                )}
              </span>
              <span>
                <IconoPin tamano={15} /> {actual.evento.lugar}
              </span>
              {actual.evento.fueraDelCampus && <span className={estilos.fuera}>Fuera del campus</span>}
            </div>
          </Pieza>

          <Pieza as="section" className={estilos.tarjeta}>
            {actual.evento.fotoUrl && (
              <div className={estilos.foto}>
                <FotoEvento ruta={actual.evento.fotoUrl} titulo={actual.evento.titulo} />
              </div>
            )}
            <div className={estilos.cuerpo}>
              <h2>Sobre el evento</h2>
              {actual.evento.descripcion ? (
                <p className={estilos.descripcion}>{actual.evento.descripcion}</p>
              ) : (
                <p className={estilos.sinDescripcion}>Quien lo publicó no agregó una descripción.</p>
              )}
              <p className={estilos.asistencia}>
                <IconoCalendario tamano={16} /> {textoAsistencia(actual.evento)}
              </p>
            </div>
          </Pieza>
        </>
      )}
    </Entrada>
  );
}
