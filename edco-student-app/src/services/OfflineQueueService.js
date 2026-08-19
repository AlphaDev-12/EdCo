import AsyncStorage from '@react-native-async-storage/async-storage';
import { submitQuizResult } from './apiService';
import { captureMessage, captureException } from './crashReporter';

const PENDING_SUBMISSIONS_KEY = 'EDCO_PENDING_QUIZ_SUBMISSIONS';

/**
 * Service to queue and flush pending quiz submissions when offline.
 */
export const queueQuizSubmission = async (quizSubmissionPayload) => {
  try {
    const existing = await getPendingSubmissions();
    const item = {
      id: `submission_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
      payload: quizSubmissionPayload,
      queuedAt: new Date().toISOString(),
    };
    existing.push(item);
    await AsyncStorage.setItem(PENDING_SUBMISSIONS_KEY, JSON.stringify(existing));
    captureMessage(`Queued offline quiz submission for quizId: ${quizSubmissionPayload.quizId}`);
    return item.id;
  } catch (error) {
    captureException(error, { action: 'queueQuizSubmission' });
    throw error;
  }
};

export const getPendingSubmissions = async () => {
  try {
    const json = await AsyncStorage.getItem(PENDING_SUBMISSIONS_KEY);
    return json ? JSON.parse(json) : [];
  } catch (error) {
    console.error('[OfflineQueueService] Failed to read pending submissions', error);
    return [];
  }
};

export const flushPendingSubmissions = async () => {
  const pending = await getPendingSubmissions();
  if (pending.length === 0) return { synced: 0, failed: 0 };

  console.log(`[OfflineQueueService] Attempting to flush ${pending.length} pending submissions...`);
  const remaining = [];
  let syncedCount = 0;
  let failedCount = 0;

  for (const item of pending) {
    try {
      await submitQuizResult(item.payload);
      syncedCount++;
      console.log(`[OfflineQueueService] Successfully synced offline submission ${item.id}`);
    } catch (error) {
      console.warn(`[OfflineQueueService] Failed to sync submission ${item.id}, retaining in queue`, error.message);
      remaining.push(item);
      failedCount++;
    }
  }

  await AsyncStorage.setItem(PENDING_SUBMISSIONS_KEY, JSON.stringify(remaining));
  return { synced: syncedCount, failed: failedCount };
};

export const clearPendingSubmissions = async () => {
  await AsyncStorage.removeItem(PENDING_SUBMISSIONS_KEY);
};
