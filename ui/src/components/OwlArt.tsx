import { For } from "solid-js";
import { theme } from "../theme";
import { Tint } from "./Widgets";

// A deliberately small, chibi owl. # = outline, @ = body, o = eye, v = beak.
const OWL_ROWS = [
  "....##....##....",
  "...####..####...",
  "..############..",
  ".##@oo@@@@oo@##.",
  ".##@oo@@@@oo@##.",
  ".##@@@@vv@@@@##.",
  ".##@@@@@@@@@@##.",
  "..##@@@@@@@@##..",
  "...##@@@@@@##...",
  "....##....##....",
] as const;

const GLYPHS: Record<string, { glyph: string; color: () => string }> = {
  "#": { glyph: "█", color: () => theme.accent },
  "@": { glyph: "▓", color: () => theme.accentSoft },
  o: { glyph: "◉", color: () => theme.highlight },
  v: { glyph: "▼", color: () => theme.warning },
  ".": { glyph: " ", color: () => theme.bg },
};

type Span = { text: string; color: () => string };

function spans(row: string): Span[] {
  const result: Span[] = [];

  for (const marker of row) {
    const glyph = GLYPHS[marker] ?? GLYPHS["."];
    const previous = result.at(-1);
    if (previous && previous.color === glyph.color) {
      previous.text += glyph.glyph;
    } else {
      result.push({ text: glyph.glyph, color: glyph.color });
    }
  }

  return result;
}

/** The Focus View mascot. */
export function OwlArt() {
  return (
    <box
      id="focus-owl"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      flexShrink={0}
      gap={0}
    >
      <For each={OWL_ROWS}>
        {(row) => (
          <text selectable={false} wrapMode="none">
            <For each={spans(row)}>
              {(span) => <Tint color={span.color()}>{span.text}</Tint>}
            </For>
          </text>
        )}
      </For>
      <text fg={theme.faint} selectable={false} wrapMode="none">
        hoot
      </text>
    </box>
  );
}
