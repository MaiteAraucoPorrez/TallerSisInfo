import Link from "next/link";
import styles from "./PiePagina.module.css";

const COLUMNAS = [
  {
    titulo: "BreakU",
    enlaces: [
      { href: "/#como-funciona", texto: "Cómo funciona" },
      { href: "/#funciones", texto: "Qué puedes hacer" },
      { href: "/#opiniones", texto: "Opiniones" },
    ],
  },
  {
    titulo: "Tu cuenta",
    enlaces: [
      { href: "/login", texto: "Iniciar sesión" },
      { href: "/login?modo=registro", texto: "Crear cuenta" },
    ],
  },
  {
    titulo: "Legal",
    enlaces: [
      { href: "/normas", texto: "Normas de la comunidad" },
      { href: "/privacidad", texto: "Política de privacidad" },
      { href: "/terminos", texto: "Términos y condiciones" },
    ],
  },
];

/**
 * Onda que empalma consigo misma en 1440 (la mitad del SVG), para desplazarla un 50 % sin que se note el corte.
 * `periodo` tiene que dividir 1440; `cresta` y `valle` son las alturas (0 arriba, 120 abajo).
 */
function ondaRepetida(periodo: number, cresta: number, valle: number) {
  const medio = (cresta + valle) / 2;
  let d = `M0,${medio}`;
  for (let x = 0; x < 2880; x += periodo) {
    const c = periodo / 4;
    d += ` C${x + c * 0.55},${cresta} ${x + c * 1.45},${cresta} ${x + c * 2},${medio}`;
    d += ` C${x + c * 2.6},${valle} ${x + c * 3.4},${valle} ${x + periodo},${medio}`;
  }
  return `${d} L2880,120 L0,120 Z`;
}

export default function PiePagina() {
  return (
    <footer className={styles.pie}>
      {/* La onda borra la línea recta entre secciones: dos capas que se deslizan lento a distinto ritmo. */}
      <div className={styles.onda} aria-hidden="true">
        <svg className={styles.ondaAtras} viewBox="0 0 2880 120" preserveAspectRatio="none">
          <path d={ondaRepetida(1440, 8, 70)} />
        </svg>
        <svg className={styles.ondaFrente} viewBox="0 0 2880 120" preserveAspectRatio="none">
          <path d={ondaRepetida(720, 34, 96)} />
        </svg>
      </div>
      <div className={styles.marco}>
        <div className={styles.presentacion}>
          <Link href="/" className={styles.marca}>
            Break<span>U</span>
          </Link>
          <p>Tu tiempo libre, bien aprovechado. Hecho por estudiantes de la UCB para estudiantes de la UCB.</p>
        </div>

        {COLUMNAS.map((c) => (
          <nav key={c.titulo} className={styles.columna} aria-label={c.titulo}>
            <h2>{c.titulo}</h2>
            <ul>
              {c.enlaces.map((e) => (
                <li key={e.href}>
                  <Link href={e.href}>{e.texto}</Link>
                </li>
              ))}
            </ul>
          </nav>
        ))}
      </div>

      <div className={styles.base}>
        <p>© 2026 BreakU · Universidad Católica Boliviana «San Pablo»</p>
        <p>Proyecto del Taller de Sistemas de Información (SIS-227)</p>
      </div>
    </footer>
  );
}
