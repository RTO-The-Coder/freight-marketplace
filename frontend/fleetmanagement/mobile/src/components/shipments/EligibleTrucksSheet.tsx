import type { ShipmentSummaryDto, TruckEvaluationResultDto, TruckSummaryDto } from '@freight/api-client'
import { useCallback, useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Button, Checkbox, IconButton, Text, TextInput, useTheme } from 'react-native-paper'
import {
  addedRouteLabel,
  datetimeLocalToIso,
  fleetApi,
  fmtDateTime,
  offerLimitLabel,
  offerPositionsLabel,
  offersApi,
  parsePriceEur,
  splitPlaceName,
  truckingCompaniesApi,
  TRUCK_TYPE_LABELS,
  truckUnfitReason,
  useSimClock,
} from '@freight/fleetmanagement-core'
import type { DeviceCompany } from '../../device/DeviceCompany'
import { pickSimDateTime } from '../../time/pickSimDateTime'
import { ActionButton, SLOW_ACTION_TEXT } from '../ActionButton'
import { ErrorState } from '../ScreenState'
import { Sheet } from '../Sheet'
import { Skeleton } from '../Skeleton'
import { useToast } from '../Toast'
import { useLoader } from '../useLoader'
import { useSlow } from '../useSlow'

/**
 * What the sheet lets the dispatcher do with the trucks that can take the shipment:
 * - offer: an open shipment - tick trucks, price each, optional limit, send them all at once;
 * - assign: a shipment booked straight to this company - assign it to one truck at its evaluated positions;
 * - view: just see them and the km each adds (the screen a notification opens).
 */
export type EligibleTrucksMode = 'offer' | 'assign' | 'view'

interface Props {
  shipment: ShipmentSummaryDto
  /** This device's company - its fleet is checked. */
  company: DeviceCompany
  mode: EligibleTrucksMode
  /** The truck screen: only this truck's result. */
  onlyTruckId?: string | null
  onClose: () => void
  /** Offers sent or the shipment assigned - the lists reload (`assigned`: a trip changed too). */
  onDone: (assigned: boolean) => void
  /** Assign: open the full assign form to choose the positions by hand. Left out, the link is hidden. */
  onChoosePositions?: () => void
}

interface Draft {
  checked: boolean
  price: string
  /** "YYYY-MM-DDTHH:mm" simulation time; null = no limit. */
  limit: string | null
}

const TITLE: Record<EligibleTrucksMode, string> = {
  offer: 'Send an offer',
  assign: 'Assign to a truck',
  view: 'Trucks that can take it',
}

/** The trucks of the company that can take the shipment (least extra driving first), and those of its type that can't. */
async function evaluate(companyId: string, shipment: ShipmentSummaryDto, onlyTruckId: string | null) {
  // The evaluation only returns truck ids; the fleet list supplies names and status.
  const [evaluation, fleet] = await Promise.all([
    truckingCompaniesApi.evaluateShipment(companyId, shipment.shipmentId),
    fleetApi.getTrucks({ truckingCompanyId: companyId }),
  ])
  const shown = (truckId: string) => onlyTruckId === null || truckId === onlyTruckId
  const feasibleIds = new Set(evaluation.trucks.filter((t) => t.isFeasible).map((t) => t.truckId))
  return {
    trucks: evaluation.trucks
      .filter((t) => t.isFeasible && shown(t.truckId))
      .sort((a, b) => (a.addedDistanceKm ?? Infinity) - (b.addedDistanceKm ?? Infinity)),
    // Trucks of another type are left out: only the needed type is worth explaining.
    unfit: fleet.trucks.filter((t) => t.truckType === shipment.requiredTruckType && !feasibleIds.has(t.truckId) && shown(t.truckId)),
    fleet: new Map<string, TruckSummaryDto>(fleet.trucks.map((t) => [t.truckId, t])),
  }
}

/**
 * The web's "trucks that could take it" in a sheet: each eligible truck with the km and time it
 * adds, the fleet's other trucks of the type with why they can't, and the action for the list.
 */
