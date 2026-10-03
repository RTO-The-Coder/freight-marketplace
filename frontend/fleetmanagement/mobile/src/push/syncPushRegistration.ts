import AsyncStorage from '@react-native-async-storage/async-storage'
import { getId, getInstallations } from '@react-native-firebase/installations'
import { truckingCompaniesApi } from '@freight/fleetmanagement-core'

/** Company id this phone is registered with on the backend; absent when not registered. */
export const PUSH_REGISTERED_KEY = 'pushRegisteredFor'

/**
 * Keeps the backend's registration of this phone in line with the notification permission.
 * Called on every app start, after the permission check:
 * - allowed: register this phone's Firebase Installation ID (FID) for the company - also
 *   picks up permission switched on later in Android settings, or a changed FID.
 * - not allowed, and registered before (permission switched off later in Android
 *   settings): remove the registration, so the backend stops sending.
 * - not allowed, never registered: nothing is sent to the backend at all.
 * Never throws: on failure the next app start tries again.
 */
export async function syncPushRegistration(companyId: string, allowed: boolean): Promise<void> {
  try {
    if (allowed) {
      const fid = await getId(getInstallations())
      await truckingCompaniesApi.registerDevice(companyId, fid)
      await AsyncStorage.setItem(PUSH_REGISTERED_KEY, companyId)
      return
    }

    const registeredFor = await AsyncStorage.getItem(PUSH_REGISTERED_KEY)
    if (!registeredFor) return

    const fid = await getId(getInstallations())
    await truckingCompaniesApi.unregisterDevice(registeredFor, fid)
    await AsyncStorage.removeItem(PUSH_REGISTERED_KEY)
  } catch (err) {
    console.warn('Could not update this phone’s push notification registration.', err)
  }
}
