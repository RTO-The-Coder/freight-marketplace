import { Redirect } from 'expo-router'

/** The app opens on the Fleet tab: this device's company. */
export default function Index() {
  return <Redirect href="/fleet" />
}
