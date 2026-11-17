"use client";

import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useCallback, useEffect, useState } from "react";
import SubirFlyer, { BorradorFlyer } from "@/components/eventos/SubirFlyer";
import FotoEvento from "@/components/eventos/FotoEvento";
import { Entrada, Pieza } from "@/components/app/Movimiento";
import { ErrorApi } from "@/lib/api";
import {
  actualizarEvento,
  cancelarEvento,
  crearEvento,
  EventoCampus,
  listarLugaresEventos,
  listarMisEventos,
  LugarEvento,
} from "@/lib/eventos";
import {
  CamposFormularioEvento,
  fechaBoliviaParaFormulario,
  formDataEvento,
  FOTO_EVENTO_MAX_BYTES,
  presentacionEvento,
  validarFotoEvento,
  validarFormularioEvento,
} from "@/lib/reglasEventos";
import { formatearFechaHora } from "@/lib/reglasFlyer";
import estilos from "./eventos.module.css";

type Pestana = "crear" | "mios";
type Campos = CamposFormularioEvento & { descripcion: string };

const CAMPOS_VACIOS: Campos = {
  titulo: "",
  descripcion: "",
  inicio: "",
  fin: "",
  lugarId: "",
  lugarTexto: "",
  asistencia: "FIJA",
  visitaMin: "",
  visitaMax: "",
};

const ETIQUETAS_ESTADO: Record<EventoCampus["estado"], string> = {
  PENDIENTE: "En revisión",
  PUBLICADO: "Publicado",
  RECHAZADO: "Rechazado",
  CANCELADO: "Cancelado",
};

