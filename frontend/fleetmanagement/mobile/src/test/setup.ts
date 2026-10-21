import { act, configure } from '@testing-library/react-native'
import { NativeModules, VirtualizedList } from 'react-native'

/*
 * CI machines are slow: give every findBy/waitFor 5 s instead of 1 s, and each test 20 s.
 * A passing step still moves on the moment its condition holds.
 */
configure({ asyncUtilTimeout: 5000 })
jest.setTimeout(20000)

/*
 * Animations (react-native-paper's tick boxes, text fields, menus and spinners, and our
 * placeholders, fades and busy bar) step on timers. Outside act() each step warns, and the
 * warnings bury real ones in the test output - so each step runs inside act(): the frames of
 * JS-driven animations, and the end of native-driven ones (a menu unmounts when its fade ends).
 */
const requestFrame = globalThis.requestAnimationFrame
globalThis.requestAnimationFrame = (callback: FrameRequestCallback) => requestFrame((time) => act(() => callback(time)))

// Native-driven ones end at once, not on a timer: every Paper menu runs a hidden close animation
// when the screen mounts, and on a busy machine a timer could end it after a test had opened the
// menu - closing it under the tap.
const nativeAnimated = NativeModules.NativeAnimatedModule as { startAnimatingNode: jest.Mock }
nativeAnimated.startAnimatingNode.mockImplementation(
  (_animationId: number, _nodeTag: number, _config: unknown, endCallback: (result: { finished: boolean }) => void) => {
    queueMicrotask(() => act(() => endCallback({ finished: true })))
  },
)

/*
 * A list renders more rows in a batch 50 ms after an update; that batch runs inside act() too.
 * (It replaces the list's own timer, so it reaches into the list's internals.)
 */
type ListInternals = {
  _scheduleCellsToRenderUpdate: () => void
  _updateCellsToRender: () => void
  _updateCellsToRenderTimeoutID: ReturnType<typeof setTimeout> | null
  props: { updateCellsBatchingPeriod?: number | null }
}
const list = VirtualizedList.prototype as unknown as ListInternals
const scheduleBatch = list._scheduleCellsToRenderUpdate
list._scheduleCellsToRenderUpdate = function (this: ListInternals) {
  const alreadyPending = this._updateCellsToRenderTimeoutID != null
  scheduleBatch.call(this)
  if (alreadyPending || this._updateCellsToRenderTimeoutID == null) return
  clearTimeout(this._updateCellsToRenderTimeoutID)
  this._updateCellsToRenderTimeoutID = setTimeout(() => {
    this._updateCellsToRenderTimeoutID = null
    act(() => this._updateCellsToRender())
  }, this.props.updateCellsBatchingPeriod ?? 50)
}
