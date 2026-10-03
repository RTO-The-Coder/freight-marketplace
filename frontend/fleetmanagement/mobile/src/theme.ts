import { MD3DarkTheme, MD3LightTheme, type MD3Theme } from 'react-native-paper'

// Colours mirror the web app's design tokens (web/src/index.css), light and dark.
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
