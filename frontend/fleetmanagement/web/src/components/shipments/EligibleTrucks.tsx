import type { ShipmentSummaryDto, TruckEvaluationResultDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import {
  addedRouteLabel,
  offerPositionsLabel,
  datetimeLocalToIso,
  fleetApi,
  offersApi,
  parsePriceEur,
  truckingCompaniesApi,
  truckUnfitReason,
} from '@freight/fleetmanagement-core'

interface Draft {
  checked: boolean
  price: string
  /** "YYYY-MM-DDTHH:mm" simulation time from the datetime-local input; '' = no limit. */
  limit: string
}

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

/**
 * The company's eligible trucks for one shipment and the km each adds. Offer mode: tick trucks,
 * price each, optional limit, send them all at once. Assign mode: assign to one truck at its
 * evaluated positions.
 */
export function EligibleTrucks({
  companyId,
  companyName,
  shipment,
  mode,
  onlyTruckId = null,
  onDone,
}: {
  companyId: string
  companyName: string
  shipment: ShipmentSummaryDto
  mode: 'offer' | 'assign'
  /** The truck page: show only this truck's result. */
  onlyTruckId?: string | null
  onDone: () => void
}) {
  const [eligible, setEligible] = useState<{
    trucks: TruckEvaluationResultDto[]
    /** The fleet's other trucks of the shipment's type, which can't take it. */
    unfit: TruckSummaryDto[]
    fleet: Map<string, TruckSummaryDto>
  } | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    // The evaluation only returns truck ids; the fleet list supplies names and status.
    Promise.all([truckingCompaniesApi.evaluateShipment(companyId, shipment.shipmentId), fleetApi.getTrucks({ truckingCompanyId: companyId })])
      .then(([evaluation, fleet]) => {
        const shown = (truckId: string) => onlyTruckId === null || truckId === onlyTruckId
        const feasibleIds = new Set(evaluation.trucks.filter((t) => t.isFeasible).map((t) => t.truckId))
        setEligible({
          // Least extra driving first.
          trucks: evaluation.trucks
            .filter((t) => t.isFeasible && shown(t.truckId))
            .sort((a, b) => (a.addedDistanceKm ?? Infinity) - (b.addedDistanceKm ?? Infinity)),
          // Trucks of another type are left out: only the needed type is worth explaining.
          unfit: fleet.trucks.filter(
            (t) => t.truckType === shipment.requiredTruckType && !feasibleIds.has(t.truckId) && shown(t.truckId),
          ),
          fleet: new Map(fleet.trucks.map((t) => [t.truckId, t])),
        })
      })
      .catch((err) => setLoadError(errorText(err, 'Evaluation failed.')))
  }, [companyId, shipment.shipmentId, shipment.requiredTruckType, onlyTruckId])

  if (loadError) return <p className="alert">{loadError}</p>
  if (!eligible) return <p className="notice">Checking…</p>
  if (eligible.fleet.size === 0) return <p className="notice">{`${companyName} has no trucks yet.`}</p>
  if (eligible.trucks.length === 0 && eligible.unfit.length === 0)
    return <p className="notice">{`No ${shipment.requiredTruckType} truck in this fleet.`}</p>

  const nameOf = (truckId: string) => eligible.fleet.get(truckId)?.truckName ?? `Truck ${truckId.slice(0, 8)}`
  const kmOf = (t: TruckEvaluationResultDto) =>
    addedRouteLabel(t.addedDistanceKm, t.addedTimeTick, eligible.fleet.get(t.truckId)?.status === 'AtOffice')
  /** Where its stops go: a fresh trip from the office, or the positions in the running route. */
  const whereOf = (t: TruckEvaluationResultDto) =>
    eligible.fleet.get(t.truckId)?.status === 'AtOffice'
      ? 'New trip, starts now'
      : offerPositionsLabel({ pickupInsertIndex: t.pickupInsertIndex ?? 0, deliveryInsertIndex: t.deliveryInsertIndex ?? 0 })
  const draftOf = (truckId: string): Draft => drafts[truckId] ?? { checked: false, price: '', limit: '' }
  const update = (truckId: string, change: Partial<Draft>) =>
    setDrafts((prev) => ({ ...prev, [truckId]: { ...draftOf(truckId), ...change } }))

  const ticked = Object.entries(drafts).filter(([, d]) => d.checked)
  const canSend = ticked.length > 0 && ticked.every(([, d]) => parsePriceEur(d.price) !== null) && !busy

  const run = async (action: () => Promise<unknown>, fallback: string) => {
    setBusy(true)
    setActionError(null)
    try {
      await action()
      onDone()
    } catch (err) {
      setActionError(errorText(err, fallback))
    } finally {
      setBusy(false)
    }
  }

  const send = () =>
    run(
      () =>
        offersApi.sendOffers(companyId, shipment.shipmentId, {
          offers: ticked.map(([truckId, d]) => ({
            truckId,
            priceEur: parsePriceEur(d.price) as number,
            limitAt: d.limit ? datetimeLocalToIso(d.limit) : null,
          })),
        }),
      'Sending the offers failed.',
    )

  const assign = (t: TruckEvaluationResultDto) =>
    run(
      () => fleetApi.assignShipmentToTruck(t.truckId, shipment.shipmentId, t.pickupInsertIndex ?? 0, t.deliveryInsertIndex ?? 0),
      'Assignment failed.',
    )

  return (
    <div className="stack">
      {/* For one truck, its reason below says it all. */}
      {eligible.trucks.length === 0 && onlyTruckId === null && (
        <p className="notice">{`No truck at ${companyName} can currently take this shipment.`}</p>
      )}
      <ul className="eligibility-list">
        {eligible.trucks.map((t) =>
          mode === 'offer' ? (
            <li key={t.truckId} className="offer-row">
              <label>
                <input
                  type="checkbox"
                  checked={draftOf(t.truckId).checked}
                  disabled={busy}
                  onChange={(e) => update(t.truckId, { checked: e.target.checked })}
                />{' '}
                {nameOf(t.truckId)} <span className="muted">{kmOf(t)}</span>
              </label>
              {draftOf(t.truckId).checked && (
                <span className="offer-row__inputs">
                  <label>
                    Price (€){' '}
                    <input
                      type="text"
                      inputMode="decimal"
                      size={8}
                      aria-label={`Price for ${nameOf(t.truckId)}`}
                      value={draftOf(t.truckId).price}
                      onChange={(e) => update(t.truckId, { price: e.target.value })}
                    />
                  </label>{' '}
                  <label>
                    Limit (optional){' '}
                    <input
                      type="datetime-local"
                      aria-label={`Offer limit for ${nameOf(t.truckId)}`}
                      value={draftOf(t.truckId).limit}
                      onChange={(e) => update(t.truckId, { limit: e.target.value })}
                    />
                  </label>
                </span>
              )}
            </li>
          ) : (
            <li key={t.truckId} className="assign-row">
              <span className="assign-row__text">
                <span>
                  {nameOf(t.truckId)} <span className="muted">{kmOf(t)}</span>
                </span>
                <span className="muted">{whereOf(t)}</span>
              </span>
              <button type="button" className="btn btn--sm btn--primary" disabled={busy} onClick={() => void assign(t)}>
                Assign
              </button>
            </li>
          ),
        )}
      </ul>
      {eligible.unfit.length > 0 && (
        <ul className="eligibility-list eligibility-list--unfit" aria-label="Trucks that can't take it">
          {eligible.unfit.map((t) => (
            <li key={t.truckId}>
              {t.truckName} <span className="muted">· {truckUnfitReason(t, shipment)}</span>
            </li>
          ))}
        </ul>
      )}
      {actionError && <p className="alert">{actionError}</p>}
      {mode === 'offer' && eligible.trucks.length > 0 && (
        <div className="shipment-card__actions">
          <button type="button" className="btn btn--sm btn--primary" disabled={!canSend} onClick={() => void send()}>
            {busy ? 'Sending…' : ticked.length > 1 ? `Send ${ticked.length} offers` : 'Send offer'}
          </button>
        </div>
      )}
    </div>
  )
}
