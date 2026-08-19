import { Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';

let SecureStore = null;
if (Platform.OS !== 'web') {
  try {
    SecureStore = require('expo-secure-store');
  } catch (e) {
    console.warn('[AuthStorage] expo-secure-store unavailable, falling back to AsyncStorage.');
  }
}

const USER_TOKEN_KEY = 'userToken';
const REFRESH_TOKEN_KEY = 'refreshToken';

export async function setToken(token) {
  try {
    if (SecureStore) {
      await SecureStore.setItemAsync(USER_TOKEN_KEY, token);
    } else {
      await AsyncStorage.setItem(USER_TOKEN_KEY, token);
    }
  } catch (e) {
    console.error('[AuthStorage] Error saving access token:', e);
    await AsyncStorage.setItem(USER_TOKEN_KEY, token);
  }
}

export async function getToken() {
  try {
    if (SecureStore) {
      const token = await SecureStore.getItemAsync(USER_TOKEN_KEY);
      if (token) return token;
    }
    return await AsyncStorage.getItem(USER_TOKEN_KEY);
  } catch (e) {
    console.error('[AuthStorage] Error reading access token:', e);
    return await AsyncStorage.getItem(USER_TOKEN_KEY);
  }
}

export async function setRefreshToken(refreshToken) {
  try {
    if (SecureStore) {
      await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken);
    } else {
      await AsyncStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }
  } catch (e) {
    console.error('[AuthStorage] Error saving refresh token:', e);
    await AsyncStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }
}

export async function getRefreshToken() {
  try {
    if (SecureStore) {
      const token = await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
      if (token) return token;
    }
    return await AsyncStorage.getItem(REFRESH_TOKEN_KEY);
  } catch (e) {
    console.error('[AuthStorage] Error reading refresh token:', e);
    return await AsyncStorage.getItem(REFRESH_TOKEN_KEY);
  }
}

export async function clearTokens() {
  try {
    if (SecureStore) {
      await SecureStore.deleteItemAsync(USER_TOKEN_KEY);
      await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
    }
    await AsyncStorage.removeItem(USER_TOKEN_KEY);
    await AsyncStorage.removeItem(REFRESH_TOKEN_KEY);
  } catch (e) {
    console.error('[AuthStorage] Error clearing auth tokens:', e);
    await AsyncStorage.removeItem(USER_TOKEN_KEY);
    await AsyncStorage.removeItem(REFRESH_TOKEN_KEY);
  }
}
