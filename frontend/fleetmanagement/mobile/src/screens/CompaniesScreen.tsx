import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { FlatList, RefreshControl } from 'react-native'
import { List } from 'react-native-paper'
import { truckingCompaniesApi } from '@freight/fleetmanagement-core'
import { CompanyLogo } from '../components/CompanyLogo'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { usePullToRefresh } from '../components/usePullToRefresh'

export function CompaniesScreen({ onSelect }: { onSelect: (companyId: string) => void }) {
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(
    () =>
      truckingCompaniesApi
        .getTruckingCompanies()
        .then((r) => {
          setCompanies(r.companies)
          setError(null)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load trucking companies.'))),
    [],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load])

  if (error && !companies) return <ErrorState message={error} />
  if (!companies) return <LoadingState />

  return (
    <FlatList
      testID="companies-list"
      data={companies}
      keyExtractor={(c) => c.companyId}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}
      ListEmptyComponent={<EmptyState message="No trucking companies have been provisioned yet." />}
      renderItem={({ item }) => (
        <List.Item
          title={item.name}
          description="Carrier"
          left={() => <CompanyLogo name={item.name} />}
          right={(props) => <List.Icon {...props} icon="chevron-right" />}
          onPress={() => onSelect(item.companyId)}
        />
      )}
    />
  )
}
