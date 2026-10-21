import type { ReactNode } from 'react'
import { KeyboardAvoidingView, Modal, Pressable, ScrollView, StyleSheet, View } from 'react-native'
import { useSafeAreaInsets } from 'react-native-safe-area-context'
import { Text, useTheme } from 'react-native-paper'
import { useAppColors } from '../theme'

interface Props {
  title: string
  /** The line under the title: whose or what this is about ("Rheinfracht Logistik AG"). */
  context?: ReactNode
  visible: boolean
  onClose: () => void
  /** The buttons, fixed at the bottom while the body scrolls (Cancel first, the action last). */
  footer?: ReactNode
  /** A result line kept in view just above the buttons (the web panel's status): "This shipment fits". */
  status?: ReactNode
  /** The web's side panels (Assign a shipment, Driver details): a sheet almost the screen's height. */
  tall?: boolean
  children: ReactNode
}

/**
 * The phone's version of a web popup: a bottom sheet with the title and its context, a body
 * that scrolls, and the buttons fixed at the bottom above the keyboard. Tapping outside or
 * Android Back closes it.
 */
export function Sheet({ title, context, visible, onClose, footer, status, tall = false, children }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const insets = useSafeAreaInsets()
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <KeyboardAvoidingView style={styles.fill} behavior="padding">
        <Pressable style={[styles.fill, { backgroundColor: colors.scrim }]} onPress={onClose} accessibilityLabel="Close" />
        <View
          style={[styles.sheet, tall ? styles.tall : styles.fit, { backgroundColor: theme.colors.surface }]}
          accessibilityViewIsModal
        >
          <View style={[styles.grab, { backgroundColor: colors.borderStrong }]} />
          <View style={[styles.head, { borderBottomColor: theme.colors.outline }]}>
            <Text variant="titleMedium" accessibilityRole="header">
              {title}
            </Text>
            {context ? (
              <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                {context}
              </Text>
            ) : null}
          </View>
          <ScrollView
            // A fitting sheet's body must be allowed to shrink: otherwise it grows to its full
            // content, the sheet's height limit cuts the bottom off, and it can't be scrolled to.
            style={tall ? styles.fill : styles.shrink}
            contentContainerStyle={[styles.body, !footer && { paddingBottom: 16 + insets.bottom }]}
            keyboardShouldPersistTaps="handled"
          >
            {children}
          </ScrollView>
          {status ? <View style={[styles.status, { borderTopColor: theme.colors.outline }]}>{status}</View> : null}
          {footer ? (
            <View style={[styles.foot, { borderTopColor: theme.colors.outline, paddingBottom: 14 + insets.bottom }]}>
              {footer}
            </View>
          ) : null}
        </View>
      </KeyboardAvoidingView>
    </Modal>
  )
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  shrink: { flexGrow: 0, flexShrink: 1 },
  sheet: { borderTopLeftRadius: 22, borderTopRightRadius: 22 },
  fit: { maxHeight: '88%' },
  tall: { height: '92%' },
  grab: { alignSelf: 'center', width: 38, height: 4, borderRadius: 2, marginTop: 8, marginBottom: 2 },
  head: { paddingHorizontal: 16, paddingTop: 6, paddingBottom: 10, gap: 2, borderBottomWidth: StyleSheet.hairlineWidth },
  body: { paddingHorizontal: 16, paddingVertical: 12, gap: 12 },
  status: { paddingHorizontal: 16, paddingVertical: 8, borderTopWidth: StyleSheet.hairlineWidth },
  foot: { flexDirection: 'row', gap: 8, paddingHorizontal: 16, paddingTop: 10, borderTopWidth: StyleSheet.hairlineWidth },
})
