import { StyleSheet, View } from 'react-native'
import { Text } from 'react-native-paper'
import { companyInitials, companyLogoHues } from '@freight/fleetmanagement-core'

/** Same monogram as the web logo (colour and initials from core); a flat colour instead of the web gradient. */
export function CompanyLogo({ name, size = 40 }: { name: string; size?: number }) {
  const { hue } = companyLogoHues(name)
  return (
    <View
      style={[styles.tile, { width: size, height: size, backgroundColor: `hsl(${hue}, 68%, 52%)` }]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      <Text style={[styles.initials, { fontSize: size * 0.38 }]}>{companyInitials(name) || '?'}</Text>
    </View>
  )
}

const styles = StyleSheet.create({
  tile: { borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
  initials: { color: '#ffffff', fontWeight: '700' },
})
