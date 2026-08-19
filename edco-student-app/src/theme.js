export const darkColors = {
  background: '#0F0F1A',
  surface: '#1A1A2E',
  surfaceLight: '#222240',
  primary: '#6C63FF',
  primaryLight: '#8B85FF',
  secondary: '#00D9FF',
  accent: '#FF6B9D',
  success: '#4ADE80',
  warning: '#FBBF24',
  error: '#F87171',
  text: '#FFFFFF',
  textSecondary: '#A0A0C0',
  textMuted: '#6B6B8D',
  border: '#2A2A4A',
  cardGradientStart: '#1E1E3A',
  cardGradientEnd: '#16162E',
  statusBarStyle: 'light-content',
};

export const lightColors = {
  background: '#F4F6F9',
  surface: '#FFFFFF',
  surfaceLight: '#EBF0F7',
  primary: '#6C63FF',
  primaryLight: '#8B85FF',
  secondary: '#0096B1',
  accent: '#E83E8C',
  success: '#10B981',
  warning: '#F59E0B',
  error: '#EF4444',
  text: '#1E293B',
  textSecondary: '#475569',
  textMuted: '#94A3B8',
  border: '#E2E8F0',
  cardGradientStart: '#FFFFFF',
  cardGradientEnd: '#F8FAFC',
  statusBarStyle: 'dark-content',
};

// Default fallback export for backwards compatibility
export const colors = darkColors;

export const fonts = {
  regular: { fontSize: 14, color: darkColors.text },
  medium: { fontSize: 16, color: darkColors.text, fontWeight: '500' },
  bold: { fontSize: 16, color: darkColors.text, fontWeight: '700' },
  title: { fontSize: 22, color: darkColors.text, fontWeight: '800' },
  subtitle: { fontSize: 18, color: darkColors.text, fontWeight: '700' },
  caption: { fontSize: 12, color: darkColors.textSecondary },
};

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
};

