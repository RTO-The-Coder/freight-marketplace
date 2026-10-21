import { MD3DarkTheme, MD3LightTheme, type MD3Theme } from 'react-native-paper'
import { useTheme } from 'react-native-paper'

// Colours mirror the web app's design tokens (@freight/web-ui styles/tokens.css), light and dark.
// Container colours must be opaque: on Android a see-through background on an elevated view
// (FAB, cards) lets the shadow render through it. They are the web's translucent tint
// pre-mixed over the page background, so they look the same.

export const lightTheme: MD3Theme = {
  ...MD3LightTheme,
  colors: {
    ...MD3LightTheme.colors,
    primary: '#7c3aed',
    onPrimary: '#ffffff',
    // rgba(124, 58, 237, 0.09) over #f7f7f9
    primaryContainer: '#ece6f8',
    background: '#f7f7f9',
    surface: '#ffffff',
    surfaceVariant: '#f1f1f4',
    onSurface: '#1a1620',
    onSurfaceVariant: '#6b6577',
    outline: '#e4e4e9',
    error: '#dc2626',
  },
}

export const darkTheme: MD3Theme = {
  ...MD3DarkTheme,
  colors: {
    ...MD3DarkTheme.colors,
    primary: '#a78bfa',
    onPrimary: '#1a1620',
    // rgba(167, 139, 250, 0.14) over #0f0e13
    primaryContainer: '#241f33',
    background: '#0f0e13',
    surface: '#17161d',
    surfaceVariant: '#1e1d26',
    onSurface: '#f3f2f6',
    onSurfaceVariant: '#a49eb2',
    outline: '#2a2833',
    error: '#f87171',
  },
}

/**
 * The web tokens Paper's theme has no slot for: subtle text, the strong border, the accent
 * tint, the popup scrim and the status hues of pills and dots. Translucent ones are only for
 * flat views (pills, highlighted rows), never for an elevated container.
 */
export interface AppColors {
  textSubtle: string
  borderStrong: string
  accentBg: string
  accentBorder: string
  /** The bold accent of the main button (＋ Add) and the text on it. */
  accentStrong: string
  onAccentStrong: string
  dangerBg: string
  scrim: string
  running: string
  runningBg: string
  idle: string
  idleBg: string
  office: string
  officeBg: string
  neutral: string
  neutralBg: string
}

const lightColors: AppColors = {
  textSubtle: '#9a94a6',
  borderStrong: '#d3d3da',
  accentBg: 'rgba(124, 58, 237, 0.09)',
  accentBorder: 'rgba(124, 58, 237, 0.28)',
  accentStrong: '#7c3aed',
  onAccentStrong: '#ffffff',
  dangerBg: 'rgba(220, 38, 38, 0.08)',
  scrim: 'rgba(20, 16, 30, 0.32)',
  running: '#2563eb',
  runningBg: 'rgba(37, 99, 235, 0.1)',
  idle: '#b45309',
  idleBg: 'rgba(180, 83, 9, 0.1)',
  office: '#15803d',
  officeBg: 'rgba(21, 128, 61, 0.1)',
  neutral: '#6b6577',
  neutralBg: 'rgba(107, 101, 119, 0.1)',
}

const darkColors: AppColors = {
  textSubtle: '#6f6a7d',
  borderStrong: '#3a3745',
  accentBg: 'rgba(167, 139, 250, 0.14)',
  accentBorder: 'rgba(167, 139, 250, 0.36)',
  accentStrong: '#8b5cf6',
  onAccentStrong: '#ffffff',
  dangerBg: 'rgba(248, 113, 113, 0.12)',
  scrim: 'rgba(0, 0, 0, 0.5)',
  running: '#60a5fa',
  runningBg: 'rgba(96, 165, 250, 0.15)',
  idle: '#fbbf24',
  idleBg: 'rgba(251, 191, 36, 0.15)',
  office: '#4ade80',
  officeBg: 'rgba(74, 222, 128, 0.15)',
  neutral: '#a49eb2',
  neutralBg: 'rgba(164, 158, 178, 0.15)',
}

/** The extra web tokens for the current light or dark theme. */
export function useAppColors(): AppColors {
  return useTheme().dark ? darkColors : lightColors
}
