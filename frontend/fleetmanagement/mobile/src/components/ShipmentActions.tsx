import type {
  ShipmentSummaryDto,
  TruckDetailDto,
  TruckEvaluationResultDto,
  TruckingCompanySummaryDto,
  TruckSummaryDto,
} from '@freight/api-client'
import { useEffect, useState } from 'react'
import { ScrollView, StyleSheet } from 'react-native'
import { Button, List, Text, useTheme } from 'react-native-paper'
import {
  fleetApi,
  shipmentsFittingTruck,
  truckingCompaniesApi,
  trucksReadyForAssignment,
} from '@freight/fleetmanagement-core'
import { AssignShipmentSheet } from './AssignShipmentSheet'
import { BottomSheet } from './BottomSheet'
import { CompanyLogo } from './CompanyLogo'
import { LoadingState, errorMessage } from './ScreenState'

export type ShipmentAction = 'assign' | 'eligibility'

type Step =
  | { kind: 'company' }
  | { kind: 'eligibility'; company: TruckingCompanySummaryDto }
  | { kind: 'truck'; company: TruckingCompanySummaryDto }
  | { kind: 'assign'; truck: TruckDetailDto }

interface Props {
  shipment: ShipmentSummaryDto
  /** The action the user started; null when nothing is open. */
  action: ShipmentAction | null
  onClose: () => void
  onAssigned: () => void
}

/**
 * Shipments-tab actions. Both start by choosing a company in a bottom sheet:
 * - Check eligibility → which of that company's trucks could take the shipment.
 * - Assign to a truck → choose one of its ready trucks that fits → the full-screen assign form.
 */
export function ShipmentActions({ shipment, action, onClose, onAssigned }: Props) {
  const [step, setStep] = useState<Step>({ kind: 'company' })
  const [loadingTruck, setLoadingTruck] = useState(false)
  const [truckError, setTruckError] = useState<string | null>(null)

  // Every new action starts again at the company choice.
  useEffect(() => {
    if (action) {
      setStep({ kind: 'company' })
      setTruckError(null)
    }
  }, [action])

  if (!action) return null

  const chooseCompany = (company: TruckingCompanySummaryDto) =>
    setStep(action === 'eligibility' ? { kind: 'eligibility', company } : { kind: 'truck', company })

  const chooseTruck = async (truckId: string) => {
    setLoadingTruck(true)
    setTruckError(null)
    try {
      setStep({ kind: 'assign', truck: await fleetApi.getTruckDetail(truckId) })
    } catch (err) {
      setTruckError(errorMessage(err, 'Failed to load truck.'))
    } finally {
      setLoadingTruck(false)
    }
  }

  return (
    <>
      <CompanySheet visible={step.kind === 'company'} onClose={onClose} onChoose={chooseCompany} />
      {step.kind === 'eligibility' && (
        <EligibilityResultSheet company={step.company} shipmentId={shipment.shipmentId} onClose={onClose} />
      )}
      {step.kind === 'truck' && (
        <TruckChoiceSheet
          company={step.company}
          shipment={shipment}
          busy={loadingTruck}
          error={truckError}
          onClose={onClose}
          onChoose={(id) => void chooseTruck(id)}
        />
      )}
      {step.kind === 'assign' && (
        <AssignShipmentSheet
          truck={step.truck}
          visible
          initialShipmentId={shipment.shipmentId}
          onClose={onClose}
          onAssigned={onAssigned}
        />
      )}
    </>
  )
}

function CompanySheet({
  visible,
  onClose,
  onChoose,
}: {
  visible: boolean
  onClose: () => void
  onChoose: (company: TruckingCompanySummaryDto) => void
}) {
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!visible) return
    setError(null)
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((r) => setCompanies(r.companies))
      .catch((err) => setError(errorMessage(err, 'Failed to load trucking companies.')))
  }, [visible])

  return (
    <BottomSheet title="Choose a company" visible={visible} onClose={onClose}>
      {error ? (
        <SheetError message={error} />
      ) : !companies ? (
        <LoadingState />
      ) : companies.length === 0 ? (
        <Text variant="bodyMedium">No trucking companies have been provisioned yet.</Text>
      ) : (
        <ScrollView style={styles.list}>
          {companies.map((c) => (
            <List.Item
              key={c.companyId}
              title={c.name}
              left={() => <CompanyLogo name={c.name} />}
              onPress={() => onChoose(c)}
            />
          ))}
        </ScrollView>
      )}
      <CloseButton onClose={onClose} />
    </BottomSheet>
  )
}

