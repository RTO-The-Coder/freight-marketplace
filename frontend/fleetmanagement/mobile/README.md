# Fleet Management — Android app

The fleet-management app for trucking companies, on Android (Expo SDK 57 / React Native 0.86). It has the same fleet and shipment features as the web app, shares its logic through `@freight/fleetmanagement-core`, and draws maps with MapLibre on OpenStreetMap. Screens are in the [app tour](../../../freight-ui-screens.md#3-fleet-management--android).

## Prerequisites

- The repository set up and the API running — see the [root README](../../../README.md#run-it-locally).
- **Android Studio** with an emulator. Use a system image **with Google Play**, otherwise push notifications are not delivered.
- **JDK 17**, with `JAVA_HOME` pointing to it. Android Studio's bundled newer JDK fails the native build.
- `ANDROID_HOME` set to your Android SDK folder.

## Firebase file (required)

The build needs `google-services.json` from **your own Firebase project**:

1. In the Firebase console, add an Android app with the package name `com.freight.fleetmanagement`.
2. Download its `google-services.json` and put it at the **repository root**. It is gitignored; never commit it.

Push notifications reach the phone only when the API uses a service-account key from the same Firebase project (`Fcm:ServiceAccountPath`, see the root README). Without that key the app still runs; the API just logs notifications instead of sending them.

## API address

Copy `.env.example` to `.env` (gitignored) and keep the line for how you run the app:

| Running on | `EXPO_PUBLIC_API_BASE_URL` |
|---|---|
| Emulator | `http://10.0.2.2:5017` (the emulator's address for your PC) |
| Phone on the same Wi-Fi | `http://<your PC's IP>:5017`, with the API started on all addresses: `dotnet run --project backend/src/Freight.Api --urls http://0.0.0.0:5017` |

## Run

From this folder, with the emulator running:

```bash
npx expo run:android     # first time, or after a native change: builds and installs the app
npx expo start           # afterwards: starts the JavaScript server for the installed app
```

The app has its own native build (MapLibre and Firebase include native code), so it **does not run in Expo Go**.

## First launch

The app asks which trucking company this phone belongs to, and then shows only that company. To choose again, clear the app's data:

```bash
adb shell pm clear com.freight.fleetmanagement
```

## Tests

```bash
npm test             # component tests (Jest + React Native Testing Library)
npm run typecheck
```

## Troubleshooting

| Problem | Fix |
|---|---|
| Build fails with a Java or Gradle version error | Use JDK 17 and point `JAVA_HOME` to it. |
| Windows: the CMake / ninja step fails on long file paths | Replace `ninja.exe` in the Android SDK's `cmake/3.22.1/bin` with ninja 1.12 or later. |
| The app can't reach the API | Check `.env`, then restart `npx expo start` — the address is read once at start. On a phone, the API must listen on `0.0.0.0`. |
| No push notifications | Use a Google Play emulator image, allow notifications, and give the API the service-account key from the same Firebase project. |
