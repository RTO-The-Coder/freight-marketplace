import { assertApiIsRunning, reseedDatabase } from './reseed'

export default async function globalSetup() {
  await assertApiIsRunning()
  console.log('[e2e] Reseeding database to a known state before the run…')
  await reseedDatabase()
  console.log('[e2e] Reseed complete.')
}
