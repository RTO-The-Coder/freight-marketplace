import { StyleSheet, View } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import { BottomSheet } from './BottomSheet'

interface Props {
  title: string
  message: string
  confirmLabel: string
  busy: boolean
  visible: boolean
  onCancel: () => void
  onConfirm: () => void
}

/** Confirmation for a destructive action, as a bottom sheet (thumb reach). */
export function ConfirmSheet({ title, message, confirmLabel, busy, visible, onCancel, onConfirm }: Props) {
  const theme = useTheme()
  return (
    <BottomSheet title={title} visible={visible} onClose={onCancel}>
      <Text variant="bodyMedium">{message}</Text>
      <View style={styles.actions}>
        <Button mode="outlined" style={styles.action} onPress={onCancel}>
          Cancel
        </Button>
        <Button
          mode="contained"
          style={styles.action}
          buttonColor={theme.colors.error}
          textColor={theme.colors.onError}
          disabled={busy}
          onPress={onConfirm}
        >
          {busy ? 'Working…' : confirmLabel}
        </Button>
      </View>
    </BottomSheet>
  )
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', gap: 12 },
  action: { flex: 1 },
})