export function EligibleTrucksSheet({ shipment, company, mode, onlyTruckId = null, onClose, onDone, onChoosePositions }: Props) {
  const theme = useTheme()
  const showToast = useToast()
  const { currentTime } = useSimClock()
  const load = useCallback(() => evaluate(company.companyId, shipment, onlyTruckId), [company.companyId, shipment, onlyTruckId])
  const { data, error: loadError, loading } = useLoader(load, null, 'Evaluation failed.')
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [actionError, setActionError] = useState<string | null>(null)
  const [assigning, setAssigning] = useState<string | null>(null)
  const slowCheck = useSlow(loading)

  const typeLabel = TRUCK_TYPE_LABELS[shipment.requiredTruckType]
  const route = `${splitPlaceName(shipment.pickupPlaceName).name} → ${splitPlaceName(shipment.deliveryPlaceName).name}`
  const context = [typeLabel, route, mode === 'offer' ? `offers close ${fmtDateTime(shipment.offerDeadline)}` : null]
    .filter(Boolean)
    .join(' · ')

  const nameOf = (truckId: string) => data?.fleet.get(truckId)?.truckName ?? `Truck ${truckId.slice(0, 8)}`
  const atOffice = (truckId: string) => data?.fleet.get(truckId)?.status === 'AtOffice'
  const kmOf = (t: TruckEvaluationResultDto) => addedRouteLabel(t.addedDistanceKm, t.addedTimeTick, atOffice(t.truckId))
  /** Where its stops go: a fresh trip from the office, or the positions in the running route. */
  const whereOf = (t: TruckEvaluationResultDto) =>
    atOffice(t.truckId)
      ? 'New trip, starts now'
      : offerPositionsLabel({ pickupInsertIndex: t.pickupInsertIndex ?? 0, deliveryInsertIndex: t.deliveryInsertIndex ?? 0 })

  const draftOf = (truckId: string): Draft => drafts[truckId] ?? { checked: false, price: '', limit: null }
  const update = (truckId: string, change: Partial<Draft>) =>
    setDrafts((prev) => ({ ...prev, [truckId]: { ...draftOf(truckId), ...change } }))
  const ticked = Object.entries(drafts).filter(([, d]) => d.checked)
  const canSend = ticked.length > 0 && ticked.every(([, d]) => parsePriceEur(d.price) !== null)

  const send = async () => {
    setActionError(null)
    try {
      await offersApi.sendOffers(company.companyId, shipment.shipmentId, {
        offers: ticked.map(([truckId, d]) => ({
          truckId,
          priceEur: parsePriceEur(d.price) as number,
          limitAt: d.limit ? datetimeLocalToIso(d.limit) : null,
        })),
      })
      showToast({ title: ticked.length > 1 ? `${ticked.length} offers sent` : 'Offer sent', detail: route })
      onDone(false)
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Sending the offers failed.')
    }
  }

  const assign = async (t: TruckEvaluationResultDto) => {
    setActionError(null)
    setAssigning(t.truckId)
    try {
      await fleetApi.assignShipmentToTruck(t.truckId, shipment.shipmentId, t.pickupInsertIndex ?? 0, t.deliveryInsertIndex ?? 0)
      showToast({ title: `Assigned to ${nameOf(t.truckId)}`, detail: route })
      onDone(true)
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Assignment failed.')
    } finally {
      setAssigning(null)
    }
  }

  const pickLimit = async (truckId: string) => {
    const current = draftOf(truckId).limit
    const picked = await pickSimDateTime(current ? datetimeLocalToIso(current) : currentTime)
    if (picked) update(truckId, { limit: picked })
  }

  const footer =
    mode === 'offer' && data && data.trucks.length > 0 ? (
      <>
        <Button mode="outlined" style={styles.action} onPress={onClose}>
          Cancel
        </Button>
        <ActionButton
          style={styles.action}
          mode="contained"
          label={ticked.length > 1 ? `Send ${ticked.length} offers` : 'Send offer'}
          busyLabel="Sending…"
          disabled={!canSend}
          onPress={send}
        />
      </>
    ) : (
      <Button mode="outlined" style={styles.action} onPress={onClose}>
        Close
      </Button>
    )

  const notice = (text: string) => (
    <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
      {text}
    </Text>
  )

  return (
    <Sheet title={TITLE[mode]} context={context} visible onClose={onClose} footer={footer}>
      {loading && (
        <View style={styles.list} testID="eligible-skeleton">
          <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {slowCheck ? SLOW_ACTION_TEXT : 'Checking routes, windows and driving rules…'}
          </Text>
          <Skeleton height={56} />
          <Skeleton height={56} />
        </View>
      )}
      {loadError && !data && <ErrorState message={loadError} />}

      {data && data.fleet.size === 0 && notice(`${company.name} has no trucks yet.`)}
      {data && data.fleet.size > 0 && data.trucks.length === 0 && data.unfit.length === 0 && notice(`No ${typeLabel} truck in this fleet.`)}
      {/* For one truck, its reason below says it all. */}
      {data && data.trucks.length === 0 && data.unfit.length > 0 && onlyTruckId === null && notice(`No truck at ${company.name} can currently take this shipment.`)}

      {data && data.trucks.length > 0 && (
        <View style={styles.list}>
          <Text variant="labelSmall" style={[styles.heading, { color: theme.colors.onSurfaceVariant }]}>
            TRUCKS THAT CAN TAKE IT
          </Text>
          {data.trucks.map((t) =>
            mode === 'offer' ? (
              <View key={t.truckId} style={[styles.row, { borderColor: theme.colors.outline }]}>
                <Checkbox.Item
                  label={`${nameOf(t.truckId)} · ${kmOf(t)}`}
                  status={draftOf(t.truckId).checked ? 'checked' : 'unchecked'}
                  onPress={() => update(t.truckId, { checked: !draftOf(t.truckId).checked })}
                  position="leading"
                  mode="android"
                  labelVariant="bodyMedium"
                  style={styles.check}
                  labelStyle={styles.checkLabel}
                />
                {draftOf(t.truckId).checked && (
                  <View style={styles.inputs}>
                    <TextInput
                      mode="outlined"
                      dense
                      label="Price (€)"
                      keyboardType="decimal-pad"
                      value={draftOf(t.truckId).price}
                      onChangeText={(price) => update(t.truckId, { price })}
                      style={styles.price}
                      accessibilityLabel={`Price for ${nameOf(t.truckId)}`}
                    />
                    <Button compact mode="text" onPress={() => void pickLimit(t.truckId)} accessibilityLabel={`Offer limit for ${nameOf(t.truckId)}`}>
                      {draftOf(t.truckId).limit ? offerLimitLabel(datetimeLocalToIso(draftOf(t.truckId).limit as string)) : 'Limit (optional)'}
                    </Button>
                    {draftOf(t.truckId).limit && (
                      <IconButton
                        icon="close"
                        size={18}
                        accessibilityLabel={`Remove limit for ${nameOf(t.truckId)}`}
                        onPress={() => update(t.truckId, { limit: null })}
                      />
                    )}
                  </View>
                )}
              </View>
            ) : (
              <View key={t.truckId} style={[styles.row, styles.assignRow, { borderColor: theme.colors.outline }]}>
                <View style={styles.assignText}>
                  <Text variant="bodyMedium">
                    <Text variant="bodyMedium" style={styles.name}>
                      {nameOf(t.truckId)}
                    </Text>
                    <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                      {`  ${kmOf(t)}`}
                    </Text>
                  </Text>
                  <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                    {whereOf(t)}
                  </Text>
                </View>
                {mode === 'assign' && (
                  <ActionButton
                    mode="contained"
                    compact
                    label="Assign"
                    busyLabel="Assigning…"
                    busy={assigning === t.truckId}
                    disabled={assigning !== null && assigning !== t.truckId}
                    onPress={() => assign(t)}
                  />
                )}
              </View>
            ),
          )}
        </View>
      )}

      {data && data.unfit.length > 0 && (
        <View style={styles.list} accessibilityLabel="Trucks that can't take it">
          <Text variant="labelSmall" style={[styles.heading, { color: theme.colors.onSurfaceVariant }]}>
            {"TRUCKS THAT CAN'T TAKE IT"}
          </Text>
          {data.unfit.map((t) => (
            <Text key={t.truckId} variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
              {`${t.truckName} · ${truckUnfitReason(t, shipment)}`}
            </Text>
          ))}
        </View>
      )}

      {actionError && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {actionError}
        </Text>
      )}

      {mode === 'assign' && data && onChoosePositions && (
        <Pressable onPress={onChoosePositions} accessibilityRole="button" hitSlop={6}>
          <Text variant="labelLarge" style={{ color: theme.colors.primary }}>
            Choose positions by hand →
          </Text>
        </Pressable>
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  list: { gap: 8 },
  heading: { letterSpacing: 0.6 },
  row: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, paddingHorizontal: 8, paddingVertical: 4 },
  check: { paddingHorizontal: 0 },
  checkLabel: { textAlign: 'left' },
  inputs: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 4, paddingLeft: 40, paddingBottom: 6 },
  price: { width: 120 },
  assignRow: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingVertical: 8 },
  assignText: { flex: 1, gap: 2 },
  name: { fontWeight: '600' },
})
