"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { createPortal } from "react-dom";
import { useRouter } from "next/navigation";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import BotonGoogle, { GOOGLE_CLIENT_ID } from "@/components/auth/BotonGoogle";
import { ErrorApi } from "@/lib/api";
import { Perfil, eliminarCuenta } from "@/lib/perfil";
import {
  TEXTO_ADVERTENCIA,
  TEXTO_CONFIRMACION,
  eliminarYCerrarSesion,
  formasDeReautenticar,
  mensajeEliminarCuenta,
  puedeEliminarConContrasena,
  puedeEliminarConGoogle,
  puedeVerEliminarCuenta,
} from "@/lib/reglasEliminarCuenta";
import { cerrarSesion, guardarAvisoLogin } from "@/services/authService";
import estilos from "./EliminarCuenta.module.css";

const sinSuscripcion = () => () => {};

/** HU29: zona «Eliminar mi cuenta» al final de Seguridad, con su confirmación y reautenticación. No se muestra al administrador. */
export default function EliminarCuenta({ perfil }: { perfil: Perfil }) {
  const [abierto, setAbierto] = useState(false);
  if (!puedeVerEliminarCuenta(perfil.rol)) return null;

  return (
    <section className={estilos.zona} aria-labelledby="zona-eliminar-cuenta">
      <h3 id="zona-eliminar-cuenta">Eliminar mi cuenta</h3>
      <p className={estilos.ayuda}>
        Borra tu cuenta y tus datos de la plataforma. No se puede deshacer.
      </p>
      <button type="button" className="hz-btn hz-btn-peligro-fuerte" onClick={() => setAbierto(true)}>
        Eliminar mi cuenta
      </button>
      <AnimatePresence>
        {abierto && <DialogoEliminar perfil={perfil} onCerrar={() => setAbierto(false)} />}
      </AnimatePresence>
    </section>
  );
}

function DialogoEliminar({ perfil, onCerrar }: { perfil: Perfil; onCerrar: () => void }) {
  const router = useRouter();
  const reducir = useReducedMotion();
  const enCliente = useSyncExternalStore(sinSuscripcion, () => true, () => false);
  const [escrito, setEscrito] = useState("");
  const [contrasena, setContrasena] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState("");
  const primerCampo = useRef<HTMLInputElement>(null);
  const formas = formasDeReautenticar(perfil);
  const googleDisponible = formas.google && GOOGLE_CLIENT_ID !== "";

  useEffect(() => {
    primerCampo.current?.focus();
  }, []);

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => e.key === "Escape" && !enviando && onCerrar();
    window.addEventListener("keydown", alTeclear);
    return () => window.removeEventListener("keydown", alTeclear);
  }, [enviando, onCerrar]);

  async function enviar(credencial: { contrasena: string } | { credencialGoogle: string }) {
    setEnviando(true);
    setError("");
    // 204: la cuenta ya no existe. Se cierra la sesión local y el login muestra el aviso una sola vez.
    const resultado = await eliminarYCerrarSesion(() => eliminarCuenta(credencial), guardarAvisoLogin, cerrarSesion);
    if (resultado.ok) {
      router.replace("/login");
      return;
    }
    // Un rechazo (400/429) no cierra la sesión ni se trata como sesión vencida: solo se explica y se puede reintentar.
    const e = resultado.error;
    setError(e instanceof ErrorApi ? mensajeEliminarCuenta(e.codigo, e.message) : e instanceof Error ? e.message : mensajeEliminarCuenta(undefined, undefined));
    setEnviando(false);
  }

  function conContrasena(e: React.FormEvent) {
    e.preventDefault();
    if (!puedeEliminarConContrasena(escrito, contrasena, enviando)) return;
    void enviar({ contrasena });
  }

  function conGoogle(credencial: string) {
    if (!puedeEliminarConGoogle(escrito, enviando)) {
      setError(`Escribe ${TEXTO_CONFIRMACION} antes de confirmar con Google.`);
      return;
    }
    void enviar({ credencialGoogle: credencial });
  }

  if (!enCliente) return null;

  return createPortal(
    <motion.div
      className="hz-overlay"
      onMouseDown={(e) => e.target === e.currentTarget && !enviando && onCerrar()}
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
    >
      <motion.form
        className="hz-modal"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="titulo-eliminar-cuenta"
        aria-describedby="texto-eliminar-cuenta"
        noValidate
        onSubmit={conContrasena}
        initial={reducir ? false : { opacity: 0, scale: 0.95, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={reducir ? { opacity: 0 } : { opacity: 0, scale: 0.97, y: 12 }}
        transition={{ type: "spring", stiffness: 380, damping: 32 }}
      >
        <h2 id="titulo-eliminar-cuenta">¿Eliminar tu cuenta?</h2>
        <div id="texto-eliminar-cuenta" style={{ display: "grid", gap: 10 }}>
          <p className={estilos.ayuda}>{TEXTO_ADVERTENCIA}</p>
          <ul className={estilos.lista}>
            <li>Se borran tu cuenta, tu horario y tus puentes.</li>
            <li>Se borran tus eventos con sus fotos, tus notificaciones y tus reportes.</li>
            <li>Se borran tus conversaciones con el asistente y tu historial de cambios.</li>
            <li>Las comunidades que creaste pasan a otro participante confirmado; si no hay, se eliminan.</li>
          </ul>
          <p className={estilos.ayuda}>
            No se borran los respaldos de la base de datos. Si vuelves a entrar con Google, se crea una cuenta nueva.
          </p>
        </div>

        <label className={estilos.campo}>
          Escribe {TEXTO_CONFIRMACION} para confirmar
          <input
            ref={primerCampo}
            type="text"
            value={escrito}
            onChange={(e) => setEscrito(e.target.value)}
            autoComplete="off"
            autoCapitalize="characters"
            spellCheck={false}
            disabled={enviando}
          />
        </label>

        {formas.contrasena && (
          <label className={estilos.campo}>
            Tu contraseña
            <input
              type="password"
              autoComplete="current-password"
              value={contrasena}
              onChange={(e) => setContrasena(e.target.value)}
              disabled={enviando}
            />
          </label>
        )}

        {formas.google && (
          <div className={estilos.google}>
            {formas.contrasena && <p className={estilos.separador}>o confirma con tu cuenta de Google</p>}
            {googleDisponible ? (
              <div aria-busy={enviando}>
                <BotonGoogle alEntrar={conGoogle} />
              </div>
            ) : (
              !formas.contrasena && <p className={estilos.ayuda}>El acceso con Google no está disponible ahora. Inténtalo más tarde.</p>
            )}
          </div>
        )}

        {error && (
          <motion.p key={error} className={estilos.error} role="alert" animate={reducir ? undefined : { x: [0, -8, 8, -5, 5, 0] }} transition={{ duration: 0.4 }}>
            {error}
          </motion.p>
        )}

        <div className="hz-acciones">
          <span className="hz-espacio" />
          <button type="button" className="hz-btn" disabled={enviando} onClick={onCerrar}>
            Cancelar
          </button>
          {formas.contrasena && (
            <button type="submit" className="hz-btn hz-btn-peligro-fuerte" disabled={!puedeEliminarConContrasena(escrito, contrasena, enviando)}>
              {enviando ? "Eliminando…" : "Eliminar mi cuenta"}
            </button>
          )}
        </div>
      </motion.form>
    </motion.div>,
    document.body,
  );
}
