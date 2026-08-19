import React from 'react';
import { View, StyleSheet, Platform, useWindowDimensions } from 'react-native';
import { useTheme } from '../context/ThemeContext';

export function WebContainer({ children }) {
  const { colors, isDark } = useTheme();
  const { width } = useWindowDimensions();

  if (Platform.OS !== 'web') {
    return <View style={{ flex: 1 }}>{children}</View>;
  }

  const isWideWeb = width >= 768;

  return (
    <View style={[
      styles.outerWebContainer,
      { backgroundColor: isDark ? '#0F172A' : '#F1F5F9' }
    ]}>
      <View style={[
        styles.innerWebFrame,
        isWideWeb && styles.wideWebFrame,
        {
          backgroundColor: colors.background,
          borderColor: isDark ? '#334155' : '#E2E8F0',
        }
      ]}>
        {children}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  outerWebContainer: {
    flex: 1,
    width: '100%',
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
  },
  innerWebFrame: {
    flex: 1,
    width: '100%',
    maxWidth: '100%',
    height: '100%',
    overflow: 'hidden',
  },
  wideWebFrame: {
    maxWidth: 1024,
    maxHeight: '100%',
    borderLeftWidth: 1,
    borderRightWidth: 1,
    boxShadow: '0 10px 30px rgba(0, 0, 0, 0.15)',
  },
});
