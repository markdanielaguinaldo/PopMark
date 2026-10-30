import { createSignal } from "solid-js";
import { createMutable } from "solid-js/store";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";

/** Every color the OpenTUI frontend reads. */
export type Theme = {
  bg: string;
  surface: string;
  surfaceRaised: string;
  surfaceHover: string;
  border: string;
  borderStrong: string;
  text: string;
  textSoft: string;
  muted: string;
  dim: string;
  faint: string;
  accent: string;
  accentSoft: string;
  accentInk: string;
  highlight: string;
  info: string;
  warning: string;
  danger: string;
  currentRow: string;
  selectedRow: string;
  previousRow: string;
};

export const themeOptions = [
  { id: "midnight", name: "Midnight", description: "teal · pink · navy" },
  { id: "ember", name: "Ember", description: "amber · coral · charcoal" },
  { id: "sakura", name: "Sakura", description: "blossom · violet · plum" },
  { id: "evergreen", name: "Evergreen", description: "moss · mint · forest" },
  { id: "mono", name: "Mono", description: "silver · graphite" },
  { id: "ruby", name: "Ruby", description: "scarlet · rose · ink" },
  { id: "cascade", name: "Cascade", description: "gold · honey · dusk" },
  { id: "random", name: "Random", description: "surprise palette" },
] as const;

export type ThemeId = (typeof themeOptions)[number]["id"];
export type PaletteId = Exclude<ThemeId, "random">;

const palettes: Record<PaletteId, Theme> = {
  midnight: {
    bg: "#080b11",
    surface: "#0f1520",
    surfaceRaised: "#141d2b",
    surfaceHover: "#1a2536",
    border: "#2a3a56",
    borderStrong: "#3b527a",
    text: "#d9e2f2",
    textSoft: "#aebbd0",
    muted: "#8494ad",
    dim: "#5a6a84",
    faint: "#38455c",
    accent: "#5ee7c4",
    accentSoft: "#2f8f7a",
    accentInk: "#04231d",
    highlight: "#ff7ac6",
    info: "#61a8ff",
    warning: "#ffd166",
    danger: "#ff6b7a",
    currentRow: "#10312c",
    selectedRow: "#1b2a44",
    previousRow: "#0c1119",
  },
  ember: {
    bg: "#0c0908",
    surface: "#18100d",
    surfaceRaised: "#221611",
    surfaceHover: "#2d1d16",
    border: "#4a2e22",
    borderStrong: "#6d402c",
    text: "#f5e6dc",
    textSoft: "#d8b9a8",
    muted: "#a97f6b",
    dim: "#7a5648",
    faint: "#4b332b",
    accent: "#ff9f5a",
    accentSoft: "#9a5324",
    accentInk: "#2a1105",
    highlight: "#ff5d8f",
    info: "#ffc857",
    warning: "#ffd166",
    danger: "#ff5c5c",
    currentRow: "#3a1d12",
    selectedRow: "#2b1711",
    previousRow: "#120c0a",
  },
  sakura: {
    bg: "#0d0911",
    surface: "#171020",
    surfaceRaised: "#21162c",
    surfaceHover: "#2c1d3a",
    border: "#4b335f",
    borderStrong: "#6b4a86",
    text: "#f4e9f7",
    textSoft: "#d8c4df",
    muted: "#a78bb5",
    dim: "#7b6288",
    faint: "#4c3a58",
    accent: "#ff9ecb",
    accentSoft: "#a34f78",
    accentInk: "#2b0719",
    highlight: "#b18cff",
    info: "#7fd8ff",
    warning: "#ffd166",
    danger: "#ff6b8a",
    currentRow: "#3a1830",
    selectedRow: "#2a1838",
    previousRow: "#120b17",
  },
  evergreen: {
    bg: "#07100c",
    surface: "#0d1a14",
    surfaceRaised: "#12251c",
    surfaceHover: "#193126",
    border: "#2b5040",
    borderStrong: "#3f735a",
    text: "#e2f4ea",
    textSoft: "#b9d6c6",
    muted: "#7fa793",
    dim: "#5b7a69",
    faint: "#354f42",
    accent: "#7ee787",
    accentSoft: "#3d8b52",
    accentInk: "#04200d",
    highlight: "#ffd479",
    info: "#6ccff6",
    warning: "#f2cc60",
    danger: "#ff7b72",
    currentRow: "#123a24",
    selectedRow: "#1b2a22",
    previousRow: "#0a120e",
  },
  mono: {
    bg: "#0a0a0c",
    surface: "#141417",
    surfaceRaised: "#1d1d22",
    surfaceHover: "#27272e",
    border: "#3a3a44",
    borderStrong: "#555562",
    text: "#f0f0f3",
    textSoft: "#c8c8d0",
    muted: "#9696a3",
    dim: "#6c6c78",
    faint: "#484852",
    accent: "#e8e8f0",
    accentSoft: "#8a8a99",
    accentInk: "#101014",
    highlight: "#ffffff",
    info: "#c8c8d0",
    warning: "#e0e0e8",
    danger: "#ff8a8a",
    currentRow: "#2a2a32",
    selectedRow: "#22222a",
    previousRow: "#0e0e11",
  },
  ruby: {
    bg: "#0d0709",
    surface: "#170b0f",
    surfaceRaised: "#221017",
    surfaceHover: "#2d1520",
    border: "#4a2230",
    borderStrong: "#6d2f42",
    text: "#f9e8ee",
    textSoft: "#dcb6c1",
    muted: "#b07f8e",
    dim: "#7d5562",
    faint: "#4d3039",
    accent: "#ff5c7a",
    accentSoft: "#9a2f47",
    accentInk: "#2a0710",
    highlight: "#ff9ecb",
    info: "#ffb86b",
    warning: "#ffd166",
    danger: "#ff4d4d",
    currentRow: "#3a121d",
    selectedRow: "#2b121a",
    previousRow: "#120a0d",
  },
  cascade: {
    bg: "#0d0b06",
    surface: "#17130b",
    surfaceRaised: "#221b10",
    surfaceHover: "#2d2315",
    border: "#4a3a1f",
    borderStrong: "#6d5530",
    text: "#f8f0d8",
    textSoft: "#ddcba1",
    muted: "#ac9470",
    dim: "#7b664b",
    faint: "#4c3e2d",
    accent: "#ffcc4d",
    accentSoft: "#9a7a1f",
    accentInk: "#2a1e03",
    highlight: "#ff9f5a",
    info: "#7fd8ff",
    warning: "#ffd166",
    danger: "#ff6b5c",
    currentRow: "#3a2c0f",
    selectedRow: "#2b2210",
    previousRow: "#120f09",
  },
};

