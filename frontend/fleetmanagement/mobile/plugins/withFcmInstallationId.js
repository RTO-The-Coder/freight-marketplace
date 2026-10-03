const { AndroidConfig, withAndroidManifest } = require('expo/config-plugins')

/**
 * Makes FCM register this device by its Firebase Installation ID (FID) instead of a
 * registration token - the backend addresses pushes by FID (see
 * docs/design/mobile-push-notifications-handoff.md). Needs firebase-messaging 25.1.0+.
 * React Native Firebase has no option for this flag, and android/ is generated, so it is
 * added to the manifest here.
 */
module.exports = function withFcmInstallationId(config) {
  return withAndroidManifest(config, (config) => {
    const mainApplication = AndroidConfig.Manifest.getMainApplicationOrThrow(config.modResults)
    AndroidConfig.Manifest.addMetaDataItemToMainApplication(
      mainApplication,
      'firebase_messaging_installation_id_enabled',
      'true',
    )
    return config
  })
}
