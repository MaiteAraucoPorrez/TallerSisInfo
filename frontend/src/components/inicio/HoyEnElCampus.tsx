"use client";

import Link from "next/link";
import { Fragment, useEffect, useState } from "react";
import { motion, useReducedMotion } from "motion/react";
import { IconoFlechaDerecha, IconoPin } from "@/components/app/Iconos";
import { SALIDA } from "@/components/app/Movimiento";
import { EventoPublico, listarEventosDelCampus } from "@/lib/eventos";
import { ahoraBoliviaParaFormulario } from "@/lib/reglasEventos";
import { cuandoEs, horaDe, lineaDeHoy } from "@/lib/reglasHoyCampus";
import estilos from "./HoyEnElCampus.module.css";

/**
 * «Hoy en el campus»: los eventos publicados de hoy en una línea de tiempo, con la marca «Ahora» que avanza sola.
 * A diferencia de «Eventos en tu puente», muestra todos los de hoy, quepan o no en tus horas libres.
 */
export default function HoyEnElCampus({ className = "" }: { className?: string }) {
  const reducir = useReducedMotion();
  const [eventos, setEventos] = useState<EventoPublico[] | null>(null);
  const [error, setError] = useState(false);
  const [ahora, setAhora] = useState(() => ahoraBoliviaParaFormulario());

  useEffect(() => {
    let activo = true;
    listarEventosDelCampus("hoy")
      .then((lista) => {
        if (activo) setEventos(lista);
      })
      .catch(() => {
        if (activo) setError(true);
      });
    return () => {
      activo = false;
    };
  }, []);

  // La marca «Ahora» y los «En 25 min» se ponen al día solos.
  useEffect(() => {
    const t = window.setInterval(() => setAhora(ahoraBoliviaParaFormulario()), 30_000);
    return () => window.clearInterval(t);
  }, []);

  if (error) return null;

  const linea = eventos ? lineaDeHoy(eventos, ahora, 3) : null;
  const marcaAhora = (
    <li className={estilos.ahora} aria-label={`Ahora, ${horaDe(ahora)}`}>
      <span className={estilos.ahoraPunto} aria-hidden="true" />
      <span className={estilos.ahoraTexto}>Ahora · {horaDe(ahora)}</span>
    </li>
  );

  return (
    <motion.section
      className={`${estilos.tarjeta} ${className}`}
      aria-labelledby="titulo-hoy-campus"
      initial={reducir ? false : { opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, ease: SALIDA }}
    >
      <div className={estilos.encabezado}>
        <h2 id="titulo-hoy-campus">Hoy en el campus</h2>
        <Link href="/eventos" className={estilos.verTodo}>
          Ver todos <IconoFlechaDerecha tamano={14} />
        </Link>
      </div>

      {!linea ? (
        <div className={estilos.cargando} role="status" aria-label="Cargando los eventos de hoy">
          <span />
          <span />
        </div>
      ) : linea.items.length === 0 ? (
        <div className={estilos.vacio}>
          <p>Hoy no hay más eventos publicados en el campus.</p>
          <Link href="/eventos">¿Organizas algo? Publícalo</Link>
        </div>
      ) : (
        <ol className={estilos.linea}>
          {linea.items.map(({ evento, estado }, i) => (
            <Fragment key={evento.id}>
              {i === linea.antesDeAhora && marcaAhora}
              <li className={estilos.evento} data-estado={estado}>
                <Link href={`/eventos/${evento.id}`} className={estilos.fila}>
                  <time className={estilos.hora} dateTime={evento.inicio}>
                    {horaDe(evento.inicio)}
                  </time>
                  <span className={estilos.punto} aria-hidden="true" />
                  <span className={estilos.cuerpo}>
                    <strong>{evento.titulo}</strong>
                    <small>
                      <IconoPin tamano={13} /> {evento.lugar}
                      {evento.fueraDelCampus ? " · Fuera del campus" : ""}
                    </small>
                    <span className={estilos.cuando}>{cuandoEs(evento, ahora)}</span>
                  </span>
                </Link>
              </li>
            </Fragment>
          ))}
          {linea.antesDeAhora === linea.items.length && marcaAhora}
        </ol>
      )}

      {linea && linea.ocultos > 0 && (
        <p className={estilos.nota}>
          Y {linea.ocultos} {linea.ocultos === 1 ? "evento más" : "eventos más"} hoy.
        </p>
      )}
    </motion.section>
  );
}
