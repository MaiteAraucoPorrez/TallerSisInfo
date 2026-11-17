"use client";

import { useMemo } from "react";
import { Selector, SelectorHora } from "@/components/app/Selectores";
import { ahoraBoliviaParaFormulario, opcionesDeFecha, partirFechaHora, unirFechaHora } from "@/lib/reglasEventos";
import estilos from "./CampoFechaHora.module.css";

type Props = {
  /** «AAAA-MM-DDTHH:mm», o a medias («AAAA-MM-DDT» / «THH:mm») mientras falta una parte. */
  valor: string;
  onCambio: (valor: string) => void;
  /** «inicio» o «fin»: se usa en las etiquetas para lectores de pantalla. */
  nombre: string;
  invalido?: boolean;
};

/** Fecha y hora con los selectores de la app (en vez del control nativo del navegador). Hora de Bolivia. */
export default function CampoFechaHora({ valor, onCambio, nombre, invalido }: Props) {
  const { fecha, hora } = partirFechaHora(valor);
  const hoy = ahoraBoliviaParaFormulario().slice(0, 10);
  const opciones = useMemo(() => opcionesDeFecha(hoy, 120, fecha), [hoy, fecha]);

  return (
    <div className={estilos.fila}>
      <Selector
        valor={fecha}
        opciones={opciones}
        onCambio={(nueva) => onCambio(unirFechaHora(nueva, hora))}
        etiqueta={`Fecha de ${nombre}`}
        placeholder="Elige el día"
        invalido={invalido}
      />
      <SelectorHora
        valor={hora}
        onCambio={(nueva) => onCambio(unirFechaHora(fecha, nueva))}
        min="00:00"
        max="23:55"
        etiqueta={`Hora de ${nombre}`}
        invalido={invalido}
        clase={estilos.hora}
      />
    </div>
  );
}
