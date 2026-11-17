"use client";

import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { IconoCamara, IconoDestello } from "@/components/app/Iconos";
import { RESORTE_TOQUE, SALIDA } from "@/components/app/Movimiento";
import { ErrorApi } from "@/lib/api";
import { BorradorEvento, ImagenFlyer, LecturaFlyer, leerFlyer } from "@/lib/eventos";
import {
  ETIQUETA_CAMPO,
  EstadoFlyer,
  FLYER_ACCEPT,
  FLYER_TEXTO_MAX,
  MENSAJE_NO_ENTENDIDO,
  camposFaltantes,
  esPdf,
  formatearFechaHora,
  imagenBorrosa,
  mensajeErrorFlyer,
  sePuedeUsarBorrador,
  textoAsistencia,
  textoVisita,
  validarArchivoFlyer,
  validarTextoFlyer,
} from "@/lib/reglasFlyer";
import estilos from "./SubirFlyer.module.css";

const MENSAJES = ["Leyendo el afiche…", "Buscando título, fecha y lugar…", "Revisando el horario del evento…", "Armando tu borrador…"];

/**
 * Lo que recibe `onBorrador` (HU24 parte 1). La parte 2 lo conecta al formulario de HU23, donde se edita.
 * - `evento`: campos leídos; vacíos (null) si no se pudieron leer. inicio/fin en hora de Bolivia «yyyy-MM-ddTHH:mm».
 * - `faltantes`: campos que hay que completar (de `titulo`, `inicio`, `fin`, `lugar`).
 * - `advertencias`: avisos del lector (imagen borrosa, datos dudosos, varios eventos).
 * - `imagen`: página 1 del PDF en JPEG (si el origen fue PDF y cabe en 2 MB); si no, null.
 * - `archivo`: el JPG/PNG original, para usarlo como imagen del evento; null si fue PDF o texto.
 */
export type BorradorFlyer = {
  consultaId: number;
  estado: EstadoFlyer;
  /** Explicación del servidor (p. ej. la imagen se ve borrosa); null si no hay. */
  mensaje: string | null;
  evento: BorradorEvento;
  faltantes: string[];
  advertencias: string[];
  imagen: ImagenFlyer | null;
  archivo: File | null;
};

type Paso = "elegir" | "leyendo" | "revisar";
type Pestana = "archivo" | "texto";

type Props = {
  onCerrar: () => void;
  onBorrador: (borrador: BorradorFlyer) => void;
  /** Se llama justo antes de cerrar cuando el estudiante elige «Llenar a mano» (no se llama a la IA). */
  onLlenarAMano?: () => void;
};

function Dato({ etiqueta, valor, falta, aviso }: { etiqueta: string; valor: string; falta: boolean; aviso?: boolean }) {
  return (
    <div className={estilos.dato}>
      <dt>{etiqueta}</dt>
      <dd>
        {falta ? <span className={estilos.falta}>falta</span> : valor}
        {aviso && !falta && <small> (sin leer)</small>}
      </dd>
    </div>
  );
}

