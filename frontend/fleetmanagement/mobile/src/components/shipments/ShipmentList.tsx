import type { ShipmentSummaryDto, TruckType } from '@freight/api-client'
import { useCallback, useMemo, useState } from 'react'
import { FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import {
  boardRows,
  EMPTY_SHIPMENT_FILTERS,
  hasActiveFilters,
  matchesShipmentFilters,
  offersApi,
  SHIPMENT_LIST_EMPTY_TEXT,
  SHIPMENT_LIST_LABEL,
  SHIPMENT_LISTS,
  sortShipments,
  TRUCK_SHIPMENT_LISTS,
  truckBoardRows,
  useSimClock,
  type ShipmentBoardRow,
  type ShipmentFilters,
  type ShipmentListName,
  type ShipmentSortKey,
  type SortDirection,
} from '@freight/fleetmanagement-core'
import type { DeviceCompany } from '../../device/DeviceCompany'
import { useAppColors } from '../../theme'
import { ActionButton } from '../ActionButton'
import { AssignShipmentSheet } from '../AssignShipmentSheet'
import { ErrorState } from '../ScreenState'
import { SkeletonList } from '../Skeleton'
import { useToast } from '../Toast'
import { Stale, UpdatingTag } from '../Updating'
import { useLoader } from '../useLoader'
import { EligibleTrucksSheet, type EligibleTrucksMode } from './EligibleTrucksSheet'
import { ShipmentCard } from './ShipmentCard'
import { ShipmentFilterBar } from './ShipmentFilterBar'

interface Props {
  /** This device's company: its shipment lists, and the fleet the actions work on. */
  company: DeviceCompany
  /**
   * One truck (the truck screen): only shipments of its type, Offered / Approved only with its
   * offer. Left out on the Shipments tab.
   */
  truck?: { truckId: string; truckType: TruckType } | null
  /** A shipment was added to a truck's trip - the screen around may reload its fleet. */
  onFleetChanged?: () => void
}

/** Offers close only means something while offers can be sent or are waiting; type only with mixed types. */
const sortKeysFor = (list: ShipmentListName, oneType: boolean): ShipmentSortKey[] =>
  (list === 'open' || list === 'offered'
    ? (['pickup', 'delivery', 'load', 'type', 'offersClose'] as ShipmentSortKey[])
    : (['pickup', 'delivery', 'load', 'type'] as ShipmentSortKey[])
  ).filter((key) => !oneType || key !== 'type')

/**
 * The company's shipments, as the web's shipments column: four lists (Open, Offered, Approved,
 * Direct) with their filtered counts, type / sort / Advanced search that stay when switching
 * lists, and shipper-style cards that open to their route and actions.
 */
export function ShipmentList({ company, truck = null, onFleetChanged }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const showToast = useToast()
  const { simVersion } = useSimClock()
  const load = useCallback(() => offersApi.getShipmentBoard(company.companyId), [company.companyId])
  const { data: board, error, loading, reloading, reload } = useLoader(load, simVersion, 'Failed to load shipments.')
  const lists = truck ? TRUCK_SHIPMENT_LISTS : SHIPMENT_LISTS
  const [list, setList] = useState<ShipmentListName>(truck ? 'direct' : 'open')
  const [filters, setFilters] = useState<ShipmentFilters>(EMPTY_SHIPMENT_FILTERS)
  const [sortKey, setSortKey] = useState<ShipmentSortKey>('pickup')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [openId, setOpenId] = useState<string | null>(null)
  const [active, setActive] = useState<{ shipment: ShipmentSummaryDto; mode: EligibleTrucksMode } | null>(null)
  /** The direct shipment the full assign form is open for (choose truck and exact positions). */
  const [assigning, setAssigning] = useState<ShipmentSummaryDto | null>(null)
  const [cardError, setCardError] = useState<{ shipmentId: string; message: string } | null>(null)

  const truckId = truck?.truckId ?? null
  const truckType = truck?.truckType ?? null

  // Every list, filtered - the tabs count these.
  const filtered = useMemo(() => {
    if (!board) return null
    const byList = {} as Record<ShipmentListName, ShipmentBoardRow[]>
    for (const name of SHIPMENT_LISTS) {
      const rows = truckId && truckType ? truckBoardRows(board, name, { truckId, truckType }) : boardRows(board, name)
      byList[name] = rows.filter((r) => matchesShipmentFilters(r.shipment, filters))
    }
    return byList
  }, [board, truckId, truckType, filters])

  // Offers close isn't offered on Approved/Direct: those lists sort by pickup instead.
  const sortKeys = sortKeysFor(list, truck !== null)
  const effectiveSortKey = sortKeys.includes(sortKey) ? sortKey : 'pickup'
  const rows = useMemo(
    () => (filtered ? sortShipments(filtered[list], effectiveSortKey, sortDirection, (r) => r.shipment) : []),
    [filtered, list, effectiveSortKey, sortDirection],
  )

  if (loading) return <SkeletonList cardHeight={130} />
  if (!board || !filtered) return <ErrorState message={error ?? 'Failed to load shipments.'} />

  const allInList = (truckId && truckType ? truckBoardRows(board, list, { truckId, truckType }) : boardRows(board, list)).length

  const afterChange = (fleetChanged: boolean) => {
    setOpenId(null)
    reload()
    if (fleetChanged) onFleetChanged?.()
  }

  const addToTrip = async (shipmentId: string, offerId: string) => {
    setCardError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      showToast({ title: 'Added to the trip' })
      afterChange(true)
    } catch (err) {
      setCardError({ shipmentId, message: err instanceof Error ? err.message : 'Adding to the trip failed.' })
    }
  }

  const actionsFor = (row: ShipmentBoardRow) => {
    const s = row.shipment
    const errorLine =
      cardError?.shipmentId === s.shipmentId ? (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {cardError.message}
        </Text>
      ) : null
    switch (list) {
      case 'open':
        return (
          <Button mode="contained" onPress={() => setActive({ shipment: s, mode: 'offer' })}>
            Send offer
          </Button>
        )
      case 'offered':
        return null
      case 'approved':
        return (
          <>
            <ActionButton
              mode="contained"
              label="Add to trip"
              busyLabel="Adding…"
              onPress={() => addToTrip(s.shipmentId, row.offers[0].offerId)}
            />
            {errorLine}
          </>
        )
      case 'direct':
        return (
          // The truck screen opens the assign form with its truck chosen; the tab lists the trucks that fit first.
          <Button mode="contained" onPress={() => (truck ? setAssigning(s) : setActive({ shipment: s, mode: 'assign' }))}>
            Assign →
          </Button>
        )
    }
  }

  const header = (
    <View style={styles.header}>
      <View style={styles.tabs} accessibilityRole="tablist">
        {lists.map((name) => {
          const selected = list === name
          return (
            <Pressable
              key={name}
              accessibilityRole="tab"
              accessibilityState={{ selected }}
              onPress={() => {
                setList(name)
                setOpenId(null)
                // Shipments booked since the last load (e.g. a new direct one) show up on a tab tap.
                reload()
              }}
              style={[
                styles.tab,
                selected
                  ? { backgroundColor: colors.accentBg, borderColor: colors.accentBorder }
                  : { backgroundColor: theme.colors.surface, borderColor: colors.borderStrong },
              ]}
            >
              <Text variant="labelLarge" style={{ color: selected ? theme.colors.primary : theme.colors.onSurface }}>
                {`${SHIPMENT_LIST_LABEL[name]} ${filtered[name].length}`}
              </Text>
            </Pressable>
          )
        })}
        <UpdatingTag visible={reloading} />
      </View>

      <ShipmentFilterBar
        filters={filters}
        onChange={setFilters}
        sortKey={effectiveSortKey}
        sortKeys={sortKeys}
        onSortKeyChange={setSortKey}
        sortDirection={sortDirection}
        onSortDirectionChange={setSortDirection}
        showType={truck === null}
      />

      {/* ADR 0010: a company sends all its offers for a shipment at once. */}
      {truck && list === 'open' && (
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          A shipment leaves Open once any truck of this company has offered on it, until the shipper changes its times.
        </Text>
      )}
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
    </View>
  )

  return (
    <View style={styles.screen}>
      <FlatList
        data={rows}
        keyExtractor={(r) => `${list}-${r.shipment.shipmentId}`}
        contentContainerStyle={styles.content}
        ListHeaderComponent={header}
        // Pull to refresh reloads in place: the cards stay, faded, with "Updating".
        refreshControl={<RefreshControl refreshing={false} onRefresh={reload} />}
        ListEmptyComponent={
          <Text variant="bodyMedium" style={[styles.empty, { color: theme.colors.onSurfaceVariant }]}>
            {allInList > 0 && hasActiveFilters(filters) ? 'No shipments match these filters.' : SHIPMENT_LIST_EMPTY_TEXT[list]}
          </Text>
        }
        renderItem={({ item: row }) => (
          <Stale stale={reloading}>
            <ShipmentCard
              row={row}
              list={list}
              companyName={company.name}
              open={openId === row.shipment.shipmentId}
              onToggle={() => setOpenId((id) => (id === row.shipment.shipmentId ? null : row.shipment.shipmentId))}
              actions={actionsFor(row)}
            />
          </Stale>
        )}
      />
      {active && (
        <EligibleTrucksSheet
          shipment={active.shipment}
          company={company}
          mode={active.mode}
          onlyTruckId={truckId}
          onClose={() => setActive(null)}
          onDone={(assigned) => {
            // Sent offers move the shipment to Offered; an assigned one leaves the lists.
            setActive(null)
            afterChange(assigned)
          }}
          // The assign form opens on top: Android drops a sheet opened while another is still closing.
          onChoosePositions={() => setAssigning(active.shipment)}
        />
      )}
      {assigning && (
        <AssignShipmentSheet
          companyId={company.companyId}
          shipment={assigning}
          fixedTruckId={truckId}
          onClose={() => setAssigning(null)}
          onAssigned={(truckName, route) => {
            setAssigning(null)
            setActive(null)
            showToast({ title: `Assigned to ${truckName}`, detail: route })
            afterChange(true)
          }}
        />
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  content: { padding: 12, gap: 10 },
  header: { gap: 8 },
  tabs: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6 },
  tab: { borderRadius: 999, borderWidth: 1, paddingHorizontal: 12, paddingVertical: 5 },
  empty: { textAlign: 'center', padding: 24 },
})
