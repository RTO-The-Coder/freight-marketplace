import type { ShipmentSummaryDto, TruckEvaluationResultDto, TruckingCompanySummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { ScrollView, StyleSheet, View } from 'react-native'
import { Button, Checkbox, IconButton, List, Text, TextInput, useTheme } from 'react-native-paper'
import {
  datetimeLocalToIso,
  fleetApi,
  fmtDateTime,
  offerLimitLabel,
  offersApi,
  parsePriceEur,
  truckingCompaniesApi,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BottomSheet } from './BottomSheet'
import { LoadingState, errorMessage } from './ScreenState'

/**
 * What the eligibility sheet lets the dispatcher do with the eligible trucks:
 * - view: just see them and the km each adds (the screen a notification opens);
 * - offer: an open shipment - tick trucks, price each, optional limit, send all offers at once;
 * - assign: a shipment booked straight to this company - assign it to one truck.
 */
export type ShipmentAction = 'view' | 'offer' | 'assign'

interface Props {
  shipment: ShipmentSummaryDto
  /** This device's company - every action works on its fleet. */
  company: TruckingCompanySummaryDto
  /** The action the user started; null when nothing is open. */
  action: ShipmentAction | null
  onClose: () => void
  /** Offers were sent, or the shipment was assigned - the lists need reloading. */
  onDone: () => void
}

interface Eligible {
  trucks: TruckEvaluationResultDto[]
  names: Map<string, string>
}

/** Per-truck offer draft: price as typed, limit as "YYYY-MM-DDTHH:mm" sim time or null. */
interface Draft {
  checked: boolean
  price: string
  limit: string | null
}

export function ShipmentActions({ shipment, company, action, onClose, onDone }: Props) {
  const theme = useTheme()
  const { currentTime } = useSimClock()
  const [eligible, setEligible] = useState<Eligible | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    if (!action) return
    setEligible(null)
    setLoadError(null)
    setDrafts({})
    setActionError(null)
    // The evaluation only returns truck ids; the fleet list supplies the names.
    Promise.all([
      truckingCompaniesApi.evaluateShipment(company.companyId, shipment.shipmentId),
      fleetApi.getTrucks({ truckingCompanyId: company.companyId }),
    ])
      .then(([evaluation, fleet]) =>
        setEligible({
          trucks: evaluation.trucks.filter((t) => t.isFeasible),
          names: new Map(fleet.trucks.map((t) => [t.truckId, t.truckName])),
        }),
      )
      .catch((err) => setLoadError(errorMessage(err, 'Evaluation failed.')))
  }, [action, company.companyId, shipment.shipmentId])

  if (!action) return null

  const draftOf = (truckId: string): Draft => drafts[truckId] ?? { checked: false, price: '', limit: null }
  const updateDraft = (truckId: string, change: Partial<Draft>) =>
    setDrafts((prev) => ({ ...prev, [truckId]: { ...draftOf(truckId), ...change } }))

  const ticked = Object.entries(drafts).filter(([, d]) => d.checked)
  const canSend = ticked.length > 0 && ticked.every(([, d]) => parsePriceEur(d.price) !== null) && !busy

  const sendOffers = async () => {
    setBusy(true)
    setActionError(null)
    try {
      await offersApi.sendOffers(company.companyId, shipment.shipmentId, {
        offers: ticked.map(([truckId, d]) => ({
          truckId,
          priceEur: parsePriceEur(d.price) as number,
          limitAt: d.limit ? datetimeLocalToIso(d.limit) : null,
        })),
      })
      onDone()
    } catch (err) {
      setActionError(errorMessage(err, 'Sending the offers failed.'))
    } finally {
      setBusy(false)
    }
  }

  const assign = async (truck: TruckEvaluationResultDto) => {
    setBusy(true)
    setActionError(null)
    try {
      await fleetApi.assignShipmentToTruck(
        truck.truckId,
        shipment.shipmentId,
        truck.pickupInsertIndex ?? 0,
        truck.deliveryInsertIndex ?? 0,
      )
      onDone()
    } catch (err) {
      setActionError(errorMessage(err, 'Assignment failed.'))
    } finally {
      setBusy(false)
    }
  }

  const pickLimit = async (truckId: string) => {
    const current = draftOf(truckId).limit
    const picked = await pickSimDateTime(current ? datetimeLocalToIso(current) : currentTime)
    if (picked) updateDraft(truckId, { limit: picked })
  }

  const nameOf = (truckId: string) => eligible?.names.get(truckId) ?? `Truck ${truckId.slice(0, 8)}`
  const kmLabel = (t: TruckEvaluationResultDto) =>
    t.addedDistanceKm !== undefined ? `+${t.addedDistanceKm.toFixed(1)} km to route` : 'Eligible'

  return (
    <BottomSheet title={`Eligibility at ${company.name}`} visible onClose={onClose}>
      {loadError ? (
        <SheetError message={loadError} />
      ) : !eligible ? (
        <LoadingState />
      ) : eligible.trucks.length === 0 ? (
        <Text variant="bodyMedium">{`No truck at ${company.name} can currently take this shipment.`}</Text>
      ) : (
        <ScrollView style={styles.list}>
          {eligible.trucks.map((t) =>
            action === 'offer' ? (
              <View key={t.truckId} style={styles.offerRow}>
                <Checkbox.Item
                  label={`${nameOf(t.truckId)} · ${kmLabel(t)}`}
                  status={draftOf(t.truckId).checked ? 'checked' : 'unchecked'}
                  onPress={() => updateDraft(t.truckId, { checked: !draftOf(t.truckId).checked })}
                  disabled={busy}
                  position="leading"
                  labelVariant="bodyMedium"
                />
                {draftOf(t.truckId).checked && (
                  <View style={styles.offerInputs}>
                    <TextInput
                      mode="outlined"
                      dense
                      label="Price (€)"
                      keyboardType="decimal-pad"
                      value={draftOf(t.truckId).price}
                      onChangeText={(price) => updateDraft(t.truckId, { price })}
                      style={styles.price}
                      accessibilityLabel={`Price for ${nameOf(t.truckId)}`}
                    />
                    <Button compact mode="text" onPress={() => void pickLimit(t.truckId)} disabled={busy}>
                      {draftOf(t.truckId).limit
                        ? offerLimitLabel(datetimeLocalToIso(draftOf(t.truckId).limit as string))
                        : 'No limit'}
                    </Button>
                    {draftOf(t.truckId).limit && (
                      <IconButton
                        icon="close"
                        size={18}
                        accessibilityLabel={`Remove limit for ${nameOf(t.truckId)}`}
                        onPress={() => updateDraft(t.truckId, { limit: null })}
                      />
                    )}
                  </View>
                )}
              </View>
            ) : (
              <List.Item
                key={t.truckId}
                title={nameOf(t.truckId)}
                description={kmLabel(t)}
                left={(props) => <List.Icon {...props} icon="check-circle-outline" />}
                right={
                  action === 'assign'
                    ? () => (
                        <Button mode="contained" compact disabled={busy} onPress={() => void assign(t)}>
                          Assign
                        </Button>
                      )
                    : undefined
                }
              />
            ),
          )}
        </ScrollView>
      )}
      {actionError && <SheetError message={actionError} />}
      {action === 'offer' && eligible && eligible.trucks.length > 0 && (
        <>
          <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {`Offers close ${fmtDateTime(shipment.offerDeadline)}. Tick trucks and enter a price for each.`}
          </Text>
          <Button mode="contained" onPress={() => void sendOffers()} disabled={!canSend} loading={busy}>
            {ticked.length > 1 ? `Send ${ticked.length} offers` : 'Send offer'}
          </Button>
        </>
      )}
      <Button mode="outlined" onPress={onClose}>
        Close
      </Button>
    </BottomSheet>
  )
}

function SheetError({ message }: { message: string }) {
  const theme = useTheme()
  return (
    <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
      {message}
    </Text>
  )
}

const styles = StyleSheet.create({
  // Long lists scroll inside the sheet instead of pushing it off screen.
  list: { maxHeight: 360 },
  offerRow: { marginBottom: 4 },
  offerInputs: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingLeft: 48 },
  price: { width: 120 },
})
