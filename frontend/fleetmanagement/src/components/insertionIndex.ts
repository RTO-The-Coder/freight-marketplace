/**
 * Resolves and clamps the pickup/delivery insertion indices for
 * `POST /trucks/{id}/assign-shipment`. Both indices are measured against the
 * PRE-insertion pending, non-Office stop list (the backend inserts the pickup
 * first, then shifts the delivery index by +1 itself). Backend invariant:
 * `0 <= pickup <= N` and `pickup <= delivery <= N`.
 *
 * `null` raw values mean "append at the end" and follow N as the route grows.
 * Resolving on every read (rather than via an effect) means a stale
 * delivery-below-pickup pair can never be sent — clamping is synchronous with
 * whatever triggered the read (a truck switch, a pickup-index change, a
 * feasibility check, or the final Assign click all see the same clamped pair).
 */
export function resolveInsertionIndices(
  pickupRaw: number | null,
  deliveryRaw: number | null,
  pendingStopCount: number,
): { pickupIndex: number; deliveryIndex: number } {
  const pickupIndex = Math.min(Math.max(pickupRaw ?? pendingStopCount, 0), pendingStopCount)
  const deliveryIndex = Math.min(Math.max(deliveryRaw ?? pendingStopCount, pickupIndex), pendingStopCount)
  return { pickupIndex, deliveryIndex }
}
