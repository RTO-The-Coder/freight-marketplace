import { router, Stack, useLocalSearchParams } from 'expo-router'
import { useState } from 'react'
import { TruckDetailScreen } from '../../../../screens/TruckDetailScreen'

export default function TruckRoute() {
  const { truckId } = useLocalSearchParams<{ truckId: string }>()
  const [title, setTitle] = useState('Truck')
  return (
    <>
      <Stack.Screen options={{ title }} />
      <TruckDetailScreen
        truckId={truckId}
        onLoaded={setTitle}
        onOpenTracking={(id) => router.navigate(`/tracking?truckId=${id}`)}
      />
    </>
  )
}
