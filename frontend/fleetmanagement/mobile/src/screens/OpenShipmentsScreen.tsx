import type {
  CompanyOfferDto,
  CompanyShipmentBoardResponse,
  ShipmentSummaryDto,
  TruckingCompanySummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native'
import { Button, Card, SegmentedButtons, Text, useTheme } from 'react-native-paper'
import {
  fmtDateTime,
  fmtEur,
  fmtWindow,
  offerLimitLabel,
  offerPositionsLabel,
  offersApi,
  sortOpenShipments,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { ShipmentActions, type ShipmentAction } from '../components/ShipmentActions'
import { ShipmentDetails } from '../components/ShipmentDetails'
import { usePullToRefresh } from '../components/usePullToRefresh'

type ListName = 'open' | 'offered' | 'approved' | 'direct'

/** One card: the shipment, plus this company's offers on it (Offered) or its accepted offer (Approved). */
interface Row {
  shipment: ShipmentSummaryDto
  offers: CompanyOfferDto[]
}

const EMPTY_TEXT: Record<ListName, string> = {
  open: 'No shipments open for offers right now.',
  offered: 'No offers waiting for a shipper.',
  approved: 'No accepted offers waiting to be added to a trip.',
  direct: 'No shipments booked directly to this company.',
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

function offerLine(o: CompanyOfferDto): string {
  return `${o.truckName} · ${offerPositionsLabel(o)} · ${fmtEur(o.priceEur)}`
}

/**
 * The Shipments tab for this device's company: four lists, each shipment in at most one.
 * - Open: open for offers, no offer from us yet -> Check eligibility, then send offers.
 * - Offered: our offers waiting for the shipper - information only.
 * - Approved: the shipper accepted our offer -> Add to trip, at the offered truck and positions.
 * - Direct: booked straight to us by the shipper -> Check eligibility, then assign to a truck.
 */
export function OpenShipmentsScreen({ company }: { company: TruckingCompanySummaryDto }) {
  const theme = useTheme()
  const { simVersion } = useSimClock()
  const [board, setBoard] = useState<CompanyShipmentBoardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [list, setList] = useState<ListName>('open')
  const [expanded, setExpanded] = useState<string | null>(null)
  const [active, setActive] = useState<{ shipment: ShipmentSummaryDto; action: ShipmentAction } | null>(null)
  const [adding, setAdding] = useState<string | null>(null)
  const [cardError, setCardError] = useState<{ shipmentId: string; message: string } | null>(null)

  const load = useCallback(
    () =>
      offersApi
        .getShipmentBoard(company.companyId)
        .then((b) => {
          setBoard(b)
          setError(null)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load shipments.'))),
    [company.companyId],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load, simVersion])

  const addToTrip = async (shipmentId: string, offerId: string) => {
    setAdding(offerId)
    setCardError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      setExpanded(null)
      await load()
    } catch (err) {
      setCardError({ shipmentId, message: errorMessage(err, 'Adding to the trip failed.') })
    } finally {
      setAdding(null)
    }
  }

  if (error && !board) return <ErrorState message={error} />
  if (!board) return <LoadingState />

  const counts: Record<ListName, number> = {
    open: board.open.length,
    offered: board.offered.length,
    approved: board.approved.length,
    direct: board.direct.length,
  }
  const rows = rowsOf(board, list)

  const extraFor = (row: Row) => {
    const s = row.shipment
    const errorLine =
      cardError?.shipmentId === s.shipmentId ? (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {cardError.message}
        </Text>
      ) : null
    switch (list) {
      case 'open':
        return <Text variant="bodyMedium">{`Offers close ${fmtDateTime(s.offerDeadline)}`}</Text>
      case 'offered':
        return (
          <View>
            <Text variant="labelMedium">Our offers</Text>
            {row.offers.map((o) => (
              <Text key={o.offerId} variant="bodyMedium">{`${offerLine(o)} · ${offerLimitLabel(o.limitAt)}`}</Text>
            ))}
          </View>
        )
      case 'approved':
        return (
          <View>
            <Text variant="labelMedium">Accepted offer</Text>
            <Text variant="bodyMedium">{offerLine(row.offers[0])}</Text>
            {errorLine}
          </View>
        )
      case 'direct':
        return <Text variant="bodyMedium">{`Booked directly to ${company.name}`}</Text>
    }
  }

  const actionsFor = (row: Row) => {
    switch (list) {
      case 'open':
        return (
          <Button mode="contained" onPress={() => setActive({ shipment: row.shipment, action: 'offer' })}>
            Check eligibility
          </Button>
        )
      case 'offered':
        return undefined
      case 'approved': {
        const offerId = row.offers[0].offerId
        return (
          <Button
            mode="contained"
            loading={adding === offerId}
            disabled={adding !== null}
            onPress={() => void addToTrip(row.shipment.shipmentId, offerId)}
          >
            Add to trip
          </Button>
        )
      }
      case 'direct':
        return (
          <Button mode="contained" onPress={() => setActive({ shipment: row.shipment, action: 'assign' })}>
            Check eligibility
          </Button>
        )
    }
  }

  return (
    <View style={styles.screen}>
      <SegmentedButtons
        style={styles.tabs}
        value={list}
        onValueChange={(value) => {
          setList(value as ListName)
          setExpanded(null)
        }}
        buttons={(['open', 'offered', 'approved', 'direct'] as const).map((name) => ({
          value: name,
          label: `${name[0].toUpperCase()}${name.slice(1)} ${counts[name]}`,
          accessibilityLabel: `${name} shipments, ${counts[name]}`,
          labelStyle: styles.tabLabel,
        }))}
      />
      <FlatList
        data={rows}
        keyExtractor={(r) => r.shipment.shipmentId}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}
        ListEmptyComponent={<EmptyState message={EMPTY_TEXT[list]} />}
        ListHeaderComponent={
          rows.length > 0 ? (
            <Text variant="bodySmall" style={styles.hint}>
              Times are simulation-clock times.
            </Text>
          ) : null
        }
        renderItem={({ item: row }) => {
          const s = row.shipment
          const open = expanded === s.shipmentId
          return (
            <Card style={styles.card} onPress={() => setExpanded(open ? null : s.shipmentId)}>
              <Card.Title title={s.requiredTruckType} subtitle={`Pickup ${fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}`} />
              {open && <ShipmentDetails shipment={s} extra={extraFor(row)} actions={actionsFor(row)} />}
            </Card>
          )
        }}
      />
      {active && (
        <ShipmentActions
          shipment={active.shipment}
          company={company}
          action={active.action}
          onClose={() => setActive(null)}
          onDone={() => {
            // Sent offers move the shipment to Offered; an assigned one leaves the lists.
            setActive(null)
            setExpanded(null)
            void load()
          }}
        />
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  tabs: { marginHorizontal: 12, marginTop: 12 },
  tabLabel: { fontSize: 12 },
  hint: { padding: 12 },
  card: { marginHorizontal: 12, marginBottom: 8 },
})
