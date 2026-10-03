import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { Button, Text, useTheme } from 'react-native-paper'
import { BottomSheet } from '../components/BottomSheet'
import { CompaniesScreen } from './CompaniesScreen'

interface Props {
  /** Saves the choice; resolves once it is stored. */
  onChosen: (company: TruckingCompanySummaryDto) => Promise<void>
}

/**
 * First launch only: the device chooses which company it belongs to. The choice is
 * permanent — only reinstalling the app (or clearing its data) resets it.
 */
export function CompanySetupScreen({ onChosen }: Props) {
  const theme = useTheme()
  const [picked, setPicked] = useState<TruckingCompanySummaryDto | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const confirm = async () => {
    if (!picked) return
    setSaving(true)
    setError(null)
    try {
      await onChosen(picked)
    } catch {
      setError('Could not save the company on this device. Try again.')
      setSaving(false)
    }
  }

  return (
    <SafeAreaView style={[styles.screen, { backgroundColor: theme.colors.background }]}>
      <View style={styles.head}>
        <Text variant="headlineSmall" accessibilityRole="header">
          Which company is this device for?
        </Text>
        <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
          The app will show only this company. You can't change it later without reinstalling the app.
        </Text>
      </View>
      <View style={styles.screen}>
        <CompaniesScreen onSelect={setPicked} />
      </View>

      <BottomSheet title={`Use ${picked?.name ?? ''}?`} visible={picked !== null} onClose={() => setPicked(null)}>
        <Text variant="bodyMedium">
          {`This device will belong to ${picked?.name ?? ''}. It can only be changed by reinstalling the app.`}
        </Text>
        {error && (
          <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
            {error}
          </Text>
        )}
        <View style={styles.actions}>
          <Button mode="outlined" style={styles.action} onPress={() => setPicked(null)} disabled={saving}>
            Cancel
          </Button>
          <Button mode="contained" style={styles.action} onPress={() => void confirm()} disabled={saving}>
            {saving ? 'Saving…' : 'Confirm'}
          </Button>
        </View>
      </BottomSheet>
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  head: { padding: 16, gap: 8 },
  actions: { flexDirection: 'row', gap: 12 },
  action: { flex: 1 },
})
