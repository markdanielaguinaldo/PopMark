import { For, createMemo, createSignal, onCleanup } from "solid-js";
import { theme } from "../theme";
import { Tint } from "./Widgets";

type Star = {
  x: number;
  y: number;
  phase: number;
  speed: number;
  peak: number;
};

type Span = { text: string; color?: string };

const GLYPHS = ["·", "˙", "✦", "✧"] as const;

function seeded(seed: number) {
  let state = seed >>> 0;
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0;
    return state / 0x100000000;
  };
}

function buildStars(width: number, height: number): Star[] {
  const random = seeded((width * 73856093) ^ (height * 19349663));
  const count = Math.max(18, Math.round((width * height) / 150));
  const cx = width / 2;
  const cy = height / 2;
  const centerRadius = Math.min(width, height) * 0.28;
  const stars: Star[] = [];

  for (let index = 0; index < count; index++) {
    const x = Math.floor(random() * width);
    const y = Math.floor(random() * height);
    const distance = Math.hypot(x - cx, y - cy);
    // Keep the central reading area calm; the sky is brightest toward the edges.
    const edgeBoost = distance < centerRadius ? 0.45 : 1;
    stars.push({
      x,
      y,
      phase: random() * Math.PI * 2,
      speed: 0.24 + random() * 0.55,
      peak: (0.35 + random() * 0.65) * edgeBoost,
    });
  }

  return stars;
}

function styleFor(level: number): { glyph: string; color: string } | null {
  if (level < 0.24) return null;
  if (level < 0.42) return { glyph: GLYPHS[0], color: theme.faint };
  if (level < 0.63) return { glyph: GLYPHS[0], color: theme.dim };
  if (level < 0.8) return { glyph: GLYPHS[1], color: theme.muted };
  if (level < 0.93) return { glyph: GLYPHS[2], color: theme.textSoft };
  return { glyph: GLYPHS[3], color: theme.accent };
}

function buildRow(
  width: number,
  y: number,
  stars: Star[],
  tick: number,
): Span[] {
  const cells = new Array(width).fill(" ");
  const colors = new Array<string | undefined>(width);

  for (const star of stars) {
    if (star.y !== y) continue;

    const wave = (Math.sin(star.phase + tick * star.speed) + 1) / 2;
    const level = star.peak * (0.3 + wave * 0.7);
    const style = styleFor(level);
    if (!style) continue;

    cells[star.x] = style.glyph;
    colors[star.x] = style.color;
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
 * A deliberately sparse, dim starfield for Focus View. Each star twinkles on its
 * own phase; the central reading area is kept quieter than the edges.
 */
export function Starfield(props: { width: number; height: number }) {
  const [tick, setTick] = createSignal(0);
  const stars = createMemo(() => buildStars(props.width, props.height));

  const timer = setInterval(() => setTick((value) => value + 1), 420);
  onCleanup(() => clearInterval(timer));

  const rows = createMemo(() => {
    const count = Math.max(0, Math.floor(props.height));
    const width = Math.max(0, Math.floor(props.width));
    const current = tick();
    return Array.from({ length: count }, (_, y) => buildRow(width, y, stars(), current));
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
