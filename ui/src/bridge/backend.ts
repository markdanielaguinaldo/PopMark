import { statSync } from "node:fs";
import { join, resolve } from "node:path";

export type BackendLaunch = {
  command: string[];
  cwd: string;
  description: string;
};

const here = import.meta.dir;
const uiRoot = resolve(here, "..", "..");
const repoRoot = resolve(uiRoot, "..");
// Published builds run `bun` beside `PopMark`, so its folder is the app root.
const appRoot = resolve(process.execPath, "..");

function candidates(): string[] {
  const roots = [
    repoRoot,
    appRoot,
    process.cwd(),
    resolve(process.cwd(), ".."),
  ];
  const paths: string[] = [];

  for (const root of roots) {
    for (const configuration of ["Release", "Debug"]) {
      paths.push(join(root, "PopMark", "bin", configuration, "net9.0", "PopMark.exe"));
      paths.push(join(root, "PopMark", "bin", configuration, "net9.0", "PopMark.dll"));
      // `dotnet publish` output, and the framework-dependent Linux apphost.
      paths.push(join(root, "PopMark"));
    }

    paths.push(join(root, "PopMark.exe"));
    paths.push(join(root, "PopMark"));
  }

  return [...new Set(paths)];
}

function isDotnetHost(path: string): boolean {
  return path.toLowerCase().endsWith(".dll");
}

function isFile(path: string): boolean {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}

function dotnetCommand(): string {
  return process.env.DOTNET_HOST_PATH ?? Bun.which("dotnet") ?? "dotnet";
}

/**
 * Locates the built C# backend. An explicit POPMARK_BACKEND wins, which lets
 * published installs or tests point at a specific binary.
 */
export function resolveBackend(): BackendLaunch {
  const override = process.env.POPMARK_BACKEND?.trim();

  if (override && !isFile(override)) {
    throw new Error(`POPMARK_BACKEND does not point to an existing file: ${override}`);
  }

  const searchPaths = override ? [override, ...candidates()] : candidates();
  const existing = searchPaths.filter((path) => isFile(path));

  if (existing.length === 0) {
    throw new Error(
      [
        "Could not find the PopMark backend.",
        "Build it first: dotnet build PopMark.sln --configuration Release",
        "Or set POPMARK_BACKEND to the full path of the PopMark backend (PopMark, PopMark.exe, or PopMark.dll).",
      ].join("\n"),
    );
  }

  // The dotnet host is the most reliable launch path for framework-dependent
  // builds: some Windows setups block apphost .exe files even when dotnet can
  // run the same assembly. A published single-file build has no .dll, so the
  // executable remains the fallback.
  const newest = (paths: string[]) =>
    paths
      .map((path) => {
        try {
          const stats = statSync(path);
          if (!stats.isFile()) return null;
          return { path, modified: stats.mtimeMs };
        } catch {
          return null;
        }
      })
      .filter((entry): entry is { path: string; modified: number } => entry !== null)
      .sort((a, b) => b.modified - a.modified)
      .map((entry) => entry.path);

  const dlls = newest(existing.filter((path) => isDotnetHost(path)));
  const executables = newest(existing.filter((path) => !isDotnetHost(path)));
  const dotnet = Bun.which("dotnet");

  const backend = override
    ? existing[0]
    : dlls[0] && (dotnet || executables.length === 0)
      ? dlls[0]
      : executables[0] ?? dlls[0];

  if (!backend) {
    throw new Error("Could not resolve a runnable PopMark backend.");
  }

  if (isDotnetHost(backend) && !dotnet && !process.env.DOTNET_HOST_PATH) {
    throw new Error(
      "The PopMark backend is a .NET DLL, but the dotnet host was not found on PATH. " +
        "Install the .NET 9 runtime or set DOTNET_HOST_PATH.",
    );
  }

  const command = isDotnetHost(backend)
    ? [dotnetCommand(), backend, "--bridge"]
    : [backend, "--bridge"];

  return {
    command,
    cwd: repoRoot,
    description: backend,
  };
}

export type BackendProcess = Bun.Subprocess<"pipe", "pipe", "pipe">;

export function spawnBackend(launch: BackendLaunch): BackendProcess {
  return Bun.spawn({
    cmd: launch.command,
    cwd: launch.cwd,
    stdin: "pipe",
    stdout: "pipe",
    stderr: "pipe",
    env: {
      ...process.env,
      DOTNET_NOLOGO: "1",
      DOTNET_CLI_TELEMETRY_OPTOUT: "1",
    },
  }) as BackendProcess;
}
