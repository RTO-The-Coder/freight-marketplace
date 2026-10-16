# 3. Real FCM push notifications behind an INotificationSender abstraction

## Status
Accepted

## Context
A dispatcher must hear about a new shipment even when the Android app is closed. That requires real OS-level push notifications, which on Android means Firebase Cloud Messaging (FCM). FCM requires a Firebase project and credentials (a service-account key for the backend, an app registration for the client) — this conflicts with the project's general "clone and run without external keys" goal.

## Decision
Implement **real, fully committed FCM push** — not a simulated or stubbed version — but place it behind an `INotificationSender` abstraction (Dependency Inversion), with the real FCM implementation as one concrete provider and a log-based implementation as the default when no Firebase key is configured. This keeps the backend runnable with zero setup while the genuine, production-shaped FCM code is still present and reviewable in the repo.

- **Two pushes:** "New shipment available" to every company for an open shipment, and "New shipment assigned to you" to one company for a shipment booked directly to it. Both are sent again when the shipper changes the shipment's times.
- **One phone per company:** the Android app registers its **Firebase Installation ID (FID)** for its company; registering again replaces it. The FID is **stored encrypted** (AES-GCM) with a key kept only in local configuration.
- **Never blocks the business action:** a push is sent after the booking is saved; a failure for one device, or of FCM itself, is logged and never fails the booking.

## Consequences
- The API and the web apps run without a Firebase account: without a service-account key, notifications are only logged; without an encryption key, phones can't register, but the rest of the API keeps working (this is how CI runs).
- The Android app can't be built without the developer's own `google-services.json`, so the mobile client — unlike the backend and web apps — does need a Firebase project.
- The FCM implementation is real, working code — not a mock — so it demonstrates genuine third-party integration and dependency inversion, rather than being pure aspiration.
- Adds Firebase SDK integration on the mobile client (FID registration and message handling) and a service-account credential path on the backend.
- A genuinely optional, key-gated enhancement, clearly documented in the README as such.