function isThemeId(value: unknown): value is ThemeId {
  return themeOptions.some((option) => option.id === value);
}

const paletteIds = themeOptions
  .map((option) => option.id)
  .filter((id): id is PaletteId => id !== "random");

const [activePaletteId, setActivePaletteId] = createSignal<PaletteId>("midnight");

/** Picks a palette that is not the one currently on screen when possible. */
function pickRandomPaletteId(exclude?: PaletteId): PaletteId {
  const pool = exclude ? paletteIds.filter((id) => id !== exclude) : paletteIds;
  const candidates = pool.length > 0 ? pool : paletteIds;
  return candidates[Math.floor(Math.random() * candidates.length)];
}

function themeStatePath(): string {
  // POPMARK_HOME is the shared override used by the C# backend and the Termux
  // launcher. Otherwise match the backend: %LOCALAPPDATA% on Windows, XDG on Unix.
  const override = expandHome(process.env.POPMARK_HOME?.trim());
  if (override) return resolve(override, "theme.json");

  const root =
    process.platform === "win32"
      ? process.env.LOCALAPPDATA || process.env.APPDATA || process.cwd()
      : process.env.XDG_DATA_HOME || join(homedir(), ".local", "share");

  return join(root, "PopMark", "theme.json");
}

/** Mirrors `PopMarkState.ExpandHome` so both halves agree on `~/...` overrides. */
function expandHome(path: string | undefined): string | undefined {
  if (!path) return undefined;
  if (path === "~") return homedir();
  return path.startsWith("~/") ? join(homedir(), path.slice(2)) : path;
}

function loadThemeId(): ThemeId {
  const override = process.env.POPMARK_THEME?.toLowerCase();
  if (isThemeId(override)) return override;

  if (process.env.NODE_ENV === "test") return "midnight";

  try {
    const saved = JSON.parse(readFileSync(themeStatePath(), "utf8")) as { theme?: unknown };
    if (isThemeId(saved.theme)) return saved.theme;
  } catch {
  }

  return "midnight";
}

function saveThemeId(id: ThemeId) {
  if (process.env.NODE_ENV === "test") return;

  try {
    const path = themeStatePath();
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, JSON.stringify({ theme: id }), "utf8");
  } catch {
  }
}

const initialThemeId = loadThemeId();
const initialPaletteId = initialThemeId === "random" ? pickRandomPaletteId() : initialThemeId;
setActivePaletteId(initialPaletteId);

/**
 * Reactive palette. Components read `theme.accent` in JSX, so assigning a new
 * palette retints the whole interface without remounting the app.
 */
export const theme = createMutable<Theme>({ ...palettes[initialPaletteId] });

export const [themeId, setThemeId] = createSignal<ThemeId>(initialThemeId);

const optionForPalette = (id: PaletteId) =>
  themeOptions.find((option) => option.id === id) ?? themeOptions[0];

/** The palette that is currently painted, even when Random chose it. */
export const activeThemeInfo = () => optionForPalette(activePaletteId());

export const themeInfo = () =>
  themeOptions.find((option) => option.id === themeId()) ?? themeOptions[0];

/** Header label that keeps Random visible while naming the picked palette. */
export const themeLabel = () =>
  themeId() === "random" ? `Random · ${activeThemeInfo().name}` : activeThemeInfo().name;

export const brandGradient = () => [theme.accent, theme.info, theme.highlight] as const;

export function applyTheme(id: ThemeId, persist = true) {
  const paletteId = id === "random" ? pickRandomPaletteId(activePaletteId()) : id;
  setActivePaletteId(paletteId);
  Object.assign(theme, palettes[paletteId]);
  setThemeId(id);
  if (persist) saveThemeId(id);
}

export function cycleTheme(direction: 1 | -1 = 1) {
  const current = themeOptions.findIndex((option) => option.id === themeId());
  const next = (current + direction + themeOptions.length) % themeOptions.length;
  const option = themeOptions[next];
  applyTheme(option.id);
  return option;
}
