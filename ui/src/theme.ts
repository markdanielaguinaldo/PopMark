/**
 * PopMark visual identity.
 *
 * The palette keeps the old player's teal/pink accent pair but moves the
 * surfaces to a deep, low-noise navy so the queue and transport read cleanly.
 */
export const theme = {
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
} as const;

export const brandGradient = [theme.accent, theme.info, theme.highlight] as const;
