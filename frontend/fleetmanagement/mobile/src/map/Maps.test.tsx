import { fireEvent, screen, waitFor, within } from '@testing-library/react-native'
import { installFakeApi, renderWithApp } from '../test/helpers'
import { CompanyDetailScreen } from '../screens/CompanyDetailScreen'
import { OpenShipmentsScreen } from '../screens/OpenShipmentsScreen'
import { TruckDetailScreen } from '../screens/TruckDetailScreen'

const geometryKey = (fromLat: number, fromLng: number, toLat: number, toLng: number) =>
  `GET /routing/geometry?fromLat=${fromLat}&fromLng=${fromLng}&toLat=${toLat}&toLng=${toLng}`

const stop = (stopId: string, kind: string, status: string, sequence: number, latitude: number, longitude: number) => ({
  stopId,
  shipmentId: 'sh1',
  kind,
  status,
  sequence,
  latitude,
  longitude,
  incomingLegDistanceKm: 10,
  incomingLegTimeTick: 3,
  reachedAt: status === 'Reached' ? '2026-08-01T07:00:00Z' : null,
})

const truckDetail = (truckId: string, truckName: string, stops: unknown[], extra = {}) => ({
  truckId,
  truckName,
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: true,
  status: stops.length ? 'EnRoute' : 'AtOffice',
  truckingCompanyId: 'c1',
  driverConfigurationType: null,
  primaryDriver: null,
  secondaryDriver: null,
  stops,
  ...extra,
})

const position = (truckId: string, latitude: number, longitude: number) => ({
  truckId,
  tripId: `trip-${truckId}`,
  latitude,
  longitude,
  headingToStopId: null,
  legProgressFraction: 0.25,
})

/** The line features the map would draw, as [lng, lat] coordinate lists. */
function drawnLines(root = screen) {
  return root.getByTestId('maplibre-source-route-lines').props.data.features as Array<{
    geometry: { coordinates: number[][] }
    properties: { dashed: boolean; color: string }
  }>
}

describe('trip map on the truck screen', () => {
  const stops = [stop('s1', 'Pickup', 'Pending', 10, 51.1, 17.1), stop('s2', 'Delivery', 'Pending', 20, 52.2, 21.2)]

  it('draws road legs from the truck’s position, and opens full screen with the stops on tap', async () => {
    installFakeApi({
      'GET /trucks/t1': truckDetail('t1', 'FL-07', stops),
      'GET /trucks/t1/position': position('t1', 50.5, 16.5),
      [geometryKey(50.5, 16.5, 51.1, 17.1)]: { distanceKm: 1, timeTicks: 1, path: [{ lat: 50.5, lng: 16.5 }, { lat: 50.8, lng: 16.9 }, { lat: 51.1, lng: 17.1 }] },
      [geometryKey(51.1, 17.1, 52.2, 21.2)]: { distanceKm: 1, timeTicks: 1, path: [{ lat: 51.1, lng: 17.1 }, { lat: 52.2, lng: 21.2 }] },
    })
    renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)

    await waitFor(() => expect(drawnLines()).toHaveLength(2))
    // GeoJSON order is [lng, lat]; the first leg follows the road via the middle point.
    expect(drawnLines()[0].geometry.coordinates).toEqual([
      [16.5, 50.5],
      [16.9, 50.8],
      [17.1, 51.1],
    ])
    expect(drawnLines().every((l) => !l.properties.dashed)).toBe(true)
    expect(screen.getByLabelText('25% along current leg')).toBeOnTheScreen()

    fireEvent.press(screen.getByLabelText('Open trip map full screen'))
    expect(await screen.findByText('Trip map')).toBeOnTheScreen()
    fireEvent(screen.getAllByTestId('maplibre-source-route-stops').at(-1)!, 'press', {
      nativeEvent: { features: [{ properties: { label: 'Delivery · pending' } }] },
    })
    expect(await screen.findByText('Delivery · pending')).toBeOnTheScreen()

    fireEvent.press(screen.getByLabelText('Close map'))
    await waitFor(() => expect(screen.queryByText('Trip map')).toBeNull())
  })

  it('falls back to dashed straight lines and says so when routing is down', async () => {
    installFakeApi({
      'GET /trucks/t1': truckDetail('t1', 'FL-07', [stop('s1', 'Pickup', 'Pending', 10, 53.3, 18.3)]),
      'GET /trucks/t1/position': position('t1', 53.9, 18.9),
    })
    renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)

    expect(
      await screen.findByText('Some legs shown as straight lines — routing service was unavailable.'),
    ).toBeOnTheScreen()
    expect(drawnLines().map((l) => l.properties.dashed)).toEqual([true])
  })

  it('has no map without a trip', async () => {
    installFakeApi({ 'GET /trucks/t1': truckDetail('t1', 'FL-07', []), 'GET /trucks/t1/position': position('t1', 50, 16) })
    renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
    await screen.findByText('No active trip — the truck is at its company office.')
    expect(screen.queryByTestId('maplibre-map')).toBeNull()
  })
})

