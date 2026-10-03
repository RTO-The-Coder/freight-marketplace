import { useDeviceCompany } from '../../../device/DeviceCompany'
import { OpenShipmentsScreen } from '../../../screens/OpenShipmentsScreen'

export default function ShipmentsRoute() {
  return <OpenShipmentsScreen company={useDeviceCompany()} />
}
