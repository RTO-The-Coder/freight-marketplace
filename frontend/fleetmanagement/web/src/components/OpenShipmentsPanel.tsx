import type {
  CompanyOfferDto,
  CompanyShipmentBoardResponse,
  ShipmentSummaryDto,
  TruckEvaluationResultDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import {
  capacityFill,
  datetimeLocalToIso,
  fleetApi,
  fmtDateTime,
  fmtEur,
  fmtRelative,
  fmtWindow,
  offerLimitLabel,
  offerPositionsLabel,
  offersApi,
  parsePriceEur,
  sortOpenShipments,
  truckingCompaniesApi,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { Modal } from './Modal'
import { ShipmentRouteMap } from './ShipmentRouteMap'

interface OpenShipmentsPanelProps {
  /** This company: its shipment lists, and the fleet eligibility is checked against. */
  companyId: string
  companyName: string
  onClose: () => void
  /** A shipment was added to one of the company's trucks - the fleet view should reload. */
  onFleetChanged: () => void
  /** Open the full assign form (choose truck and exact positions) - for direct shipments. */
  onAssign: () => void
}

type ListName = 'open' | 'offered' | 'approved' | 'direct'

const LIST_LABEL: Record<ListName, string> = { open: 'Open', offered: 'Offered', approved: 'Approved', direct: 'Direct' }

const EMPTY_TEXT: Record<ListName, string> = {
  open: 'No shipments open for offers right now.',
  offered: 'No offers waiting for a shipper.',
  approved: 'No accepted offers waiting to be added to a trip.',
  direct: 'No shipments booked directly to this company.',
}

interface Row {
  shipment: ShipmentSummaryDto
  offers: CompanyOfferDto[]
}

function rowsOf(board: CompanyShipmentBoardResponse, list: ListName): Row[] {
  switch (list) {
    case 'open':
      return sortOpenShipments(board.open).map((shipment) => ({ shipment, offers: [] }))
    case 'offered':
      return board.offered.map((o) => ({ shipment: o.shipment, offers: o.offers }))
    case 'approved':
      return board.approved.map((a) => ({ shipment: a.shipment, offers: [a.offer] }))
    case 'direct':
      return sortOpenShipments(board.direct).map((shipment) => ({ shipment, offers: [] }))
  }
}

const offerLine = (o: CompanyOfferDto) => `${o.truckName} · ${offerPositionsLabel(o)} · ${fmtEur(o.priceEur)}`

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

/**
 * The company's shipments, in four lists - each shipment is in at most one:
 * - Open: open for offers, no offer from us yet -> Check eligibility, then send offers.
 * - Offered: our offers waiting for the shipper - information only.
 * - Approved: the shipper accepted our offer -> Add to trip, at the offered truck and positions.
 * - Direct: booked straight to us by the shipper -> Check eligibility, then assign to a truck.
 */
export function OpenShipmentsPanel({ companyId, companyName, onClose, onFleetChanged, onAssign }: OpenShipmentsPanelProps) {
  const { currentTime, simVersion } = useSimClock()
  const [board, setBoard] = useState<CompanyShipmentBoardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [list, setList] = useState<ListName>('open')
  const [expanded, setExpanded] = useState<string | null>(null)
  const [eligibleFor, setEligibleFor] = useState<string | null>(null)
  const [adding, setAdding] = useState<string | null>(null)
  const [cardError, setCardError] = useState<{ shipmentId: string; message: string } | null>(null)

  const load = useCallback(
    () =>
      offersApi
        .getShipmentBoard(companyId)
        .then((b) => {
          setBoard(b)
          setError(null)
        })
        .catch((err) => setError(errorText(err, 'Failed to load shipments.'))),
    [companyId],
  )

  useEffect(() => {
    void load()
  }, [load, simVersion])

  const toggle = (shipmentId: string) => {
    setExpanded((current) => (current === shipmentId ? null : shipmentId))
    setEligibleFor(null)
    setCardError(null)
  }

  const afterChange = (fleetChanged: boolean) => {
    setExpanded(null)
    setEligibleFor(null)
    void load()
    if (fleetChanged) onFleetChanged()
  }

  const addToTrip = async (shipmentId: string, offerId: string) => {
    setAdding(offerId)
    setCardError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      afterChange(true)
    } catch (err) {
      setCardError({ shipmentId, message: errorText(err, 'Adding to the trip failed.') })
    } finally {
      setAdding(null)
    }
  }

  const counts = board
    ? { open: board.open.length, offered: board.offered.length, approved: board.approved.length, direct: board.direct.length }
    : null
  const rows = board ? rowsOf(board, list) : []

  return (
    <Modal title="Shipments" onClose={onClose}>
      <div className="stack">
        <div className="tab-row" role="tablist" aria-label="Shipment lists">
          {(Object.keys(LIST_LABEL) as ListName[]).map((name) => (
            <button
              key={name}
              type="button"
              role="tab"
              aria-selected={list === name}
              className={`btn btn--sm${list === name ? ' btn--primary' : ''}`}
              onClick={() => {
                setList(name)
                setExpanded(null)
                setEligibleFor(null)
              }}
            >
              {LIST_LABEL[name]}
              {counts ? ` (${counts[name]})` : ''}
            </button>
          ))}
        </div>

        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-subtle)', margin: 0 }}>
          Times are simulation-clock times.
        </p>

        {error && <p className="alert">{error}</p>}
        {!board && !error && <p className="notice">Loading…</p>}
        {board && rows.length === 0 && <p className="notice">{EMPTY_TEXT[list]}</p>}

        <ul className="shipment-list">
          {rows.map((row) => {
            const s = row.shipment
            const open = expanded === s.shipmentId
            const cap = capacityFill(s)
            return (
              <li key={s.shipmentId} className="shipment-card">
                <button type="button" className="shipment-card__head" onClick={() => toggle(s.shipmentId)}>
                  <span className="shipment-card__type">{s.requiredTruckType}</span>
                  <span className="shipment-card__pickup">{fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}</span>
                  <span className="shipment-card__chev">{open ? '▾' : '▸'}</span>
                </button>

                {open && (
                  <div className="shipment-card__body">
                    <ShipmentRouteMap
                      pickup={{ latitude: s.pickupLatitude, longitude: s.pickupLongitude }}
                      delivery={{ latitude: s.deliveryLatitude, longitude: s.deliveryLongitude }}
                    />

                    <dl className="shipment-facts">
                      <dt>Load</dt>
                      <dd>
                        <div className="capbar">
                          <span className="capbar__fill" style={{ width: `${cap.weightPct}%` }} />
                        </div>
                        <span className="capbar__label">
                          {cap.weightLabel} · {cap.volumeLabel} <span className="muted">vs Large truck</span>
                        </span>
                      </dd>

                      <dt>Pickup</dt>
                      <dd>
                        {fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}{' '}
                        <span className="muted">({fmtRelative(s.pickupWindowEarliest, currentTime)})</span>
                      </dd>

                      <dt>Deliver</dt>
                      <dd>{fmtWindow(s.deliveryWindowEarliest, s.deliveryWindowLatest)}</dd>

                      {list === 'open' && (
                        <>
                          <dt>Offers close</dt>
                          <dd>{fmtDateTime(s.offerDeadline)}</dd>
                        </>
                      )}
                      {list === 'offered' && (
                        <>
                          <dt>Our offers</dt>
                          <dd>
                            {row.offers.map((o) => (
                              <div key={o.offerId}>{`${offerLine(o)} · ${offerLimitLabel(o.limitAt)}`}</div>
                            ))}
                          </dd>
                        </>
                      )}
                      {list === 'approved' && (
                        <>
                          <dt>Accepted offer</dt>
                          <dd>{offerLine(row.offers[0])}</dd>
                        </>
                      )}
                      {list === 'direct' && (
                        <>
                          <dt>Booked</dt>
                          <dd>{`Directly to ${companyName}`}</dd>
                        </>
                      )}
                    </dl>

                    {(list === 'open' || list === 'direct') && (
                      <div className="shipment-card__actions">
                        <button
                          type="button"
                          className="btn btn--sm btn--primary"
                          onClick={() => setEligibleFor(eligibleFor === s.shipmentId ? null : s.shipmentId)}
                        >
                          Check eligibility
                        </button>
                        {list === 'direct' && (
                          <button type="button" className="btn btn--sm" onClick={onAssign}>
                            Assign to a truck →
                          </button>
                        )}
                      </div>
                    )}
                    {list === 'approved' && (
                      <div className="shipment-card__actions">
                        <button
                          type="button"
                          className="btn btn--sm btn--primary"
                          disabled={adding !== null}
                          onClick={() => void addToTrip(s.shipmentId, row.offers[0].offerId)}
                        >
                          {adding === row.offers[0].offerId ? 'Adding…' : 'Add to trip'}
                        </button>
                      </div>
                    )}
                    {cardError?.shipmentId === s.shipmentId && <p className="alert">{cardError.message}</p>}

                    {eligibleFor === s.shipmentId && (list === 'open' || list === 'direct') && (
                      <EligibleTrucks
                        companyId={companyId}
                        companyName={companyName}
                        shipment={s}
                        mode={list === 'open' ? 'offer' : 'assign'}
                        onDone={() => afterChange(list === 'direct')}
                      />
                    )}
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}

interface Draft {
  checked: boolean
  price: string
  /** "YYYY-MM-DDTHH:mm" simulation time from the datetime-local input; '' = no limit. */
  limit: string
}

/**
 * The company's eligible trucks for one shipment and the km each adds. Offer mode: tick trucks,
 * price each, optional limit, send them all at once. Assign mode: assign to one truck at its
 * evaluated positions.
 */
function EligibleTrucks({
  companyId,
  companyName,
  shipment,
  mode,
  onDone,
}: {
  companyId: string
  companyName: string
  shipment: ShipmentSummaryDto
  mode: 'offer' | 'assign'
  onDone: () => void
}) {
  const [eligible, setEligible] = useState<{ trucks: TruckEvaluationResultDto[]; names: Map<string, string> } | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    // The evaluation only returns truck ids; the fleet list supplies the names.
    Promise.all([truckingCompaniesApi.evaluateShipment(companyId, shipment.shipmentId), fleetApi.getTrucks({ truckingCompanyId: companyId })])
      .then(([evaluation, fleet]) =>
        setEligible({
          trucks: evaluation.trucks.filter((t) => t.isFeasible),
          names: new Map(fleet.trucks.map((t) => [t.truckId, t.truckName])),
        }),
      )
      .catch((err) => setLoadError(errorText(err, 'Evaluation failed.')))
  }, [companyId, shipment.shipmentId])

  if (loadError) return <p className="alert">{loadError}</p>
  if (!eligible) return <p className="notice">Checking…</p>
  if (eligible.trucks.length === 0) return <p className="notice">{`No truck at ${companyName} can currently take this shipment.`}</p>

  const nameOf = (truckId: string) => eligible.names.get(truckId) ?? `Truck ${truckId.slice(0, 8)}`
  const kmOf = (t: TruckEvaluationResultDto) => (t.addedDistanceKm !== undefined ? `+${t.addedDistanceKm.toFixed(1)} km to route` : '')
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
            <li key={t.truckId}>
              {nameOf(t.truckId)} <span className="muted">{kmOf(t)}</span>{' '}
              <button type="button" className="btn btn--sm btn--primary" disabled={busy} onClick={() => void assign(t)}>
                Assign
              </button>
            </li>
          ),
        )}
      </ul>
      {actionError && <p className="alert">{actionError}</p>}
      {mode === 'offer' && (
        <div className="shipment-card__actions">
          <button type="button" className="btn btn--sm btn--primary" disabled={!canSend} onClick={() => void send()}>
            {busy ? 'Sending…' : ticked.length > 1 ? `Send ${ticked.length} offers` : 'Send offer'}
          </button>
          <span className="muted" style={{ fontSize: 'var(--text-sm)' }}>
            {`Offers close ${fmtDateTime(shipment.offerDeadline)}. Times are simulation-clock times (UTC).`}
          </span>
        </div>
      )}
    </div>
  )
}
