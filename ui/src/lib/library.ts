import type { SavedQueueInfo, Track } from "../bridge/protocol";

export type LibraryEntry =
  | {
      kind: "queue";
      index: number;
      name: string;
      trackCount: number;
      savedAtUtc: string;
    }
  | {
      kind: "favorite";
      index: number;
      track: Track;
    };

export function buildLibraryEntries(
  queues: SavedQueueInfo[],
  favorites: Track[],
): LibraryEntry[] {
  const entries: LibraryEntry[] = [];
  let index = 0;

  for (const queue of queues) {
    entries.push({
      kind: "queue",
      index: index++,
      name: queue.name,
      trackCount: queue.trackCount,
      savedAtUtc: queue.savedAtUtc,
    });
  }

  for (const track of favorites) {
    entries.push({ kind: "favorite", index: index++, track });
  }

  return entries;
}

export function savedDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleDateString([], { month: "short", day: "numeric" });
}
