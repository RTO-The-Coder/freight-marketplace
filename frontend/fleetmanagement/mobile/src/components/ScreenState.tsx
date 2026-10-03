import { StyleSheet, View } from 'react-native'
import { ActivityIndicator, Text, useTheme } from 'react-native-paper'

export function LoadingState() {
  return (
    <View style={styles.center}>
      <ActivityIndicator accessibilityLabel="Loading" />
    </View>
  )
}

export function ErrorState({ message }: { message: string }) {
  const theme = useTheme()
  return (
    <View style={styles.center}>
      <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
        {message}
      </Text>
    </View>
  )
}

export function EmptyState({ message }: { message: string }) {
  const theme = useTheme()
  return (
    <View style={styles.center}>
      <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
        {message}
      </Text>
    </View>
  )
}

export function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

const styles = StyleSheet.create({
  center: { padding: 24, alignItems: 'center', justifyContent: 'center' },
})
