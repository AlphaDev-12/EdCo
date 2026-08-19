/**
 * Expo Push Notification Service
 * 
 * Manages push notification permission requests, device token generation,
 * and push payload handling for assignment and AI grading notifications.
 */

import { Platform } from 'react-native';

let NotificationsModule = null;

try {
  NotificationsModule = require('expo-notifications');
  
  // Configure default notification handler
  if (NotificationsModule && NotificationsModule.setNotificationHandler) {
    NotificationsModule.setNotificationHandler({
      handleNotification: async () => ({
        shouldShowAlert: true,
        shouldPlaySound: true,
        shouldSetBadge: true,
      }),
    });
  }
} catch (e) {
  console.warn('[NotificationService] expo-notifications module not installed or unavailable:', e.message);
}

/**
 * Request notification permissions and return the Expo Push Token.
 */
export const registerForPushNotificationsAsync = async () => {
  if (!NotificationsModule) {
    console.log('[NotificationService] Push notifications bypassed (SDK unavailable).');
    return null;
  }

  try {
    const { status: existingStatus } = await NotificationsModule.getPermissionsAsync();
    let finalStatus = existingStatus;

    if (existingStatus !== 'granted') {
      const { status } = await NotificationsModule.requestPermissionsAsync();
      finalStatus = status;
    }

    if (finalStatus !== 'granted') {
      console.warn('[NotificationService] Notification permission was not granted by user.');
      return null;
    }

    const tokenData = await NotificationsModule.getExpoPushTokenAsync();
    const token = tokenData.data;
    console.log('[NotificationService] Push token retrieved:', token);

    if (Platform.OS === 'android') {
      NotificationsModule.setNotificationChannelAsync('default', {
        name: 'default',
        importance: NotificationsModule.AndroidImportance.MAX,
        vibrationPattern: [0, 250, 250, 250],
        lightColor: '#3B82F6',
      });
    }

    return token;
  } catch (error) {
    console.error('[NotificationService] Error registering push notifications:', error);
    return null;
  }
};

/**
 * Add listener for incoming notifications when app is foregrounded.
 */
export const addNotificationReceivedListener = (callback) => {
  if (NotificationsModule && NotificationsModule.addNotificationReceivedListener) {
    return NotificationsModule.addNotificationReceivedListener(callback);
  }
  return { remove: () => {} };
};

/**
 * Add listener for notification responses (when user taps on a notification).
 */
export const addNotificationResponseReceivedListener = (callback) => {
  if (NotificationsModule && NotificationsModule.addNotificationResponseReceivedListener) {
    return NotificationsModule.addNotificationResponseReceivedListener(callback);
  }
  return { remove: () => {} };
};
