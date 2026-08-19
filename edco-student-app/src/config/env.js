/**
 * Centralized Application Environment Configuration
 * 
 * Supports dynamic environment injection via EXPO_PUBLIC_* variables.
 * Falls back to local development network defaults when environment variables are unset.
 */

const getDevHost = () => {
  if (typeof window !== 'undefined' && window.location && window.location.hostname) {
    return `http://${window.location.hostname}:5075`;
  }
  return 'http://192.168.1.154:5075';
};

const DEFAULT_DEV_HOST = getDevHost();
const DEFAULT_DEV_API_URL = `${DEFAULT_DEV_HOST}/api/v1`;

export const API_URL = process.env.EXPO_PUBLIC_API_URL || DEFAULT_DEV_API_URL;
export const API_HOST = process.env.EXPO_PUBLIC_API_HOST || DEFAULT_DEV_HOST;
export const SENTRY_DSN = process.env.EXPO_PUBLIC_SENTRY_DSN || '';
export const IS_PRODUCTION = process.env.NODE_ENV === 'production' && !__DEV__;

console.log(`[Config] Operating in ${__DEV__ ? 'Development' : 'Production'} mode.`);
console.log(`[Config] Target API URL: ${API_URL}`);
