interface SelectorTruck {
  truckId: string
  truckName: string
  /** The truck's route colour on the map. */
  color: string
}

interface TruckSelectorProps {
  /** Every active truck, in fleet order. */
  trucks: SelectorTruck[]
  /** Trucks switched off in the selector. */
  hiddenTruckIds: ReadonlySet<string>
  onToggleTruck: (truckId: string) => void
  onShowAll: () => void
  onShowNone: () => void
  hideAtOffice: boolean
  onHideAtOfficeChange: (hide: boolean) => void
}

/** Below the fleet map: which active trucks it draws. */
export function TruckSelector({
  trucks,
  hiddenTruckIds,
  onToggleTruck,
  onShowAll,
  onShowNone,
  hideAtOffice,
  onHideAtOfficeChange,
}: TruckSelectorProps) {
  return (
    <div className="truck-selector">
      <label className="check truck-selector__office">
        <input type="checkbox" checked={hideAtOffice} onChange={(event) => onHideAtOfficeChange(event.target.checked)} />
        Hide trucks at the office
      </label>

      {trucks.length > 0 && (
        <div className="truck-selector__chips" role="group" aria-label="Show on map">
          <span className="truck-selector__label">Show on map:</span>
          {trucks.map((truck) => {
            const shown = !hiddenTruckIds.has(truck.truckId)
            return (
              <button
                key={truck.truckId}
                type="button"
                className={`truck-chip${shown ? ' truck-chip--on' : ''}`}
                aria-pressed={shown}
                onClick={() => onToggleTruck(truck.truckId)}
              >
                <span className="truck-chip__swatch" style={{ background: truck.color }} />
                {truck.truckName}
              </button>
            )
          })}
          <span className="truck-selector__all">
            <button type="button" className="link-button" onClick={onShowAll}>
              All
            </button>
            {' · '}
            <button type="button" className="link-button" onClick={onShowNone}>
              None
            </button>
          </span>
        </div>
      )}
    </div>
  )
}
