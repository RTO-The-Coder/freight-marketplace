import { reseedDatabase } from './reseed'

// Runs after every test in the suite, pass or fail, so a test run never
// leaves fleet/trip data created during the tests sitting in the database for
// later manual testing to trip over.
export default async function globalTeardown() {
  console.log('[e2e] Reseeding database to a known state after the run…')
  await reseedDatabase()
  console.log('[e2e] Reseed complete.')
}