describe('fleet map on the company screen', () => {
  function fleetRoutes(trucks: ReturnType<typeof truckDetail>[]) {
    const routes: Record<string, unknown> = {
      'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight', officeLatitude: 49.9, officeLongitude: 15.9 },
      'GET /trucks?truckingCompanyId=c1': {
        trucks: trucks.map((t) => ({ ...t, hasDriverAssignment: false })),
      },
    }
    for (const t of trucks) {
      routes[`GET /trucks/${t.truckId}`] = t
      routes[`GET /trucks/${t.truckId}/position`] = position(t.truckId, 50.1, 16.1)
    }
    return routes
  }

  it('shows each running truck’s route in its own colour, and a truck tap (full screen) opens it', async () => {
    installFakeApi(
      fleetRoutes([
        truckDetail('t1', 'FL-01', [stop('a', 'Pickup', 'Pending', 1, 54.4, 18.4)]),
        truckDetail('t2', 'FL-02', [stop('b', 'Pickup', 'Pending', 1, 54.5, 18.5)]),
        truckDetail('t3', 'FL-03', []),
      ]),
    )
    const onSelectTruck = jest.fn()
    renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={onSelectTruck} />)

    await waitFor(() => expect(drawnLines()).toHaveLength(2))
    const colors = drawnLines().map((l) => l.properties.color)
    expect(new Set(colors).size).toBe(2)
    expect(screen.getByLabelText('Company office')).toBeOnTheScreen()
    expect(screen.queryByTestId('maplibre-marker-truck-t3')).toBeNull()

    fireEvent.press(screen.getByLabelText('Open fleet map full screen'))
    expect(await screen.findByLabelText('Close map')).toBeOnTheScreen()
    fireEvent.press(screen.getAllByTestId('maplibre-marker-truck-t2').at(-1)!)
    expect(onSelectTruck).toHaveBeenCalledWith('t2')
    await waitFor(() => expect(screen.queryByLabelText('Close map')).toBeNull())
  })

  it('says so when no truck is on a trip', async () => {
    installFakeApi(fleetRoutes([truckDetail('t1', 'FL-01', [])]))
    renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} />)
    expect(
      await screen.findByText('No trucks are on a trip right now. Assign a shipment to see its route here.'),
    ).toBeOnTheScreen()
    expect(screen.queryByTestId('maplibre-map')).toBeNull()
  })
})

it('shipment card: the expanded card shows the pickup → delivery route', async () => {
  installFakeApi({
    'GET /shipments/pending': {
      shipments: [
        {
          shipmentId: 'sh1',
          truckingCompanyId: null,
          pickupLatitude: 55.1,
          pickupLongitude: 19.1,
          deliveryLatitude: 55.6,
          deliveryLongitude: 19.6,
          loadWeightKg: 1000,
          loadVolumeCubicMeters: 5,
          requiredTruckType: 'Flatbed',
          pickupWindowEarliest: '2026-08-01T08:00:00Z',
          pickupWindowLatest: '2026-08-01T12:00:00Z',
          deliveryWindowEarliest: '2026-08-03T08:00:00Z',
          deliveryWindowLatest: '2026-08-03T18:00:00Z',
          offerDeadline: '2026-08-01T06:00:00Z',
          status: 'Pending',
        },
      ],
    },
    [geometryKey(55.1, 19.1, 55.6, 19.6)]: { distanceKm: 1, timeTicks: 1, path: [{ lat: 55.1, lng: 19.1 }, { lat: 55.6, lng: 19.6 }] },
  })
  renderWithApp(<OpenShipmentsScreen />)
  expect(screen.queryByTestId('maplibre-map')).toBeNull()

  fireEvent.press(await screen.findByText('Flatbed'))
  await waitFor(() => expect(drawnLines()).toHaveLength(1))
  const dots = within(screen.getByTestId('maplibre-map')).getByTestId('maplibre-source-route-stops').props.data.features
  expect(dots.map((f: { properties: { label: string } }) => f.properties.label)).toEqual(['Pickup', 'Delivery'])
  expect(screen.getByLabelText('Open shipment route full screen')).toBeOnTheScreen()
})
