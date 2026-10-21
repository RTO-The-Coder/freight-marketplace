import { Pressable, StyleSheet, View } from 'react-native'
import { Icon, Text, useTheme } from 'react-native-paper'
import { Sheet } from '../Sheet'

interface Props {
  companyName: string
  visible: boolean
  onClose: () => void
  onAddTruck: () => void
  onAddDriver: () => void
}

/** The ＋ Add button's sheet: add a truck or a driver to the company. */
export function AddSheet({ companyName, visible, onClose, onAddTruck, onAddDriver }: Props) {
  return (
    <Sheet title={`Add to ${companyName}`} visible={visible} onClose={onClose}>
      <Choice icon="truck-plus-outline" title="Add a truck" detail="Name, type and size" onPress={onAddTruck} />
      <Choice icon="account-plus-outline" title="Add a driver" detail="Name and driving rules" onPress={onAddDriver} />
    </Sheet>
  )
}

function Choice({ icon, title, detail, onPress }: { icon: string; title: string; detail: string; onPress: () => void }) {
  const theme = useTheme()
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      style={({ pressed }) => [
        styles.choice,
        { backgroundColor: pressed ? theme.colors.surfaceVariant : theme.colors.background, borderColor: theme.colors.outline },
      ]}
    >
      <Icon source={icon} size={24} color={theme.colors.primary} />
      <View style={styles.text}>
        <Text variant="titleSmall">{title}</Text>
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          {detail}
        </Text>
      </View>
      <Icon source="chevron-right" size={20} color={theme.colors.onSurfaceVariant} />
    </Pressable>
  )
}

const styles = StyleSheet.create({
  choice: { flexDirection: 'row', alignItems: 'center', gap: 12, borderRadius: 14, borderWidth: StyleSheet.hairlineWidth, padding: 14 },
  text: { flex: 1, gap: 2 },
})
