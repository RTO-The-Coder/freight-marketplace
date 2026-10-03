import { PermissionsAndroid, Platform } from 'react-native'

/** Android 13 (API 33) is the first version that needs a runtime permission to show notifications. */
const FIRST_API_LEVEL_WITH_PERMISSION = 33

/**
 * Asks "Allow Fleet Management to send you notifications?" on Android 13+. Older Android
 * versions grant it at install time, so this resolves true there without asking.
 * Android itself stops showing the dialog once the user has denied it twice; after that
 * this just resolves false. Never throws - a failed request counts as not granted.
 */
export async function requestNotificationPermission(): Promise<boolean> {
  if (Platform.OS !== 'android' || Platform.Version < FIRST_API_LEVEL_WITH_PERMISSION) {
    return true
  }

  try {
    const result = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS)
    return result === PermissionsAndroid.RESULTS.GRANTED
  } catch {
    return false
  }
}
