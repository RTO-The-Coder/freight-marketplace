import type { ReactNode } from 'react'
import { Modal, Pressable, StyleSheet, View } from 'react-native'
import { useSafeAreaInsets } from 'react-native-safe-area-context'
import { Text, useTheme } from 'react-native-paper'

interface Props {
  title: string
  visible: boolean
  onClose: () => void
  children: ReactNode
}

/** A modal sheet anchored to the bottom of the screen; tapping outside or Android Back closes it. */
export function BottomSheet({ title, visible, onClose, children }: Props) {
  const theme = useTheme()
  const insets = useSafeAreaInsets()
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose} accessibilityLabel="Close" />
      <View
        style={[
          styles.sheet,
          { backgroundColor: theme.colors.surface, paddingBottom: 16 + insets.bottom },
        ]}
      >
        <View style={[styles.handle, { backgroundColor: theme.colors.outline }]} />
        <Text variant="titleMedium" accessibilityRole="header">
          {title}
        </Text>
        {children}
      </View>
    </Modal>
  )
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, backgroundColor: 'rgba(0, 0, 0, 0.32)' },
  sheet: { borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingHorizontal: 16, paddingTop: 8, gap: 12 },
  handle: { alignSelf: 'center', width: 32, height: 4, borderRadius: 2, marginBottom: 4 },
})
