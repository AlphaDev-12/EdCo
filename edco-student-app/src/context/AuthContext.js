import React, { createContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { BASE_URL } from '../services/apiService';
import { setUserContext } from '../services/crashReporter';
import { setToken, getToken, setRefreshToken, getRefreshToken, clearTokens } from '../utils/authStorage';

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [userToken, setUserToken] = useState(null);
  const [userData, setUserData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const isLoggedIn = () => {
    return userToken !== null;
  };

  const login = async (email, password) => {
    setIsLoading(true);
    try {
      const res = await fetch(`${BASE_URL}/Auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      });
      
      const data = await res.json();
      if (data.success) {
        setUserToken(data.token);
        setUserData(data.user);
        setUserContext(data.user);
        
        await setToken(data.token);
        if (data.refreshToken) {
          await setRefreshToken(data.refreshToken);
        }
        await AsyncStorage.setItem('userData', JSON.stringify(data.user));
      } else {
        throw new Error(data.message || 'Login failed');
      }
    } catch (e) {
      console.error(`[AuthContext] Login error: ${e}`);
      throw e;
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (fullName, email, password, gradeLevelId) => {
    setIsLoading(true);
    try {
      const res = await fetch(`${BASE_URL}/Auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fullName, email, password, gradeLevelId }),
      });
      
      const data = await res.json();
      if (!res.ok || !data.success) {
        throw new Error(data.errors ? data.errors.join(', ') : 'Registration failed');
      }
      return data;
    } catch (e) {
      console.error(`[AuthContext] Register error: ${e}`);
      throw e;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    setIsLoading(true);
    setUserToken(null);
    setUserData(null);
    await clearTokens();
    await AsyncStorage.removeItem('userData');
    setIsLoading(false);
  };

  const checkLoginStatus = async () => {
    try {
      const token = await getToken();
      const user = await AsyncStorage.getItem('userData');
      if (token) {
        setUserToken(token);
        if (user) {
          const parsedUser = JSON.parse(user);
          setUserData(parsedUser);
          setUserContext(parsedUser);
        }
      }
    } catch (e) {
      console.log(`[AuthContext] Storage read error: ${e}`);
    } finally {
      setIsLoading(false);
    }
  };

  const authenticateWithBiometrics = async () => {
    try {
      let LocalAuthentication = null;
      try {
        LocalAuthentication = require('expo-local-authentication');
      } catch (err) {
        console.log('[AuthContext] LocalAuthentication module not installed or available.');
        return false;
      }

      const hasHardware = await LocalAuthentication.hasHardwareAsync();
      const isEnrolled = await LocalAuthentication.isEnrolledAsync();

      if (!hasHardware || !isEnrolled) {
        console.log('[AuthContext] Biometric hardware or enrollment unavailable.');
        return false;
      }

      const result = await LocalAuthentication.authenticateAsync({
        promptMessage: 'Unlock EdCo StudyPro',
        fallbackLabel: 'Use Password',
      });

      return result.success;
    } catch (e) {
      console.warn('[AuthContext] Biometric authentication error:', e);
      return false;
    }
  };

  const updateSubscriptionStatus = async (isSubscribed) => {
    const newUser = { ...userData, isSubscribed };
    setUserData(newUser);
    await AsyncStorage.setItem('userData', JSON.stringify(newUser));
  };

  const updateUserLocally = async (newUserData) => {
    setUserData(newUserData);
    await AsyncStorage.setItem('userData', JSON.stringify(newUserData));
    if (newUserData.token) {
      setUserToken(newUserData.token);
      await setToken(newUserData.token);
    }
  };

  useEffect(() => {
    checkLoginStatus();
  }, []);

  return (
    <AuthContext.Provider value={{ login, logout, register, updateSubscriptionStatus, updateUserLocally, authenticateWithBiometrics, isLoading, userToken, userData, isLoggedIn }}>
      {children}
    </AuthContext.Provider>
  );
};
