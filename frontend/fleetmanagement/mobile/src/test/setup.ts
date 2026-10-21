import { act } from '@testing-library/react-native'
import { NativeModules } from 'react-native'

/*
 * Animations (react-native-paper's tick boxes, text fields, menus and spinners, and our
 * placeholders, fades and busy bar) step on timers. Outside act() each step warns, and the
 * warnings bury real ones in the test output - so each step runs inside act(): the frames of
 * JS-driven animations, and the end of native-driven ones (a menu unmounts when its fade ends).
 */
const requestFrame = globalThis.requestAnimationFrame
globalThis.requestAnimationFrame = (callback: FrameRequestCallback) => requestFrame((time) => act(() => callback(time)))

const nativeAnimated = NativeModules.NativeAnimatedModule as { startAnimatingNode: jest.Mock }
nativeAnimated.startAnimatingNode.mockImplementation(
  (_animationId: number, _nodeTag: number, _config: unknown, endCallback: (result: { finished: boolean }) => void) => {
    setTimeout(() => act(() => endCallback({ finished: true })), 16)
  },
)
