import { Stack, useLocalSearchParams } from 'expo-router'
import { useState } from 'react'
import { DriverDetailScreen } from '../../../../screens/DriverDetailScreen'

export default function DriverRoute() {
  const { driverId } = useLocalSearchParams<{ driverId: string }>()
  const [title, setTitle] = useState('Driver')
  return (
    <>
      <Stack.Screen options={{ title }} />
      <DriverDetailScreen driverId={driverId} onLoaded={setTitle} />
    </>
  )
}
