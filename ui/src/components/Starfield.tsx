import { For, createMemo, createSignal, onCleanup } from "solid-js";
import { theme } from "../theme";
import { Tint } from "./Widgets";

type Layer = "far" | "mid" | "near";

type Star = {
  x: number;
  y: number;
  phase: number;
  speed: number;
  min: number;
  max: number;
  glyph: string;
  layer: Layer;
};

type Span = { text: string; color?: string };

const GLYPHS: Record<Layer, string> = {
  far: "·",
  mid: "·",
  near: "✦",
};

function seeded(seed: number) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

function hexToRgb(hex: string): [number, number, number] {
  const value = hex.replace("#", "");
  return [
    parseInt(value.slice(0, 2), 16),
    parseInt(value.slice(2, 4), 16),
    parseInt(value.slice(4, 6), 16),
  ];
}

function mixColor(from: string, to: string, amount: number): string {
  const a = hexToRgb(from);
  const b = hexToRgb(to);
  const t = Math.max(0, Math.min(1, amount));
  const channel = (index: number) =>
    Math.round(a[index] + (b[index] - a[index]) * t)
      .toString(16)
      .padStart(2, "0");
  return `#${channel(0)}${channel(1)}${channel(2)}`;
}

function buildStars(width: number, height: number): Star[] {
  const random = seeded((width * 73856093) ^ (height * 19349663));
  // Deliberately sparse: a handful of lights, never a busy sky.
  const count = Math.max(6, Math.min(18, Math.round((width * height) / 520)));
  const stars: Star[] = [];

  for (let index = 0; index < count; index++) {
    const layer: Layer = index < count * 0.58 ? "far" : index < count * 0.86 ? "mid" : "near";
    const near = layer === "near";
    const mid = layer === "mid";

    stars.push({
      x: Math.floor(random() * width),
      y: Math.floor(random() * height),
      phase: random() * Math.PI * 2,
      speed: 0.16 + random() * 0.28,
      min: near ? 0.65 : mid ? 0.4 : 0.22,
      max: near ? 0.95 : mid ? 0.72 : 0.52,
      glyph: GLYPHS[layer],
      layer,
    });
  }

  return stars;
}

function starColor(star: Star, level: number): string {
  if (star.layer === "near") return mixColor(theme.muted, theme.textSoft, level);
  if (star.layer === "mid") return mixColor(theme.faint, theme.muted, level);
  return mixColor(theme.faint, theme.dim, level);
}

function buildRow(
  width: number,
  y: number,
  stars: Star[],
  now: number,
  cx: number,
  cy: number,
  centerRadius: number,
): Span[] {
  const cells = new Array(width).fill(" ");
  const colors = new Array<string | undefined>(width);

  for (const star of stars) {
    if (star.y !== y) continue;

    const pulse = (Math.sin(star.phase + now * star.speed) + 1) / 2;
    const distance = Math.hypot(star.x - cx, star.y - cy);
    const centerFactor = distance < centerRadius ? 0.52 : 1;
    const level = (star.min + (star.max - star.min) * pulse) * centerFactor;

    cells[star.x] = star.glyph;
    colors[star.x] = starColor(star, level);
  }

  const spans: Span[] = [];
  let text = "";
  let color: string | undefined;

  for (let x = 0; x < width; x++) {
    const next = colors[x];
    if (next !== color) {
      if (text) spans.push({ text, color });
      text = "";
      color = next;
    }
    text += cells[x];
  }

  if (text) spans.push({ text, color });
  return spans;
}

/**
 * Minimal three-depth starfield for Focus View. Stars never blink in or out;
 * only their color drifts slowly, so the effect reads as ambient light.
 */
export function Starfield(props: { width: number; height: number }) {
  const [tick, setTick] = createSignal(0);
  const stars = createMemo(() => buildStars(props.width, props.height));

  const timer = setInterval(() => setTick((value) => value + 1), 150);
  onCleanup(() => clearInterval(timer));

  const rows = createMemo(() => {
    tick();
    const width = Math.max(0, Math.floor(props.width));
    const height = Math.max(0, Math.floor(props.height));
    const now = Date.now() / 1000;
    const cx = width / 2;
    const cy = height / 2;
    const centerRadius = Math.min(width, height) * 0.3;

    return Array.from({ length: height }, (_, y) =>
      buildRow(width, y, stars(), now, cx, cy, centerRadius),
    );
  });

  return (
    <box
      id="focus-stars"
      position="absolute"
      top={0}
      left={0}
      width="100%"
      height="100%"
      zIndex={0}
      flexDirection="column"
      overflow="hidden"
    >
      <For each={rows()}>
        {(row) => (
          <text selectable={false} wrapMode="none">
            <For each={row}>
              {(span) =>
                span.color ? <Tint color={span.color}>{span.text}</Tint> : span.text
              }
            </For>
          </text>
        )}
      </For>
    </box>
  );
}
