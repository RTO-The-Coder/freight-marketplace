import { router } from 'expo-router'
import { CompaniesScreen } from '../../../screens/CompaniesScreen'

export default function CompaniesRoute() {
  return <CompaniesScreen onSelect={(companyId) => router.push(`/companies/${companyId}`)} />
}
