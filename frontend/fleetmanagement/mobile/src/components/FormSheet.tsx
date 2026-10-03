import type { ReactNode } from 'react'
import { KeyboardAvoidingView, Modal, ScrollView, StyleSheet, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { Button, Text, useTheme } from 'react-native-paper'

interface Props {
  title: string
  visible: boolean
  onClose: () => void
  confirmLabel: string
  busyLabel: string
  busy: boolean
  canConfirm: boolean
  onConfirm: () => void
  error: string | null
  children: ReactNode
}

/**
 * A full-screen form. Title on top; Cancel and confirm in a fixed bottom bar (thumb
 * reach) that stays above the keyboard. Android Back cancels.
 */
export function FormSheet({
  title,
  visible,
  onClose,
  confirmLabel,
  busyLabel,
  busy,
  canConfirm,
  onConfirm,
  error,
  children,
}: Props) {
  const theme = useTheme()
  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <SafeAreaView style={[styles.screen, { backgroundColor: theme.colors.background }]}>
        <KeyboardAvoidingView style={styles.screen} behavior="padding">
          <Text variant="headlineSmall" style={styles.title} accessibilityRole="header">
            {title}
          </Text>

          <ScrollView contentContainerStyle={styles.body} keyboardShouldPersistTaps="handled">
            {children}
            {error && (
              <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
                {error}
              </Text>
            )}
          </ScrollView>

          <View style={[styles.actions, { borderTopColor: theme.colors.outline }]}>
            <Button mode="outlined" style={styles.action} onPress={onClose}>
              Cancel
            </Button>
            <Button mode="contained" style={styles.action} onPress={onConfirm} disabled={!canConfirm || busy}>
              {busy ? busyLabel : confirmLabel}
            </Button>
          </View>
        </KeyboardAvoidingView>
      </SafeAreaView>
    </Modal>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  title: { paddingHorizontal: 16, paddingTop: 16, paddingBottom: 8 },
  body: { padding: 16, gap: 16 },
  actions: { flexDirection: 'row', gap: 12, padding: 16, borderTopWidth: StyleSheet.hairlineWidth },
  action: { flex: 1 },
})
