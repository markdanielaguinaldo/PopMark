import { afterEach, describe, expect, test } from "bun:test";
import { existsSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { resolveBackend } from "../src/bridge/backend";

const original = process.env.POPMARK_BACKEND;
const tempFile = join(tmpdir(), `popmark-backend-${process.pid}.dll`);

afterEach(() => {
  if (original === undefined) delete process.env.POPMARK_BACKEND;
  else process.env.POPMARK_BACKEND = original;

  if (existsSync(tempFile)) rmSync(tempFile);
});

describe("resolveBackend", () => {
  test("uses an explicit POPMARK_BACKEND override", () => {
    writeFileSync(tempFile, "stub");
    process.env.POPMARK_BACKEND = tempFile;

    const launch = resolveBackend();

    expect(launch.description).toBe(tempFile);
    expect(launch.command.at(-1)).toBe("--bridge");
    expect(launch.command).toContain(tempFile);
  });

  test("rejects a missing override instead of silently using another build", () => {
    process.env.POPMARK_BACKEND = join(tmpdir(), "popmark-does-not-exist.dll");

    expect(() => resolveBackend()).toThrow(/POPMARK_BACKEND/);
  });
});
