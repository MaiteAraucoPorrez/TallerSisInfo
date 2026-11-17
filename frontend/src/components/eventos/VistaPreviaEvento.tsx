"use client";

import { IconoCalendario, IconoPin, IconoReloj } from "@/components/app/Iconos";
import { ahoraBoliviaParaFormulario, rangoLegible } from "@/lib/reglasEventos";
import FotoEvento from "./FotoEvento";
import VistaPreviaFoto from "./VistaPreviaFoto";
import estilos from "./VistaPreviaEvento.module.css";

type Props = {
  titulo: string;
  descripcion: string;
  inicio: string;
  fin: string;
  /** Nombre del lugar elegido del catálogo o el escrito a mano. */
  lugar: string;
  /** Por qué pasaría por revisión (`motivosDeRevision`); vacío = se publica al instante. */
  revision: string[];
  asistencia: "FIJA" | "LIBRE" | "";
  visitaMin: string;
  visitaMax: string;
  fotoNueva: File | null;
  fotoActual: string | null;
};

/** Cómo verá la comunidad el evento mientras se llena el formulario (a la derecha en pantallas anchas). */
export default function VistaPreviaEvento(p: Props) {
  const hoy = ahoraBoliviaParaFormulario().slice(0, 10);
  const cuando = rangoLegible(p.inicio, p.fin, hoy);
  const nombre = p.titulo.trim() || "Tu evento";
  const asistencia =
    p.asistencia === "FIJA" ? "Hay que llegar a la hora"
      : p.asistencia === "LIBRE" ? (p.visitaMin && p.visitaMax ? `Puedes pasar de ${p.visitaMin} a ${p.visitaMax} min` : "Puedes pasar un rato")
        : "";

  return (
    <div className={estilos.vista}>
      <p className={estilos.titulo}>Así lo verá la comunidad</p>
      <article className={estilos.tarjeta} aria-label={`Vista previa de ${nombre}`}>
        <div className={estilos.foto}>
          {p.fotoNueva ? <VistaPreviaFoto archivo={p.fotoNueva} titulo={nombre} />
            : p.fotoActual ? <FotoEvento ruta={p.fotoActual} titulo={nombre} />
              : (
                <div className={estilos.sinFoto} aria-hidden="true">
                  <IconoCalendario tamano={30} />
                  <span>Sin foto</span>
                </div>
              )}
        </div>
        <div className={estilos.cuerpo}>
          <span className={p.revision.length > 0 ? estilos.chipRevision : estilos.chip}>
            {p.revision.length > 0 ? "Pasará por revisión" : "Se publica al instante"}
          </span>
          <h3 className={p.titulo.trim() ? undefined : estilos.tenue}>{nombre}</h3>
          <ul className={estilos.datos}>
            <li className={cuando ? undefined : estilos.tenue}><IconoReloj tamano={16} /> {cuando || "Elige el día y la hora"}</li>
            <li className={p.lugar ? undefined : estilos.tenue}><IconoPin tamano={16} /> {p.lugar || "Elige el lugar"}</li>
          </ul>
          {p.descripcion.trim() && <p className={estilos.descripcion}>{p.descripcion.trim()}</p>}
          {asistencia && <p className={estilos.asistencia}>{asistencia}</p>}
        </div>
      </article>
      {p.revision.length > 0 ? (
        <div className={estilos.nota} role="status">
          <strong>Un administrador lo revisa antes de publicarlo:</strong>
          <ul>{p.revision.map((m) => <li key={m}>{m}</li>)}</ul>
        </div>
      ) : (
        <p className={estilos.nota}>Con un lugar del catálogo y entre las 07:15 y las 21:00, se publica apenas lo envíes.</p>
      )}
    </div>
  );
}
