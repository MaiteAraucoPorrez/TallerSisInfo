"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { motion, useReducedMotion } from "motion/react";
import { IconoPin } from "@/components/app/Iconos";
import { Selector, SelectorHora } from "@/components/app/Selectores";
import {
  CampusAdmin,
  CategoriaAdmin,
  EtiquetaAdmin,
  GuardarLugar,
  cambiarEstadoLugar,
  crearLugarAdmin,
  editarLugarAdmin,
  listarCampusAdmin,
  listarCategoriasAdmin,
  listarEtiquetasAdmin,
  obtenerLugarAdmin,
  quitarFotoLugarAdmin,
  subirFotoLugarAdmin,
} from "@/lib/admin";
import { errorEtiquetas, quedaActivoYRecomendable } from "@/lib/reglasEtiquetas";
import CampoFoto, { CambioFoto } from "./CampoFoto";
import SelectorEtiquetas from "./SelectorEtiquetas";
import estilos from "./FormularioLugar.module.css";

const LARGO_MAXIMO_DESCRIPCION = 500;
const sinSuscripcion = () => () => {};

type Props = {
  /** Sin id: lugar nuevo. Con id: se cargan sus datos para editarlo. */
  lugarId: number | null;
  /** HU30: se abrió para activar un lugar que no tenía tipo de actividad: las etiquetas son obligatorias y al guardar queda ACTIVO. */
  activarAlGuardar?: boolean;
  onCerrar: () => void;
  /** Se guardó: el panel recarga el catálogo y muestra el aviso. */
  onGuardado: (mensaje: string) => void;
};

type Campos = {
  nombre: string;
  descripcion: string;
  categoriaId: string;
  latitud: string;
  longitud: string;
  duracion: string;
  conHorario: boolean;
  apertura: string;
  cierre: string;
  minAPie: string;
};

const VACIO: Campos = {
  nombre: "",
  descripcion: "",
  categoriaId: "",
  latitud: "",
  longitud: "",
  duracion: "30",
  conHorario: false,
  apertura: "08:00",
  cierre: "20:00",
  minAPie: "",
};

const numero = (texto: string) => (texto.trim() === "" ? null : Number(texto.replace(",", ".")));

