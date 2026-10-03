import { Redirect } from 'expo-router'

/** The app opens on the Companies tab, like the web app opens on the Companies list. */
export default function Index() {
  return <Redirect href="/companies" />
}
