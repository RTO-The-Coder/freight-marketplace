# Mobile app: receive "new shipment" push notifications

For the fleet-management mobile app slice (`frontend/fleetmanagement/mobile`).
Written 2026-10-03. The backend part is built but not committed yet; this file lists what
the mobile app has to do so that push notifications actually reach a phone.

## Why

When a shipper books a shipment, the backend notifies **every** trucking company. It does no
filtering and no eligibility check (ADR 0007, `docs/adr/0007-shipment-evaluation-insertion-search.md`).
A dispatcher who is interested then checks their own fleet with the existing
"check eligibility" call. The notification is real FCM push (ADR 0003,
`docs/adr/0003-fcm-push-behind-notification-abstraction.md`).

Today the backend can send, but nothing reaches a phone, because no phone has registered.
That registration is this slice's job.

## How the backend sends (already built)

- **The phone is addressed by its Firebase Installation ID (FID), not an FCM registration
  token.** The Firebase Admin SDK the backend uses (FirebaseAdmin 3.6.0) marks sending by
  registration token as deprecated and replaces it with FIDs, and this project uses no
  deprecated APIs. **Do not send the FCM registration token (`getToken()`) to the backend.**
- **One device per company.** Registering again for the same company replaces the FID
  stored for it.
- **Stored encrypted.** The FID is encrypted (AES-GCM) before it is saved. Nothing to do on
  the app side.
- **Sending.** On every booking, the backend sends one push to every registered FID.
  It never fails the booking if the push fails.

## What the app must do

### 1. Set up Firebase in the app

- Add the Firebase React Native libraries the app needs to:
  - get the device's **Installation ID**, and
  - **receive** push messages, in the foreground and the background.

  The app is Expo SDK 57 with a native Android build (`expo run:android`), so libraries with
  native code are fine. Follow `frontend/fleetmanagement/mobile/AGENTS.md`: install with
  `npx expo install`, configure through `app.json` config plugins, never hand-edit `android/`,
  and check the current Expo docs rather than memory.
- **Add `google-services.json`.** Download it from the **same Firebase project** as the
  backend's service-account key (the file `firebase-service-account.json` the owner keeps locally). It is
  already gitignored, so never commit it.
- **Notification permission.** Android 13 and later need the `POST_NOTIFICATIONS` runtime
  permission, so ask for it.
- **Emulator.** Testing on an emulator needs a Google Play system image, or FCM won't deliver.

### 2. Verify FID delivery first

Sending to a FID is newer than registration tokens. **Before building the rest, confirm that
a push sent to a FID actually arrives** on the device with the libraries you chose. A quick
check: get the FID on the device, register it (step 3), then book a shipment.

If FID-addressed pushes can't be received, **stop and tell the owner.** The backend design
depends on this.

### 3. Register the FID with the backend

Call this when the app starts and again whenever the FID changes (for example after a
reinstall or after app data is cleared):

```
POST /companies/{companyId}/device-token
Content-Type: application/json

{ "fid": "<the device's Firebase Installation ID>" }
```

- **Success:** `204 No Content`.
- **Errors:** `400` with `{ "error": "..." }` when `fid` is empty or the company doesn't exist.
- **API client.** The call is not in `@freight/api-client` yet. Add it to
  `frontend/api-client/src/truckingCompaniesApi.ts`, next to `evaluateShipment`. For example:
  `registerDevice: (companyId: string, fid: string) => client.post<void>(`/companies/${companyId}/device-token`, { fid })`.
  `client.post` already returns `undefined` for a `204`.

### 4. Handle the notification

**What the backend sends:**

| Field | Value |
|---|---|
| `notification.title` | `New shipment available` |
| `notification.body` | e.g. `Refrigerated needed, pickup from 52.5200,13.4050 by 03/10/2026 14:00.` |
| `data.shipmentId` | the booked shipment's id (GUID string) |

**When it arrives:**
- **App in background or closed:** the OS shows the notification itself.
- **App in foreground:** show something in the app (e.g. a snackbar), because Android doesn't
  show the system notification while the app is open.
- **When tapped:** open the Shipments tab (`OpenShipmentsScreen`) on that shipment, so the
  dispatcher can run "check eligibility" for their company. That flow already exists in
  `ShipmentActions.tsx` (`truckingCompaniesApi.evaluateShipment`).

## Decision the owner needs to make first

**Which company does a phone register for?** The app has no login. Today the user picks a
company from a list, and the backend stores one device per company. Options:

- **A. Register for the company the user opens.**
  - Simple.
  - Opening company B takes over B's notifications from whatever device had B.
  - If one phone opens several companies, the same FID gets stored for each, so that phone
    receives the same push once per company.
- **B. A "Notify me for this company" choice in the app.** The phone registers for only one
  company, and changing it is deliberate.
- **C. A fixed test company** for now, until a login exists.

There is no "unregister" endpoint yet. If the chosen option needs one, raise it with the
backend side.

## Out of scope for this slice

- **iOS.** It needs APNs setup; this is Android only for now.
- **Anything beyond a single push.** No in-app notification history, offers or bidding.
- **Backend changes.** If you need one, raise it rather than editing backend code.

## Testing end to end

1. Backend running with `Fcm:ServiceAccountPath` set in `appsettings.Development.json`.
   If a booking logs "no push sent, Fcm:ServiceAccountPath is not configured", the key
   path is wrong and the backend is only logging.
2. App on a Google Play emulator, notification permission granted, FID registered.
   Check the database: `SELECT * FROM "DeviceTokens";` should show one row for the company,
   with the FID encrypted.
3. Book a shipment (web app or `POST /shipments`).
4. The push arrives. Tapping it opens the shipment, and "check eligibility" works for that
   company.
5. If nothing arrives, check the backend log. The send logs a warning per failed device,
   naming the company.
