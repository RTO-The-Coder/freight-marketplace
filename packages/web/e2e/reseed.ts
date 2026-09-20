import { execFile } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { promisify } from 'node:util'

const execFileAsync = promisify(execFile)

const API_BASE_URL = 'http://localhost:5017'
const HERE = path.dirname(fileURLToPath(import.meta.url))
// Repo root, two levels up from packages/web.
const SEEDER_PROJECT = path.resolve(HERE, '../../../backend/tools/Freight.Seeder')

/**
 * Truncates and repopulates the database with the fixed, deterministic seed
 * dataset (5 companies, 20 shippers, 23 shipments, sim clock at
 * 2026-08-01T05:00Z, no trucks/drivers/trips — see Freight.Seeder/Program.cs).
 * Runs the real seeder binary against the real Postgres container; there is
 * no in-memory shortcut because the tests exercise the real API end to end.
 */
export async function reseedDatabase(): Promise<void> {
  await execFileAsync('dotnet', ['run', '--project', SEEDER_PROJECT])
}

/** Fails fast with a clear message instead of every test timing out on a dead API. */
export async function assertApiIsRunning(): Promise<void> {
  try {
    const res = await fetch(`${API_BASE_URL}/simulation/time`, { signal: AbortSignal.timeout(10_000) })
    if (!res.ok) throw new Error(`unexpected status ${res.status}`)
  } catch (err) {
    throw new Error(
      `Freight API is not reachable at ${API_BASE_URL}. Start the backend (dotnet run ` +
        `--project backend/src/Freight.Api) and Postgres (docker) before running e2e tests.\n` +
        `Underlying error: ${err instanceof Error ? err.message : String(err)}`,
    )
  }
}
