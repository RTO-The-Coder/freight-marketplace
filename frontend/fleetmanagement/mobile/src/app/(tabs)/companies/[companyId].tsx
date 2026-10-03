import { router, Stack, useLocalSearchParams } from 'expo-router'
import { useState } from 'react'
import { CompanyDetailScreen } from '../../../screens/CompanyDetailScreen'

export default function CompanyRoute() {
  const { companyId } = useLocalSearchParams<{ companyId: string }>()
  const [title, setTitle] = useState('Company')
  return (
    <>
      <Stack.Screen options={{ title }} />
      <CompanyDetailScreen
        companyId={companyId}
        onLoaded={setTitle}
        onSelectTruck={(truckId) => router.push(`/companies/truck/${truckId}`)}
      />
    </>
  )
}