/** Crear o editar un lugar del catálogo. Las reglas las valida el servidor; aquí solo se arma el pedido y se muestra su mensaje. */
export default function FormularioLugar({ lugarId, activarAlGuardar = false, onCerrar, onGuardado }: Props) {
  const reducir = useReducedMotion();
  const enCliente = useSyncExternalStore(sinSuscripcion, () => true, () => false);
  const [campos, setCampos] = useState<Campos | null>(null);
  const [categorias, setCategorias] = useState<CategoriaAdmin[]>([]);
  const [campus, setCampus] = useState<CampusAdmin[]>([]);
  const [error, setError] = useState("");
  const [guardando, setGuardando] = useState(false);
  const [fotoActual, setFotoActual] = useState<string | null>(null);
  const [cambioFoto, setCambioFoto] = useState<CambioFoto>({ tipo: "igual" });
  // HU30: etiquetas de tipo de actividad del lugar y si tiene que tener al menos una.
  const [catalogoEtiquetas, setCatalogoEtiquetas] = useState<EtiquetaAdmin[]>([]);
  const [etiquetas, setEtiquetas] = useState<number[]>([]);
  const [requiereEtiqueta, setRequiereEtiqueta] = useState(true);
  const [errorEtiqueta, setErrorEtiqueta] = useState("");
  const refEtiquetas = useRef<HTMLDivElement>(null);
  const editando = lugarId !== null;

  useEffect(() => {
    let activo = true;
    Promise.all([
      listarCategoriasAdmin(),
      listarCampusAdmin(),
      listarEtiquetasAdmin(),
      editando ? obtenerLugarAdmin(lugarId) : Promise.resolve(null),
    ])
      .then(([cats, camps, etiqs, lugar]) => {
        if (!activo) return;
        setCategorias(cats);
        setCampus(camps);
        setCatalogoEtiquetas(etiqs);
        setRequiereEtiqueta(activarAlGuardar || quedaActivoYRecomendable(lugar));
        if (lugar) {
          setEtiquetas((lugar.etiquetas ?? []).map((e) => e.id));
          setFotoActual(lugar.fotoUrl ?? null);
          setCampos({
            nombre: lugar.nombre,
            descripcion: lugar.descripcion ?? "",
            categoriaId: String(lugar.categoriaId),
            latitud: String(lugar.latitud),
            longitud: String(lugar.longitud),
            duracion: String(lugar.duracionEstimadaMin),
            conHorario: lugar.horaApertura !== null,
            apertura: lugar.horaApertura ?? "08:00",
            cierre: lugar.horaCierre ?? "20:00",
            minAPie: lugar.minAPieDesdeBloqueA === null ? "" : String(lugar.minAPieDesdeBloqueA),
          });
        } else {
          // Un lugar nuevo arranca en el centro del campus: el admin solo ajusta las coordenadas.
          const centro = camps[0];
          setCampos({ ...VACIO, latitud: centro ? String(centro.latitud) : "", longitud: centro ? String(centro.longitud) : "" });
        }
      })
      .catch((e: Error) => activo && setError(e.message));
    return () => {
      activo = false;
    };
  }, [editando, lugarId, activarAlGuardar]);

  // Escape cierra (si no se está guardando).
  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => e.key === "Escape" && !guardando && onCerrar();
    window.addEventListener("keydown", alTeclear);
    return () => window.removeEventListener("keydown", alTeclear);
  }, [guardando, onCerrar]);

  const cambiar = <K extends keyof Campos>(campo: K, valor: Campos[K]) => {
    setCampos((c) => (c ? { ...c, [campo]: valor } : c));
    if (error) setError("");
  };

  async function guardar(e: React.FormEvent) {
    e.preventDefault();
    if (!campos || guardando) return;
    if (!campos.nombre.trim()) return setError("Escribe el nombre del lugar.");
    if (!campos.categoriaId) return setError("Elige una categoría.");
    const faltaEtiqueta = errorEtiquetas(etiquetas, requiereEtiqueta);
    if (faltaEtiqueta) {
      setErrorEtiqueta(faltaEtiqueta);
      // El error se ve donde se corrige: el modal puede estar desplazado hasta abajo.
      refEtiquetas.current?.scrollIntoView({ behavior: reducir ? "auto" : "smooth", block: "center" });
      refEtiquetas.current?.querySelector("button")?.focus({ preventScroll: true });
      return;
    }

    const lugar: GuardarLugar = {
      nombre: campos.nombre.trim(),
      descripcion: campos.descripcion.trim() || null,
      categoriaId: Number(campos.categoriaId),
      latitud: numero(campos.latitud) ?? Number.NaN,
      longitud: numero(campos.longitud) ?? Number.NaN,
      duracionEstimadaMin: numero(campos.duracion) ?? 0,
      horaApertura: campos.conHorario ? campos.apertura : null,
      horaCierre: campos.conHorario ? campos.cierre : null,
      minAPieDesdeBloqueA: numero(campos.minAPie),
      etiquetaIds: etiquetas,
    };
    if (Number.isNaN(lugar.latitud) || Number.isNaN(lugar.longitud)) return setError("Indica la latitud y la longitud del lugar.");

    setGuardando(true);
    try {
      let id = lugarId;
      if (editando) await editarLugarAdmin(lugarId, lugar);
      else id = (await crearLugarAdmin(lugar)).id;
      if (editando && activarAlGuardar) await cambiarEstadoLugar(lugarId, "ACTIVO");
      const listo = editando
        ? activarAlGuardar
          ? `${lugar.nombre} ya está activo y se puede recomendar.`
          : `${lugar.nombre}: cambios guardados.`
        : `${lugar.nombre} ya está en el catálogo.`;

      // La foto va después: al crear, recién ahora existe el id. Si falla, el lugar ya quedó guardado y se avisa.
      try {
        if (id !== null && cambioFoto.tipo === "nueva") await subirFotoLugarAdmin(id, cambioFoto.foto);
        if (id !== null && cambioFoto.tipo === "quitar") await quitarFotoLugarAdmin(id);
      } catch (errFoto) {
        onGuardado(`${listo} Pero la foto no se guardó: ${(errFoto as Error).message} Vuelve a intentarlo editando el lugar.`);
        return;
      }
      onGuardado(listo);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setGuardando(false);
    }
  }

  const centro = campus[0];
  if (!enCliente) return null;

  return createPortal(
    <motion.div
      className="hz-overlay"
      onMouseDown={(e) => e.target === e.currentTarget && !guardando && onCerrar()}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
    >
      <motion.form
        className={`hz-modal ${estilos.modal}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-lugar"
        onSubmit={guardar}
        noValidate
        initial={reducir ? false : { opacity: 0, scale: 0.95, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={reducir ? { opacity: 0 } : { opacity: 0, scale: 0.97, y: 12 }}
        transition={{ type: "spring", stiffness: 380, damping: 32 }}
      >
        <div className="hz-modal-cabecera">
          <span className="hz-modal-icono" aria-hidden="true">
            <IconoPin tamano={22} />
          </span>
          <h2 id="titulo-lugar">{activarAlGuardar ? "Elige su tipo de actividad para activarlo" : editando ? "Editar lugar" : "Nuevo lugar"}</h2>
        </div>

        {error && (
          <motion.p key={error} className={estilos.error} role="alert" animate={reducir ? undefined : { x: [0, -8, 8, -5, 5, 0] }} transition={{ duration: 0.4 }}>
            {error}
          </motion.p>
        )}

        {!campos ? (
          <p className="hz-nota" aria-busy="true">Cargando…</p>
        ) : (
          <>
            <label className="hz-campo">
              Nombre
              <input value={campos.nombre} maxLength={100} onChange={(e) => cambiar("nombre", e.target.value)} placeholder="Ej: Sala de Estudio Norte" autoFocus />
            </label>

            <div className="hz-campo">
              Categoría
              <Selector
                valor={campos.categoriaId}
                opciones={categorias.map((c) => ({ valor: String(c.id), etiqueta: c.nombre }))}
                onCambio={(v) => cambiar("categoriaId", v)}
                etiqueta="Categoría"
              />
            </div>

            <div ref={refEtiquetas}>
            <SelectorEtiquetas
              catalogo={catalogoEtiquetas}
              seleccion={etiquetas}
              requerido={requiereEtiqueta}
              error={errorEtiqueta}
              deshabilitado={guardando}
              onCambio={(s) => {
                setEtiquetas(s);
                if (errorEtiqueta) setErrorEtiqueta("");
                if (error) setError("");
              }}
              onNueva={(nueva) => {
                setCatalogoEtiquetas((c) => [...c, nueva].sort((a, b) => a.nombre.localeCompare(b.nombre, "es")));
                setEtiquetas((s) => [...s, nueva.id].sort((a, b) => a - b));
                if (errorEtiqueta) setErrorEtiqueta("");
              }}
            />
            </div>

            <label className="hz-campo">
              <span>
                Descripción <em>(opcional)</em>
              </span>
              <textarea
                className={estilos.texto}
                rows={3}
                value={campos.descripcion}
                maxLength={LARGO_MAXIMO_DESCRIPCION}
                onChange={(e) => cambiar("descripcion", e.target.value)}
                placeholder="Qué hay y para qué sirve"
              />
              <small className={estilos.contador}>
                {campos.descripcion.length}/{LARGO_MAXIMO_DESCRIPCION}
              </small>
            </label>

            <CampoFoto
              fotoActual={fotoActual}
              categoria={categorias.find((c) => String(c.id) === campos.categoriaId)?.nombre ?? "Descanso"}
              cambio={cambioFoto}
              onCambio={(c) => {
                setCambioFoto(c);
                if (error) setError("");
              }}
              deshabilitado={guardando}
            />

            <fieldset className={estilos.grupo}>
              <legend>Ubicación</legend>
              <div className={estilos.dos}>
                <label className="hz-campo">
                  Latitud
                  <input inputMode="decimal" value={campos.latitud} onChange={(e) => cambiar("latitud", e.target.value)} />
                </label>
                <label className="hz-campo">
                  Longitud
                  <input inputMode="decimal" value={campos.longitud} onChange={(e) => cambiar("longitud", e.target.value)} />
                </label>
              </div>
              {centro && (
                <p className={estilos.ayuda}>
                  Tiene que estar a {centro.radioMaxM} m o menos del centro de {centro.nombre}.{" "}
                  <button
                    type="button"
                    className={estilos.enlace}
                    onClick={() => setCampos((c) => (c ? { ...c, latitud: String(centro.latitud), longitud: String(centro.longitud) } : c))}
                  >
                    Usar el centro del campus
                  </button>
                </p>
              )}
            </fieldset>

            <div className={estilos.dos}>
              <label className="hz-campo">
                Duración estimada (min)
                <input inputMode="numeric" value={campos.duracion} onChange={(e) => cambiar("duracion", e.target.value)} />
              </label>
              <label className="hz-campo">
                <span>
                  A pie desde el bloque A <em>(min)</em>
                </span>
                <input inputMode="numeric" value={campos.minAPie} onChange={(e) => cambiar("minAPie", e.target.value)} placeholder="Opcional" />
              </label>
            </div>

            <label className={estilos.casillaFila}>
              <input type="checkbox" checked={campos.conHorario} onChange={(e) => cambiar("conHorario", e.target.checked)} />
              <span className={estilos.casilla} aria-hidden="true" />
              Tiene horario de atención
            </label>
            {campos.conHorario && (
              <div className={estilos.dos}>
                <div className="hz-campo">
                  Abre
                  <SelectorHora valor={campos.apertura} onCambio={(v) => cambiar("apertura", v)} min="05:00" max="23:00" etiqueta="Abre" />
                </div>
                <div className="hz-campo">
                  Cierra
                  <SelectorHora valor={campos.cierre} onCambio={(v) => cambiar("cierre", v)} min="05:00" max="23:30" etiqueta="Cierra" />
                </div>
              </div>
            )}
          </>
        )}

        <div className="hz-acciones">
          <span className="hz-espacio" />
          <button type="button" className="hz-btn" disabled={guardando} onClick={onCerrar}>
            Cancelar
          </button>
          <button type="submit" className="hz-btn hz-btn-primary" disabled={guardando || !campos}>
            {guardando ? "Guardando…" : activarAlGuardar ? "Guardar y activar" : editando ? "Guardar cambios" : "Crear lugar"}
          </button>
        </div>
      </motion.form>
    </motion.div>,
    document.body,
  );
}
