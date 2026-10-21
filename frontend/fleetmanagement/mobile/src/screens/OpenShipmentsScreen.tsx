import { ShipmentList } from '../components/shipments/ShipmentList'
import type { DeviceCompany } from '../device/DeviceCompany'

/** The Shipments tab: this device's company's shipments, as the web company page's Shipments column. */
export function OpenShipmentsScreen({ company }: { company: DeviceCompany }) {
  return <ShipmentList company={company} />
}
