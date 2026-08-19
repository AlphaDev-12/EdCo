import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, Animated } from 'react-native';

let NetInfo = null;
try {
  NetInfo = require('@react-native-community/netinfo');
} catch (e) {
  // Fallback if NetInfo native module is unlinked in pure JS dev environment
}

export function OfflineBanner() {
  const [isConnected, setIsConnected] = useState(true);

  useEffect(() => {
    if (NetInfo && typeof NetInfo.addEventListener === 'function') {
      const unsubscribe = NetInfo.addEventListener(state => {
        setIsConnected(state.isConnected ?? true);
      });
      return () => unsubscribe();
    } else {
      // Fallback periodic ping listener for dev sandbox environments
      const checkConnection = async () => {
        try {
          const res = await fetch('https://www.google.com/generate_204', { method: 'HEAD', cache: 'no-store' });
          setIsConnected(res.ok);
        } catch {
          setIsConnected(false);
        }
      };

      const interval = setInterval(checkConnection, 15000);
      return () => clearInterval(interval);
    }
  }, []);

  if (isConnected) return null;

  return (
    <View style={styles.banner}>
      <Text style={styles.bannerText}>⚠️ Offline Mode — Internet connection lost</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    backgroundColor: '#D97706',
    paddingVertical: 6,
    paddingHorizontal: 12,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 9999,
  },
  bannerText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '600',
  },
});
