import { describe, expect, test } from "bun:test";
import type { Track } from "../src/bridge/protocol";
import {
  browseHistory,
  buildPlaylist,
  buildPlaylistEntries,
  followCurrentSelection,
  initialHistory,
  moveSelection,
  playlistIndex,
  rememberCommand,
} from "../src/lib/playlist";

function track(title: string): Track {
  return { title, url: `https://youtube.com/watch?v=${title}`, displaySource: "YouTube" };
}

describe("playlist helpers", () => {
  test("builds the playlist in previous/current/pending order", () => {
    const playlist = buildPlaylist({
      previous: [track("a"), track("b")],
      current: track("c"),
      pending: [track("d")],
    });

    expect(playlist.map((entry) => entry.title)).toEqual(["a", "b", "c", "d"]);
    expect(playlistIndex({ previous: [track("a"), track("b")], current: track("c") })).toBe(2);
  });

  test("labels rows by section", () => {
    const entries = buildPlaylistEntries({
      previous: [track("a")],
      current: track("b"),
      pending: [track("c"), track("d")],
    });

    expect(entries.map((entry) => `${entry.kind}:${entry.track.title}`)).toEqual([
      "previous:a",
      "current:b",
      "pending:c",
      "pending:d",
    ]);
    expect(entries.map((entry) => entry.index)).toEqual([0, 1, 2, 3]);
  });

  test("handles an empty queue", () => {
    expect(buildPlaylist({ previous: [], pending: [] })).toEqual([]);
    expect(buildPlaylistEntries({ previous: [], pending: [] })).toEqual([]);
    expect(playlistIndex({ previous: [], current: undefined })).toBe(-1);
  });
});

describe("moveSelection", () => {
  test("clamps at both ends", () => {
    expect(moveSelection(0, -1, 3)).toBe(0);
    expect(moveSelection(2, 1, 3)).toBe(2);
    expect(moveSelection(1, 1, 3)).toBe(2);
  });

  test("starts at the first or last row", () => {
    expect(moveSelection(-1, 1, 3)).toBe(0);
    expect(moveSelection(-1, -1, 3)).toBe(2);
    expect(moveSelection(0, 1, 0)).toBe(-1);
  });
});

describe("followCurrentSelection", () => {
  test("follows a real track change", () => {
    expect(followCurrentSelection(2, 1, 5, 1)).toBe(2);
    expect(followCurrentSelection(0, -1, 3, -2)).toBe(0);
  });

  test("keeps a user selection through unrelated queue updates", () => {
    expect(followCurrentSelection(1, 3, 5, 1)).toBe(3);
    expect(followCurrentSelection(-1, 2, 5, -1)).toBe(2);
  });

  test("clamps when the queue shrinks and handles an empty queue", () => {
    expect(followCurrentSelection(0, 4, 2, 0)).toBe(0);
    expect(followCurrentSelection(-1, 4, 2, -1)).toBe(0);
    expect(followCurrentSelection(-1, 2, 0, -1)).toBe(-1);
  });
});

describe("command history", () => {
  test("remembers unique commands and keeps the newest last", () => {
    let history = initialHistory();
    history = rememberCommand(history, "next");
    history = rememberCommand(history, "play");
    history = rememberCommand(history, "next");

    expect(history.entries).toEqual(["play", "next"]);
  });

  test("walks backwards and forwards with a draft", () => {
    let history = rememberCommand(initialHistory(), "next");
    history = rememberCommand(history, "play");

    let step = browseHistory(history, -1, "draft");
    expect(step.value).toBe("play");
    history = step.state;

    step = browseHistory(history, -1, "draft");
    expect(step.value).toBe("next");
    history = step.state;

    step = browseHistory(history, 1, "draft");
    expect(step.value).toBe("play");
    history = step.state;

    step = browseHistory(history, 1, "draft");
    expect(step.value).toBe("draft");
  });
});
