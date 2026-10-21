import { StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { fmtEur, type TrackingSummary as Summary } from '@freight/fleetmanagement-core'

interface Tile {
  label: string
  value: string
  sub: string
  accent?: boolean
}

/** A strip of small tiles over the cards, three to a row. */
export function SummaryTiles({ tiles, label }: { tiles: Tile[]; label: string }) {
  const theme = useTheme()
  return (
    <View style={styles.tiles} accessibilityLabel={label}>
      {tiles.map((tile) => (
        <View key={tile.label} style={[styles.tile, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline }]}>
          <Text variant="labelSmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {tile.label}
          </Text>
          <Text variant="titleMedium" style={[styles.value, tile.accent && { color: theme.colors.primary }]}>
            {tile.value}
          </Text>
          <Text variant="labelSmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {tile.sub}
          </Text>
        </View>
      ))}
    </View>
  )
}

/** The running trips' summary, as the web strip: trips, shipments by state, and the money. */
export function TrackingSummary({ summary }: { summary: Summary }) {
  return (
    <SummaryTiles
      label="Summary of running trips"
      tiles={[
        {
          label: 'Trips running',
          value: String(summary.tripsRunning),
          sub: summary.tripsRunning === 1 ? '1 truck on the road' : `${summary.tripsRunning} trucks on the road`,
        },
        {
          label: 'Shipments on trips',
          value: String(summary.shipments),
          sub: `${summary.delivered} delivered · ${summary.onBoard} on board · ${summary.toPick} to pick`,
        },
        { label: 'Earned', value: fmtEur(summary.earned), sub: 'delivered on running trips' },
        { label: 'To deliver', value: fmtEur(summary.toDeliver), sub: 'on board + to pick' },
        {
          label: 'Offers',
          value: fmtEur(summary.offers),
          sub: `${summary.acceptedOffers} accepted · ${summary.waitingOffers} waiting`,
          accent: true,
        },
      ]}
    />
  )
}

const styles = StyleSheet.create({
  tiles: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  tile: { flexBasis: '31%', flexGrow: 1, borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: 8, gap: 1 },
  value: { fontWeight: '700', fontVariant: ['tabular-nums'] },
})