export default function SubirFlyer({ onCerrar, onBorrador, onLlenarAMano }: Props) {
  const reducir = useReducedMotion();
  const [paso, setPaso] = useState<Paso>("elegir");
  const [pestana, setPestana] = useState<Pestana>("archivo");
  const [archivo, setArchivo] = useState<File | null>(null);
  const [vista, setVista] = useState<string | null>(null);
  const [texto, setTexto] = useState("");
  const [lectura, setLectura] = useState<LecturaFlyer | null>(null);
  const [textoLeido, setTextoLeido] = useState("");
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState(0);
  const [arrastrando, setArrastrando] = useState(false);
  const galeria = useRef<HTMLInputElement>(null);
  const camara = useRef<HTMLInputElement>(null);
  const urlVista = useRef<string | null>(null);
  const ocupado = paso === "leyendo";

  useEffect(
    () => () => {
      if (urlVista.current) URL.revokeObjectURL(urlVista.current);
    },
    [],
  );

  useEffect(() => {
    if (paso !== "leyendo") return;
    const t = setInterval(() => setMensaje((m) => (m + 1) % MENSAJES.length), 1600);
    return () => clearInterval(t);
  }, [paso]);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => e.key === "Escape" && !ocupado && onCerrar();
    window.addEventListener("keydown", alTeclear);
    return () => window.removeEventListener("keydown", alTeclear);
  }, [ocupado, onCerrar]);

  function soltarVista() {
    if (urlVista.current) URL.revokeObjectURL(urlVista.current);
    urlVista.current = null;
    setVista(null);
  }

  function elegir(f: File | undefined) {
    setError("");
    if (!f) return;
    const problema = validarArchivoFlyer(f);
    if (problema) return setError(problema);
    soltarVista();
    if (!esPdf(f)) {
      urlVista.current = URL.createObjectURL(f);
      setVista(urlVista.current);
    }
    setArchivo(f);
  }

  async function leer() {
    const entrada = pestana === "archivo" ? archivo : texto.trim();
    if (!entrada) return;
    const problema = typeof entrada === "string" ? validarTextoFlyer(entrada) : validarArchivoFlyer(entrada);
    if (problema) return setError(problema);
    setPaso("leyendo");
    setError("");
    setMensaje(0);
    try {
      const r = await leerFlyer(entrada);
      setLectura(r);
      setTextoLeido(typeof entrada === "string" ? entrada : "");
      setPaso("revisar");
    } catch (e) {
      // Ningún error de la lectura es sesión vencida (pedirApi ya maneja el 401): se explica y se puede volver a intentar.
      setError(e instanceof ErrorApi ? mensajeErrorFlyer(e.codigo, e.message) : e instanceof Error ? e.message : mensajeErrorFlyer(undefined, undefined));
      setPaso("elegir");
    }
  }

  function volverASubir() {
    setLectura(null);
    setError("");
    setPaso("elegir");
  }

  function llenarAMano() {
    onLlenarAMano?.();
    onCerrar();
  }

  function usar() {
    if (!lectura || !sePuedeUsarBorrador(lectura.estado)) return;
    onBorrador({
      consultaId: lectura.consultaId,
      estado: lectura.estado,
      mensaje: lectura.mensaje,
      evento: lectura.evento,
      faltantes: [...camposFaltantes(lectura.faltantes, lectura.evento)],
      advertencias: lectura.advertencias,
      imagen: lectura.imagen,
      archivo: archivo && !esPdf(archivo) && pestana === "archivo" ? archivo : null,
    });
  }

  const puedeLeer = pestana === "archivo" ? archivo !== null : texto.trim().length > 0;
  const subtitulo = paso === "revisar" ? "Revisa lo que leímos antes de usarlo." : "La IA lee el afiche y te arma un borrador del evento.";

  return (
    <motion.div
      className={estilos.fondo}
      onMouseDown={(e) => e.target === e.currentTarget && !ocupado && onCerrar()}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
    >
      <motion.section
        className={estilos.modal}
        role="dialog"
        aria-modal="true"
        aria-labelledby="flyer-titulo"
        initial={reducir ? false : { opacity: 0, scale: 0.95, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={reducir ? { opacity: 0 } : { opacity: 0, scale: 0.97, y: 12 }}
        transition={{ type: "spring", stiffness: 380, damping: 32 }}
      >
        <header className={estilos.cabecera}>
          <span className={estilos.icono} aria-hidden="true">
            <IconoCamara tamano={22} />
          </span>
          <div>
            <h2 id="flyer-titulo">Evento desde un afiche</h2>
            <p>{subtitulo}</p>
          </div>
          <button type="button" className={estilos.cerrar} onClick={onCerrar} disabled={ocupado} aria-label="Cerrar">
            ×
          </button>
        </header>

        {error && (
          <motion.p
            key={error}
            className={estilos.error}
            role="alert"
            initial={reducir ? false : { x: 0 }}
            animate={{ x: [0, -8, 8, -5, 5, 0] }}
            transition={{ duration: 0.42 }}
          >
            {error}
          </motion.p>
        )}

        <AnimatePresence mode="wait" initial={false}>
          {paso !== "revisar" && (
            <motion.div key="elegir" className={estilos.cuerpo} exit={{ opacity: 0, y: -8 }} transition={{ duration: 0.2, ease: SALIDA }}>
              <div className={estilos.pestanas} role="tablist" aria-label="Cómo enviar el evento">
                {(["archivo", "texto"] as const).map((p) => (
                  <button
                    key={p}
                    type="button"
                    role="tab"
                    aria-selected={pestana === p}
                    className={pestana === p ? `${estilos.pestana} ${estilos.pestanaActiva}` : estilos.pestana}
                    disabled={ocupado}
                    onClick={() => {
                      setPestana(p);
                      setError("");
                    }}
                  >
                    {p === "archivo" ? "Subir archivo" : "Pegar texto"}
                  </button>
                ))}
              </div>

              {pestana === "archivo" ? (
                <div
                  className={`${estilos.zona} ${arrastrando ? estilos.zonaActiva : ""}`}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setArrastrando(true);
                  }}
                  onDragLeave={() => setArrastrando(false)}
                  onDrop={(e) => {
                    e.preventDefault();
                    setArrastrando(false);
                    if (!ocupado) elegir(e.dataTransfer.files[0]);
                  }}
                >
                  {vista ? (
                    <div className={estilos.vista}>
                      {/* eslint-disable-next-line @next/next/no-img-element -- vista previa local (blob:), no pasa por el optimizador */}
                      <img src={vista} alt="Vista previa del afiche" />
                      {paso === "leyendo" && (
                        <>
                          <span className={estilos.escaneo} aria-hidden="true" />
                          <span className={estilos.velo} aria-hidden="true" />
                        </>
                      )}
                    </div>
                  ) : archivo ? (
                    <div className={estilos.vacio}>
                      <span className={estilos.vacioIcono} aria-hidden="true">
                        PDF
                      </span>
                      <strong>{archivo.name}</strong>
                      <span>{(archivo.size / (1024 * 1024)).toFixed(1)} MB</span>
                    </div>
                  ) : (
                    <div className={estilos.vacio}>
                      <span className={estilos.vacioIcono} aria-hidden="true">
                        <IconoCamara tamano={28} />
                      </span>
                      <strong>Toma, elige o arrastra el afiche del evento</strong>
                      <span>JPG, PNG o PDF de hasta 2 páginas, máximo 8 MB.</span>
                    </div>
                  )}
                </div>
              ) : (
                <label className={estilos.campoTexto}>
                  <span>Pega el texto del evento</span>
                  <textarea
                    value={texto}
                    maxLength={FLYER_TEXTO_MAX}
                    rows={8}
                    disabled={ocupado}
                    placeholder="Ej.: Feria de Ciencias, 20 de noviembre de 8:00 a 13:00 en el patio central."
                    onChange={(e) => setTexto(e.target.value)}
                  />
                  <small>
                    {texto.length}/{FLYER_TEXTO_MAX}
                  </small>
                </label>
              )}

              {paso === "leyendo" ? (
                <div className={estilos.leyendo} role="status">
                  <AnimatePresence mode="wait">
                    <motion.span
                      key={mensaje}
                      initial={reducir ? false : { opacity: 0, y: 8 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -8 }}
                      transition={{ duration: 0.25 }}
                    >
                      <IconoDestello tamano={16} /> {MENSAJES[mensaje]}
                    </motion.span>
                  </AnimatePresence>
                </div>
              ) : (
                <div className={estilos.acciones}>
                  <button type="button" className={estilos.manual} onClick={llenarAMano}>
                    Llenar a mano
                  </button>
                  {pestana === "archivo" && (
                    <>
                      <input ref={camara} type="file" accept="image/jpeg,image/png" capture="environment" hidden onChange={(e) => elegir(e.target.files?.[0])} />
                      <input ref={galeria} type="file" accept={FLYER_ACCEPT} hidden onChange={(e) => elegir(e.target.files?.[0])} />
                      <button type="button" className={estilos.secundario} onClick={() => camara.current?.click()}>
                        Tomar foto
                      </button>
                      <button type="button" className={estilos.secundario} onClick={() => galeria.current?.click()}>
                        {archivo ? "Elegir otro" : "Elegir archivo"}
                      </button>
                    </>
                  )}
                  <motion.button
                    type="button"
                    className={estilos.primario}
                    disabled={!puedeLeer}
                    onClick={() => void leer()}
                    whileTap={{ scale: 0.97 }}
                    transition={RESORTE_TOQUE}
                  >
                    <IconoDestello tamano={18} /> Leer evento
                  </motion.button>
                </div>
              )}
            </motion.div>
          )}

          {paso === "revisar" && lectura && (
            <motion.div
              key="revisar"
              className={estilos.cuerpo}
              initial={reducir ? false : { opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -8 }}
              transition={{ duration: 0.25, ease: SALIDA }}
            >
              <Revision lectura={lectura} vista={vista} texto={textoLeido} />
              <div className={estilos.acciones}>
                <button type="button" className={estilos.manual} onClick={llenarAMano}>
                  Llenar a mano
                </button>
                <button type="button" className={estilos.secundario} onClick={volverASubir}>
                  Volver a subir
                </button>
                {sePuedeUsarBorrador(lectura.estado) && (
                  <motion.button type="button" className={estilos.primario} onClick={usar} whileTap={{ scale: 0.97 }} transition={RESORTE_TOQUE}>
                    Usar estos datos
                  </motion.button>
                )}
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.section>
    </motion.div>
  );
}

function Revision({ lectura, vista, texto }: { lectura: LecturaFlyer; vista: string | null; texto: string }) {
  const { estado, evento, advertencias, imagen } = lectura;

  if (estado === "NO_ENTENDIDO") {
    return (
      <div className={estilos.vacio} role="status">
        <strong>No encontramos un evento</strong>
        <span>{lectura.mensaje || MENSAJE_NO_ENTENDIDO}</span>
      </div>
    );
  }

  const faltan = camposFaltantes(lectura.faltantes, evento);
  const borrosa = imagenBorrosa(estado, lectura.mensaje);
  const original = vista ?? (imagen ? `data:${imagen.tipoMime};base64,${imagen.contenidoBase64}` : null);

  return (
    <>
      {borrosa && (
        <p className={estilos.aviso} role="status">
          <strong>La imagen se ve borrosa.</strong> Revisa los datos o súbela con más calidad.
        </p>
      )}
      {!borrosa && estado === "INCOMPLETO" && lectura.mensaje && (
        <p className={estilos.aviso} role="status">
          {lectura.mensaje}
        </p>
      )}
      {advertencias.length > 0 && (
        <div className={estilos.aviso} role="status">
          <strong>Ten en cuenta:</strong>
          <ul>
            {advertencias.map((a) => (
              <li key={a}>{a}</li>
            ))}
          </ul>
        </div>
      )}

      <div className={estilos.comparar}>
        <div className={estilos.original}>
          {original ? (
            // eslint-disable-next-line @next/next/no-img-element -- blob: o data: local, no pasa por el optimizador
            <img src={original} alt="Afiche original para comparar" />
          ) : texto ? (
            <p className={estilos.textoOriginal}>{texto}</p>
          ) : (
            <p className={estilos.textoOriginal}>La vista previa del PDF no está disponible, pero se leyó igual.</p>
          )}
        </div>
        <dl className={estilos.resumenDatos}>
          <Dato etiqueta={ETIQUETA_CAMPO.titulo} valor={evento.titulo ?? ""} falta={faltan.has("titulo")} />
          <Dato etiqueta={ETIQUETA_CAMPO.descripcion} valor={evento.descripcion ?? ""} falta={false} aviso={!evento.descripcion} />
          <Dato etiqueta={ETIQUETA_CAMPO.inicio} valor={formatearFechaHora(evento.inicio)} falta={faltan.has("inicio")} />
          <Dato etiqueta={ETIQUETA_CAMPO.fin} valor={formatearFechaHora(evento.fin)} falta={faltan.has("fin")} />
          <Dato etiqueta={ETIQUETA_CAMPO.lugar} valor={evento.lugarTexto ?? ""} falta={faltan.has("lugar")} />
          <Dato etiqueta={ETIQUETA_CAMPO.asistencia} valor={textoAsistencia(evento.asistencia)} falta={!evento.asistencia} />
          {evento.asistencia === "LIBRE" && <Dato etiqueta="Visita sugerida" valor={textoVisita(evento)} falta={!textoVisita(evento)} />}
        </dl>
      </div>
      {faltan.size > 0 && <p className={estilos.ayuda}>Lo marcado como «falta» lo completas en el formulario.</p>}
    </>
  );
}
