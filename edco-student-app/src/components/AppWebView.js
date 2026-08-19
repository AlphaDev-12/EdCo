import React from 'react';
import { Platform, View, StyleSheet } from 'react-native';

let NativeWebView = null;
if (Platform.OS !== 'web') {
  try {
    NativeWebView = require('react-native-webview').WebView;
  } catch (e) {
    console.warn('[AppWebView] Native WebView not available:', e);
  }
}

export function AppWebView({ source, style, injectedJavaScript, ...rest }) {
  if (Platform.OS === 'web' || !NativeWebView) {
    const htmlContent = source?.html;
    const uriContent = source?.uri;

    return (
      <View style={[styles.webFrameWrapper, style]}>
        <iframe
          srcDoc={htmlContent || undefined}
          src={uriContent || undefined}
          style={{
            width: '100%',
            height: '100%',
            border: 'none',
            backgroundColor: 'transparent',
          }}
          title="AppWebView"
          sandbox="allow-scripts allow-same-origin allow-forms allow-popups"
        />
      </View>
    );
  }

  return (
    <NativeWebView
      source={source}
      style={style}
      injectedJavaScript={injectedJavaScript}
      {...rest}
    />
  );
}

const styles = StyleSheet.create({
  webFrameWrapper: {
    flex: 1,
    width: '100%',
    height: '100%',
    overflow: 'hidden',
  },
});
