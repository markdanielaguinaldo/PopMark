export function clamp(value: number, min: number, max: number): number {
  if (Number.isNaN(value)) return min;
  return Math.min(max, Math.max(min, value));
}

/** mm:ss for short tracks, h:mm:ss for long ones, and a placeholder when unknown. */
export function formatTime(totalSeconds: number | undefined | null): string {
  if (totalSeconds === undefined || totalSeconds === null || !Number.isFinite(totalSeconds)) {
    return "--:--";
  }

  const safe = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  const seconds = safe % 60;

  if (hours > 0) {
    return `${hours}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  }

  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}

export function formatDurationLong(totalSeconds: number | undefined | null): string {
  if (totalSeconds === undefined || totalSeconds === null || !Number.isFinite(totalSeconds)) {
    return "unknown";
  }

  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
  return `${seconds}s`;
}

export function progressRatio(elapsed: number, duration?: number): number {
  if (!duration || duration <= 0) return 0;
  return clamp(elapsed / duration, 0, 1);
}

export function truncate(text: string, maxLength: number): string {
  if (maxLength <= 0) return "";
  if (text.length <= maxLength) return text;
  if (maxLength <= 3) return text.slice(0, maxLength);
  return `${text.slice(0, maxLength - 3)}...`;
}

export function basename(path: string | undefined): string {
  if (!path) return "not found";
  const parts = path.split(/[\\/]/);
  return parts[parts.length - 1] || path;
}

/** Removes ANSI/OSC terminal sequences and non-printing control characters. */
export function stripTerminalControl(text: string): string {
  return text
    .replace(/\u001B\][^\u0007\u001B]*(?:\u0007|\u001B\\)/g, "")
    .replace(/\u001B\[[0-?]*[ -/]*[@-~]/g, "")
    .replace(/\u001B[@-_]/g, "")
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, "");
}

/** Collapses arbitrary process/diagnostic text into one safe UI line. */
export function singleLine(text: string | undefined | null, maxLength?: number): string {
  const cleaned = stripTerminalControl(text ?? "")
    .replace(/\s+/g, " ")
    .trim();
  return maxLength === undefined ? cleaned : truncate(cleaned, maxLength);
}

export function pluralize(count: number, singular: string, plural = `${singular}s`): string {
  return `${count} ${count === 1 ? singular : plural}`;
}
