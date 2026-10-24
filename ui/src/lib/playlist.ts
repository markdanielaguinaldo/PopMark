import type { QueueMessage, Track } from "../bridge/protocol";

export type PlaylistEntry = {
  track: Track;
  index: number;
  kind: "previous" | "current" | "pending";
};

export function buildPlaylist(queue: Pick<QueueMessage, "current" | "previous" | "pending">): Track[] {
  const tracks = [...queue.previous];
  if (queue.current) tracks.push(queue.current);
  tracks.push(...queue.pending);
  return tracks;
}

export function buildPlaylistEntries(
  queue: Pick<QueueMessage, "current" | "previous" | "pending">,
): PlaylistEntry[] {
  const entries: PlaylistEntry[] = [];
  let index = 0;

  for (const track of queue.previous) {
    entries.push({ track, index: index++, kind: "previous" });
  }

  if (queue.current) {
    entries.push({ track: queue.current, index: index++, kind: "current" });
  }

  for (const track of queue.pending) {
    entries.push({ track, index: index++, kind: "pending" });
  }

  return entries;
}

export function playlistIndex(
  queue: Pick<QueueMessage, "current" | "previous">,
): number {
  return queue.current ? queue.previous.length : -1;
}

export function moveSelection(
  current: number,
  delta: number,
  length: number,
): number {
  if (length <= 0) return -1;
  if (current < 0) return delta >= 0 ? 0 : length - 1;
  return Math.min(length - 1, Math.max(0, current + delta));
}

/**
 * Keeps the user's queue selection stable while background updates arrive.
 * Follow the current track only when it actually changed; otherwise preserve
 * the selected row and clamp it if the queue shrank.
 */
export function followCurrentSelection(
  currentIndex: number,
  selectedIndex: number,
  length: number,
  previousCurrentIndex: number,
): number {
  if (length <= 0) return -1;

  if (currentIndex >= 0 && currentIndex !== previousCurrentIndex)
    return currentIndex;

  if (selectedIndex >= 0 && selectedIndex < length)
    return selectedIndex;

  return currentIndex >= 0 ? currentIndex : 0;
}

export type HistoryState = {
  entries: string[];
  cursor: number;
  draft: string;
};

export function initialHistory(): HistoryState {
  return { entries: [], cursor: -1, draft: "" };
}

export function rememberCommand(state: HistoryState, command: string): HistoryState {
  const entries = state.entries.filter((entry) => entry !== command);
  entries.push(command);
  return { entries: entries.slice(-100), cursor: -1, draft: "" };
}

export function browseHistory(
  state: HistoryState,
  direction: -1 | 1,
  draft: string,
): { state: HistoryState; value: string } {
  if (state.entries.length === 0) return { state, value: draft };

  if (direction === -1) {
    const base = state.cursor === -1 ? state.entries.length : state.cursor;
    const cursor = Math.max(0, base - 1);
    return {
      state: {
        entries: state.entries,
        cursor,
        draft: state.cursor === -1 ? draft : state.draft,
      },
      value: state.entries[cursor],
    };
  }

  if (state.cursor === -1) return { state, value: draft };

  const cursor = state.cursor + 1;
  if (cursor >= state.entries.length) {
    return { state: { ...state, cursor: -1 }, value: state.draft };
  }

  return { state: { ...state, cursor }, value: state.entries[cursor] };
}
