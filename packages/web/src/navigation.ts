export type View =
  | { section: 'companies'; companyId: null }
  | { section: 'companies'; companyId: string }
  | { section: 'trucks'; truckId: null }
  | { section: 'trucks'; truckId: string }
  | { section: 'drivers'; driverId: null }
  | { section: 'drivers'; driverId: string }
