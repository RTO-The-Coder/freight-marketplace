# 6. Real FCM push notifications behind an INotificationSender abstraction

## Status
Accepted

## Context
Beyond in-app live updates (SSE/WebSocket off the event backbone), the dispatcher-facing mobile app should be able to notify a dispatcher of events (new shipment entered eligible pool, bid auto-withdrawn, bid lost) even when the app is closed — this requires real OS-level push notifications, which on React Native means Firebase Cloud Messaging (FCM). FCM requires a Firebase project and credentials (a service-account key for the backend, app registration for the client) — this conflicts with the project's general "clone and run without external keys" goal (ADR 0005), the same tension already accepted for the AI/Insights layer.

## Decision
Implement **real, fully committed FCM push** — not a simulated or stubbed version — but place it behind an `INotificationSender` abstraction (Dependency Inversion, matching the existing `IPositionProvider` pattern from Section 11.3), with the real FCM implementation as one concrete provider and a no-op/log-based implementation as the default. This keeps the app runnable with zero setup while the genuine, production-shaped FCM code is still present and reviewable in the repo.

## Consequences
- A visitor cloning the repo can run the full app without a Firebase account; push simply no-ops (falls back to console/log) unless they configure their own Firebase project and credentials.
- The FCM implementation is real, working code — not a mock — so it demonstrates genuine third-party integration and DIP in the same pattern already used for the GPS position source, rather than being pure aspiration.
- Adds Firebase SDK integration on the mobile client (device token registration) and a service-account credential path on the backend — real, if bounded, additional Slice 7 scope.
- Same category of trade-off as the AI/Insights layer (ADR forthcoming with Slice 8): genuinely optional, key-gated enhancement, clearly documented in the README as such.