function EligibilityResultSheet({
  company,
  shipmentId,
  onClose,
}: {
  company: TruckingCompanySummaryDto
  shipmentId: string
  onClose: () => void
}) {
  const [result, setResult] = useState<{ feasible: TruckEvaluationResultDto[]; names: Map<string, string> } | null>(
    null,
  )
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    // The evaluation only returns truck ids; the fleet list supplies the names.
    Promise.all([
      truckingCompaniesApi.evaluateShipment(company.companyId, shipmentId),
      fleetApi.getTrucks({ truckingCompanyId: company.companyId }),
    ])
      .then(([evaluation, fleet]) =>
        setResult({
          feasible: evaluation.trucks.filter((t) => t.isFeasible),
          names: new Map(fleet.trucks.map((t) => [t.truckId, t.truckName])),
        }),
      )
      .catch((err) => setError(errorMessage(err, 'Evaluation failed.')))
  }, [company.companyId, shipmentId])

  return (
    <BottomSheet title={`Eligibility at ${company.name}`} visible onClose={onClose}>
      {error ? (
        <SheetError message={error} />
      ) : !result ? (
        <LoadingState />
      ) : result.feasible.length === 0 ? (
        <Text variant="bodyMedium">{`No truck at ${company.name} can currently take this shipment.`}</Text>
      ) : (
        <ScrollView style={styles.list}>
          {result.feasible.map((t) => (
            <List.Item
              key={t.truckId}
              title={result.names.get(t.truckId) ?? `Truck ${t.truckId.slice(0, 8)}`}
              description={
                t.addedDistanceKm !== undefined ? `Feasible · +${t.addedDistanceKm.toFixed(1)} km to route` : 'Feasible'
              }
              left={(props) => <List.Icon {...props} icon="check-circle-outline" />}
            />
          ))}
        </ScrollView>
      )}
      <CloseButton onClose={onClose} />
    </BottomSheet>
  )
}

function TruckChoiceSheet({
  company,
  shipment,
  busy,
  error,
  onClose,
  onChoose,
}: {
  company: TruckingCompanySummaryDto
  shipment: ShipmentSummaryDto
  busy: boolean
  error: string | null
  onClose: () => void
  onChoose: (truckId: string) => void
}) {
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    fleetApi
      .getTrucks({ truckingCompanyId: company.companyId })
      // Only trucks that may take a shipment and match this one's type and load.
      .then((r) => setTrucks(trucksReadyForAssignment(r.trucks).filter((t) => shipmentsFittingTruck(t, [shipment]).length > 0)))
      .catch((err) => setLoadError(errorMessage(err, 'Failed to load trucks.')))
  }, [company.companyId, shipment])

  return (
    <BottomSheet title="Choose a truck" visible onClose={onClose}>
      {loadError || error ? (
        <SheetError message={(loadError ?? error) as string} />
      ) : null}
      {!trucks && !loadError ? (
        <LoadingState />
      ) : trucks && trucks.length === 0 ? (
        <Text variant="bodyMedium">
          {`No truck at ${company.name} can take this shipment: it needs an active ${shipment.requiredTruckType} with a driver and enough capacity.`}
        </Text>
      ) : trucks ? (
        <ScrollView style={styles.list}>
          {trucks.map((t) => (
            <List.Item
              key={t.truckId}
              title={t.truckName}
              description={`${t.truckType} · ${t.truckSize}`}
              disabled={busy}
              left={(props) => <List.Icon {...props} icon="truck-outline" />}
              right={(props) => <List.Icon {...props} icon="chevron-right" />}
              onPress={() => onChoose(t.truckId)}
            />
          ))}
        </ScrollView>
      ) : null}
      <CloseButton onClose={onClose} />
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

function CloseButton({ onClose }: { onClose: () => void }) {
  return (
    <Button mode="outlined" onPress={onClose}>
      Close
    </Button>
  )
}

const styles = StyleSheet.create({
  // Long lists scroll inside the sheet instead of pushing it off screen.
  list: { maxHeight: 360 },
})