export default function EventosPage() {
  const reducir = useReducedMotion();
  const [pestana, setPestana] = useState<Pestana>("crear");
  const [campos, setCampos] = useState<Campos>(CAMPOS_VACIOS);
  const [lugares, setLugares] = useState<LugarEvento[]>([]);
  const [eventos, setEventos] = useState<EventoCampus[]>([]);
  const [foto, setFoto] = useState<File | null>(null);
  const [editando, setEditando] = useState<number | null>(null);
  const [cargando, setCargando] = useState(true);
  const [ahora, setAhora] = useState(0);
  const [guardando, setGuardando] = useState(false);
  const [mostrandoFlyer, setMostrandoFlyer] = useState(false);
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  const obtenerDatos = useCallback(() => Promise.all([listarLugaresEventos(), listarMisEventos()]), []);

  const cargar = useCallback(async () => {
    setCargando(true);
    setError("");
    try {
      const [opciones, propios] = await obtenerDatos();
      setLugares(opciones);
      setEventos(propios);
      setAhora(Date.now());
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudieron cargar tus eventos.");
    } finally {
      setCargando(false);
    }
  }, [obtenerDatos]);

  useEffect(() => {
    let activo = true;
    obtenerDatos()
      .then(([opciones, propios]) => {
        if (!activo) return;
        setLugares(opciones);
        setEventos(propios);
        setAhora(Date.now());
      })
      .catch((e: unknown) => {
        if (activo) setError(e instanceof Error ? e.message : "No se pudieron cargar tus eventos.");
      })
      .finally(() => {
        if (activo) setCargando(false);
      });
    return () => { activo = false; };
  }, [obtenerDatos]);

  function cambiar<K extends keyof Campos>(campo: K, valor: Campos[K]) {
    setCampos((actuales) => ({ ...actuales, [campo]: valor }));
    setError("");
  }

  function limpiarFormulario() {
    setCampos(CAMPOS_VACIOS);
    setFoto(null);
    setEditando(null);
  }

  async function guardar(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (guardando) return;
    const problema = validarFormularioEvento(campos);
    if (problema) {
      setError(problema);
      return;
    }
    if (foto) {
      const problemaFoto = validarFotoEvento(foto);
      if (problemaFoto) {
        setError(problemaFoto);
        return;
      }
    }

    setGuardando(true);
    setError("");
    setMensaje("");
    try {
      const nombreLugar = lugares.find((l) => String(l.id) === campos.lugarId)?.nombre;
      const datos = formDataEvento(campos, foto, nombreLugar);
      const resultado = editando === null
        ? await crearEvento(datos)
        : await actualizarEvento(editando, datos);
      setMensaje(
        resultado.estado === "PUBLICADO"
          ? editando === null ? "Tu evento se publicó correctamente." : "Los cambios quedaron guardados y el evento está publicado."
          : resultado.estado === "PENDIENTE"
            ? `Tu evento quedó en revisión. ${resultado.motivo ?? ""}`.trim()
            : `El evento se guardó con estado: ${ETIQUETAS_ESTADO[resultado.estado].toLowerCase()}.`,
      );
      if (resultado.estado === "PUBLICADO") limpiarFormulario();
      setPestana("mios");
      try {
        setEventos(await listarMisEventos());
      } catch (e) {
        setError(`El evento sí se guardó, pero no se pudo actualizar la lista: ${e instanceof Error ? e.message : "error inesperado"}`);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudo guardar el evento.");
    } finally {
      setGuardando(false);
    }
  }

  function editar(evento: EventoCampus) {
    setCampos({
      titulo: evento.titulo,
      descripcion: evento.descripcion ?? "",
      inicio: fechaBoliviaParaFormulario(evento.inicio),
      fin: fechaBoliviaParaFormulario(evento.fin),
      lugarId: evento.lugarId === null ? "" : String(evento.lugarId),
      lugarTexto: evento.lugarId === null ? evento.lugar : "",
      asistencia: evento.asistencia,
      visitaMin: evento.visitaMin === null ? "" : String(evento.visitaMin),
      visitaMax: evento.visitaMax === null ? "" : String(evento.visitaMax),
    });
    setFoto(null);
    setEditando(evento.id);
    setPestana("crear");
    setMensaje("");
    setError("");
    window.scrollTo({ top: 0, behavior: reducir ? "instant" : "smooth" });
  }

  async function cancelar(evento: EventoCampus) {
    if (!window.confirm(`¿Cancelar «${evento.titulo}»? Esta acción cambia el estado del evento a Cancelado.`)) return;
    setGuardando(true);
    setError("");
    setMensaje("");
    try {
      const actualizado = await cancelarEvento(evento.id);
      setEventos((actuales) => actuales.map((e) => e.id === actualizado.id ? actualizado : e));
      setMensaje("El evento quedó cancelado.");
    } catch (e) {
      setError(e instanceof ErrorApi ? e.message : e instanceof Error ? e.message : "No se pudo cancelar el evento.");
    } finally {
      setGuardando(false);
    }
  }

  function usarBorrador(borrador: BorradorFlyer) {
    const evento = borrador.evento;
    const coincidencia = lugares.find((l) => l.nombre.localeCompare(evento.lugarTexto ?? "", "es", { sensitivity: "base" }) === 0);
    setCampos({
      titulo: evento.titulo ?? "",
      descripcion: evento.descripcion ?? "",
      inicio: evento.inicio ?? "",
      fin: evento.fin ?? "",
      lugarId: coincidencia ? String(coincidencia.id) : "",
      lugarTexto: coincidencia ? "" : evento.lugarTexto ?? "",
      asistencia: evento.asistencia ?? "FIJA",
      visitaMin: evento.visitaMin === null ? "" : String(evento.visitaMin),
      visitaMax: evento.visitaMax === null ? "" : String(evento.visitaMax),
    });
    setFoto(borrador.archivo ?? imagenDelFlyer(borrador));
    setEditando(null);
    setMostrandoFlyer(false);
    setMensaje("Revisa los datos del borrador y completa cualquier campo que falte.");
    setError("");
  }

  const seleccion = lugares.find((l) => String(l.id) === campos.lugarId);

  return (
    <Entrada as="main" className={estilos.pagina}>
      <Pieza as="header" className={estilos.portada}>
        <span className={estilos.etiqueta}>Vida universitaria</span>
        <h1>Eventos del campus</h1>
        <p>Publica un evento y compártelo con la comunidad. Si algo necesita revisión, verás el motivo en «Mis eventos».</p>
      </Pieza>

      <div className={estilos.pestanas} role="tablist" aria-label="Secciones de eventos">
        <button type="button" role="tab" aria-selected={pestana === "crear"} className={pestana === "crear" ? estilos.pestanaActiva : estilos.pestana} onClick={() => setPestana("crear")}>
          {editando === null ? "Crear evento" : "Editar evento"}
        </button>
        <button type="button" role="tab" aria-selected={pestana === "mios"} className={pestana === "mios" ? estilos.pestanaActiva : estilos.pestana} onClick={() => setPestana("mios")}>
          Mis eventos <span>{eventos.length}</span>
        </button>
      </div>

      <AnimatePresence mode="wait" initial={false}>
        {error && (
          <motion.p key={error} className={estilos.error} role="alert" initial={reducir ? false : { opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }}>
            {error}
          </motion.p>
        )}
        {mensaje && (
          <motion.p key={mensaje} className={estilos.mensaje} role="status" initial={reducir ? false : { opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }}>
            {mensaje}
          </motion.p>
        )}
      </AnimatePresence>

      {pestana === "crear" ? (
        <Pieza as="section" className={estilos.tarjeta}>
          <div className={estilos.encabezadoFormulario}>
            <div>
              <h2>{editando === null ? "Cuéntanos qué va a pasar" : "Actualiza los datos"}</h2>
              <p>Los campos con * son obligatorios. Descripción y foto son opcionales.</p>
            </div>
            <button type="button" className={estilos.botonSecundario} onClick={() => setMostrandoFlyer(true)} disabled={guardando}>
              Completar desde un afiche
            </button>
          </div>

          <form className={estilos.formulario} onSubmit={(e) => void guardar(e)} noValidate>
            <label className={estilos.campo}>
              <span>Título <b>*</b></span>
              <input value={campos.titulo} maxLength={120} required onChange={(e) => cambiar("titulo", e.target.value)} placeholder="Ej.: Feria de ciencias" />
            </label>

            <label className={estilos.campo}>
              <span>Descripción <small>(opcional)</small></span>
              <textarea value={campos.descripcion} maxLength={4000} rows={4} onChange={(e) => cambiar("descripcion", e.target.value)} placeholder="Agrega detalles para quienes quieran participar." />
              <small className={estilos.contador}>{campos.descripcion.length}/4000</small>
            </label>

            <div className={estilos.dosColumnas}>
              <label className={estilos.campo}>
                <span>Empieza <b>*</b></span>
                <input type="datetime-local" value={campos.inicio} required onChange={(e) => cambiar("inicio", e.target.value)} />
              </label>
              <label className={estilos.campo}>
                <span>Termina <b>*</b></span>
                <input type="datetime-local" value={campos.fin} required onChange={(e) => cambiar("fin", e.target.value)} />
              </label>
            </div>
            <p className={estilos.ayuda}>Las fechas se interpretan en hora de Bolivia (UTC−4). Un horario fuera del campus quedará en revisión.</p>

            <label className={estilos.campo}>
              <span>Lugar <b>*</b></span>
              <select value={campos.lugarId} onChange={(e) => cambiar("lugarId", e.target.value)}>
                <option value="">Elige un lugar del catálogo o escribe uno</option>
                {lugares.map((lugar) => <option key={lugar.id} value={lugar.id}>{lugar.nombre}</option>)}
              </select>
            </label>
            {!seleccion && (
              <label className={estilos.campo}>
                <span>Nombre del lugar <b>*</b></span>
                <input value={campos.lugarTexto} maxLength={150} required onChange={(e) => cambiar("lugarTexto", e.target.value)} placeholder="Ej.: Patio junto al coliseo" />
                <small className={estilos.ayuda}>Los lugares que no estén en el catálogo se envían a revisión.</small>
              </label>
            )}

            <fieldset className={estilos.grupo}>
              <legend>Tipo de asistencia</legend>
              <label className={estilos.opcion}>
                <input type="radio" name="asistencia" checked={campos.asistencia === "FIJA"} onChange={() => cambiar("asistencia", "FIJA")} />
                <span><strong>Hay que llegar a la hora</strong><small>La asistencia es fija durante el evento.</small></span>
              </label>
              <label className={estilos.opcion}>
                <input type="radio" name="asistencia" checked={campos.asistencia === "LIBRE"} onChange={() => cambiar("asistencia", "LIBRE")} />
                <span><strong>Se puede pasar un rato</strong><small>Indica cuánto tiempo se recomienda quedarse.</small></span>
              </label>
            </fieldset>

            {campos.asistencia === "LIBRE" && (
              <div className={estilos.dosColumnas}>
                <label className={estilos.campo}>
                  <span>Visita mínima (min) <b>*</b></span>
                  <input type="number" min={1} max={825} step={1} value={campos.visitaMin} onChange={(e) => cambiar("visitaMin", e.target.value)} />
                </label>
                <label className={estilos.campo}>
                  <span>Visita máxima (min) <b>*</b></span>
                  <input type="number" min={1} max={825} step={1} value={campos.visitaMax} onChange={(e) => cambiar("visitaMax", e.target.value)} />
                </label>
              </div>
            )}

            <label className={estilos.campo}>
              <span>Foto <small>(opcional; JPG o PNG, hasta 2 MB)</small></span>
              <input type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" onChange={(e) => {
                const archivo = e.target.files?.[0] ?? null;
                const problema = archivo ? validarFotoEvento(archivo) : null;
                if (problema) {
                  setFoto(null);
                  setError(problema);
                  e.target.value = "";
                  return;
                }
                setError("");
                setFoto(archivo);
              }} />
              {foto && <small className={estilos.ayuda}>{foto.name} · {(foto.size / 1024).toFixed(0)} KB de {FOTO_EVENTO_MAX_BYTES / 1024} KB</small>}
            </label>

            <div className={estilos.accionesFormulario}>
              {editando !== null && <button type="button" className={estilos.botonSecundario} disabled={guardando} onClick={limpiarFormulario}>Salir de edición</button>}
              <button type="submit" className={estilos.botonPrimario} disabled={guardando || cargando}>
                {guardando ? "Guardando…" : editando === null ? "Publicar evento" : "Guardar cambios"}
              </button>
            </div>
          </form>
        </Pieza>
      ) : (
        <section className={estilos.lista} aria-busy={cargando}>
          <div className={estilos.tituloLista}>
            <div><h2>Mis eventos</h2><p>Consulta su estado y el motivo si alguno quedó pendiente o fue rechazado.</p></div>
            <button type="button" className={estilos.botonSecundario} onClick={() => void cargar()} disabled={cargando}>Actualizar</button>
          </div>
          {cargando ? <p className={estilos.vacio}>Cargando tus eventos…</p> : eventos.length === 0 ? (
            <div className={estilos.vacio}><strong>Aún no has creado eventos.</strong><span>Cuando publiques uno, aparecerá aquí.</span><button type="button" className={estilos.botonPrimario} onClick={() => setPestana("crear")}>Crear mi primer evento</button></div>
          ) : eventos.map((evento) => {
            const vista = presentacionEvento(evento.estado, evento.inicio, evento.fin, ahora);
            return (
            <article key={evento.id} className={estilos.evento}>
              {evento.fotoUrl && (
                <FotoEvento ruta={evento.fotoUrl} titulo={evento.titulo} />
              )}
              <div className={estilos.detalleEvento}>
                <div className={estilos.tituloEvento}>
                  <div><span className={`${estilos.estado} ${estilos[`estado${vista.clave}`]}`}>{vista.etiqueta}</span><h3>{evento.titulo}</h3></div>
                  <span className={estilos.horario}>{formatearFechaHora(evento.inicio)} – {formatearFechaHora(evento.fin)}</span>
                </div>
                <p className={estilos.lugar}>{evento.lugarId === null ? `Fuera del campus · ${evento.lugar}` : evento.lugar}</p>
                {evento.descripcion && <p className={estilos.descripcionEvento}>{evento.descripcion}</p>}
                {evento.asistencia === "FIJA" ? <p className={estilos.ayuda}>Hay que llegar a la hora</p> : <p className={estilos.ayuda}>Visita libre: {evento.visitaMin}–{evento.visitaMax} min</p>}
                {(evento.estado === "PENDIENTE" || evento.estado === "RECHAZADO") && evento.motivo && <p className={estilos.motivo}><strong>Motivo:</strong> {evento.motivo}</p>}
                {(vista.editable || vista.cancelable) && (
                  <div className={estilos.accionesEvento}>
                    {vista.editable && <button type="button" className={estilos.botonSecundario} onClick={() => editar(evento)} disabled={guardando}>Editar</button>}
                    {vista.cancelable && <button type="button" className={estilos.botonPeligro} onClick={() => void cancelar(evento)} disabled={guardando}>Cancelar evento</button>}
                  </div>
                )}
              </div>
            </article>
            );
          })}
        </section>
      )}

      {mostrandoFlyer && (
        <SubirFlyer onCerrar={() => setMostrandoFlyer(false)} onBorrador={usarBorrador} />
      )}
    </Entrada>
  );
}

function imagenDelFlyer(borrador: BorradorFlyer): File | null {
  if (!borrador.imagen) return null;
  const binario = atob(borrador.imagen.contenidoBase64);
  const bytes = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i);
  return new File([bytes], "afiche.jpg", { type: borrador.imagen.tipoMime });
}
