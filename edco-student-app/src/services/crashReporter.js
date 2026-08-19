import { SENTRY_DSN, IS_PRODUCTION } from '../config/env';

/**
 * Crash Reporter Service
 * 
 * Provides a unified interface for runtime error tracking and crash reporting.
 * Dynamically uses Sentry when EXPO_PUBLIC_SENTRY_DSN is set, with fallbacks for local dev logging.
 */

let isSentryInitialized = false;
let SentryInstance = null;

export const initCrashReporter = () => {
  const dsnToUse = SENTRY_DSN || process.env.EXPO_PUBLIC_SENTRY_DSN;

  if (dsnToUse && dsnToUse.trim().startsWith('http')) {
    try {
      const Sentry = require('@sentry/react-native');
      Sentry.init({
        dsn: dsnToUse,
        enableInExpoDevelopment: true,
        debug: !IS_PRODUCTION,
        environment: IS_PRODUCTION ? 'production' : 'development',
        tracesSampleRate: IS_PRODUCTION ? 0.2 : 1.0,
      });
      SentryInstance = Sentry;
      isSentryInitialized = true;
      console.log(`[CrashReporter] Sentry telemetry initialized successfully (${IS_PRODUCTION ? 'Production' : 'Development'}).`);
    } catch (e) {
      console.warn('[CrashReporter] Sentry SDK not installed or failed to initialize:', e.message);
    }
  } else {
    console.log('[CrashReporter] No valid EXPO_PUBLIC_SENTRY_DSN provided. Using local ErrorBoundary & console telemetry.');
  }
};

export const captureException = (error, context = {}) => {
  console.error('[CrashReporter] Exception captured:', error, context);
  if (isSentryInitialized && SentryInstance) {
    SentryInstance.captureException(error, { extra: context });
  }
};

export const captureMessage = (message, level = 'info') => {
  console.log(`[CrashReporter] Message captured (${level}):`, message);
  if (isSentryInitialized && SentryInstance) {
    SentryInstance.captureMessage(message, level);
  }
};

export const setUserContext = (user) => {
  if (isSentryInitialized && SentryInstance && user) {
    SentryInstance.setUser({
      id: user.id || user.studentId,
      email: user.email,
      username: user.username || user.fullName,
    });
  }
};
