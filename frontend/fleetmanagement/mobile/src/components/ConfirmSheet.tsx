import { StyleSheet } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import { ActionButton } from './ActionButton'
import { Sheet } from './Sheet'

interface Props {
  title: string
  message: string
  confirmLabel: string
  /** Shown with a spinner while the action runs: "Removing…". */
  busyLabel: string
  visible: boolean
  onCancel: () => void
  onConfirm: () => Promise<unknown> | void
}

/** Confirmation for a destructive action, as a sheet with Cancel and the red action at the bottom. */
export function ConfirmSheet({ title, message, confirmLabel, busyLabel, visible, onCancel, onConfirm }: Props) {
  const theme = useTheme()
  return (
    <Sheet
      title={title}
      visible={visible}
      onClose={onCancel}
      footer={
        <>
          <Button mode="outlined" style={styles.action} onPress={onCancel}>
            Cancel
          </Button>
          <ActionButton
            style={styles.action}
            mode="contained"
            buttonColor={theme.colors.error}
            textColor={theme.colors.onError}
            label={confirmLabel}
            busyLabel={busyLabel}
            onPress={onConfirm}
          />
        </>
      }
    >
      <Text variant="bodyMedium">{message}</Text>
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
})
