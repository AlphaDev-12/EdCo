import * as FileSystem from 'expo-file-system/legacy';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getOfflineQuestions, getFlashcards } from './apiService';
import { API_HOST } from '../config/env';

const OFFLINE_INDEX_KEY = 'OFFLINE_UNITS_INDEX';
const OFFLINE_DIR = FileSystem.documentDirectory + 'offline_units/';

// Ensure the offline directory exists
const ensureDirExists = async () => {
  const dirInfo = await FileSystem.getInfoAsync(OFFLINE_DIR);
  if (!dirInfo.exists) {
    await FileSystem.makeDirectoryAsync(OFFLINE_DIR, { intermediates: true });
  }
};

export const getOfflineUnits = async () => {
  try {
    const json = await AsyncStorage.getItem(OFFLINE_INDEX_KEY);
    return json ? JSON.parse(json) : [];
  } catch (e) {
    console.error('Failed to get offline index', e);
    return [];
  }
};

const saveToOfflineIndex = async (unitSummary) => {
  const units = await getOfflineUnits();
  const existingIndex = units.findIndex(u => u.id === unitSummary.id);
  if (existingIndex >= 0) {
    units[existingIndex] = unitSummary;
  } else {
    units.push(unitSummary);
  }
  await AsyncStorage.setItem(OFFLINE_INDEX_KEY, JSON.stringify(units));
};

const removeFromOfflineIndex = async (unitId) => {
  const units = await getOfflineUnits();
  const filtered = units.filter(u => u.id !== unitId);
  await AsyncStorage.setItem(OFFLINE_INDEX_KEY, JSON.stringify(filtered));
};

export const downloadUnit = async (unit, flashcards, onProgress) => {
  await ensureDirExists();
  const unitId = unit.id;
  const offlineUnit = { ...unit };

  try {
    // Helper to ensure URL is absolute
    const makeAbsolute = (url) => {
      if (!url) return null;
      if (url.startsWith('http')) return url;
      return `${API_HOST}${url.startsWith('/') ? '' : '/'}${url}`;
    };


    if (onProgress) onProgress('Downloading Video...');
    if (unit.videoUrl) {
      let absVideoUrl = makeAbsolute(unit.videoUrl);
      const PULL_ZONE = 'vz-946a10f3-d90.b-cdn.net';
      let isBunnyCdn = false;
      let bunnyVideoId = null;
      
      // Transform BunnyCDN embed URLs to direct MP4 URLs
      if (absVideoUrl.includes('mediadelivery.net/') || absVideoUrl.includes('bunnycdn.com/play/')) {
        isBunnyCdn = true;
        const urlWithoutQuery = absVideoUrl.split('?')[0];
        const parts = urlWithoutQuery.split('/');
        bunnyVideoId = parts[parts.length - 1]; // Extracted video ID
      }
      
      // Use original extension if available
      const extMatch = absVideoUrl.match(/\.([a-zA-Z0-9]+)(?:[\?#]|$)/);
      const ext = extMatch ? extMatch[1] : 'mp4';
      const videoPath = OFFLINE_DIR + `unit_${unitId}_video.${ext}`;
      
      const token = await AsyncStorage.getItem('userToken');
      const headers = token ? { 'Authorization': `Bearer ${token}`, 'Referer': 'http://localhost:5300/' } : { 'Referer': 'http://localhost:5300/' };
      
      if (isBunnyCdn) {
        const resolutions = ['720p', '480p', '360p', '240p'];
        let success = false;
        
        for (const res of resolutions) {
          if (onProgress) onProgress(`Downloading Video (${res})...`);
          const testUrl = `https://${PULL_ZONE}/${bunnyVideoId}/play_${res}.mp4`;
          const downloadResult = await FileSystem.downloadAsync(testUrl, videoPath, { headers });
          
          if (downloadResult.status === 200) {
            offlineUnit.videoUrl = downloadResult.uri;
            success = true;
            break;
          } else {
            // Delete the error HTML page
            await FileSystem.deleteAsync(videoPath, { idempotent: true });
          }
        }
        
        if (!success) {
          throw new Error("Could not download video. MP4 Fallback might be disabled or Token Authentication is blocking it.");
        }
      } else {
        const downloadResult = await FileSystem.downloadAsync(absVideoUrl, videoPath, { headers });
        if (downloadResult.status !== 200) {
           await FileSystem.deleteAsync(videoPath, { idempotent: true });
           throw new Error("Failed to download video, server returned status: " + downloadResult.status);
        }
        offlineUnit.videoUrl = downloadResult.uri;
      }
    }

    if (onProgress) onProgress('Downloading Notes...');
    if (unit.notesUrl) {
      const absNotesUrl = makeAbsolute(unit.notesUrl);
      const notesPath = OFFLINE_DIR + `unit_${unitId}_notes.pdf`;
      const token = await AsyncStorage.getItem('userToken');
      const headers = token ? { 'Authorization': `Bearer ${token}` } : {};
      const downloadResult = await FileSystem.downloadAsync(absNotesUrl, notesPath, { headers });
      offlineUnit.notesUrl = downloadResult.uri;
    }

    if (onProgress) onProgress('Downloading Questions...');
    try {
      const mcqs = await getOfflineQuestions(unitId);
      offlineUnit.questions = mcqs;
    } catch (e) {
      console.warn('Failed to fetch offline MCQs, proceeding with default questions', e);
    }

    if (onProgress) onProgress('Downloading Flashcards...');
    let finalFlashcards = flashcards;
    try {
      const fcData = await getFlashcards(unitId);
      finalFlashcards = fcData;
    } catch (e) {
      console.warn('Failed to fetch offline Flashcards', e);
    }

    if (onProgress) onProgress('Saving Data...');
    // Save to AsyncStorage
    await AsyncStorage.setItem(`offline_unit_${unitId}`, JSON.stringify(offlineUnit));
    await AsyncStorage.setItem(`offline_flashcards_${unitId}`, JSON.stringify(finalFlashcards));

    // Update index
    await saveToOfflineIndex({
      id: unitId,
      title: unit.title,
      chapterId: unit.chapterId,
      orderIndex: unit.orderIndex,
      downloadedAt: new Date().toISOString()
    });

    if (onProgress) onProgress('Done!');
    return true;
  } catch (e) {
    console.error('Error downloading unit', e);
    throw e;
  }
};

export const getOfflineUnitDetails = async (unitId) => {
  try {
    const unitJson = await AsyncStorage.getItem(`offline_unit_${unitId}`);
    const flashcardsJson = await AsyncStorage.getItem(`offline_flashcards_${unitId}`);
    
    if (!unitJson) throw new Error('Unit not found offline');

    return {
      unit: JSON.parse(unitJson),
      flashcards: flashcardsJson ? JSON.parse(flashcardsJson) : { active: [], mastered: [] }
    };
  } catch (e) {
    console.error('Failed to load offline unit details', e);
    throw e;
  }
};

export const removeOfflineUnit = async (unitId) => {
  try {
    const { unit } = await getOfflineUnitDetails(unitId);
    
    // Delete files
    if (unit.videoUrl && unit.videoUrl.startsWith('file://')) {
      await FileSystem.deleteAsync(unit.videoUrl, { idempotent: true });
    }
    if (unit.notesUrl && unit.notesUrl.startsWith('file://')) {
      await FileSystem.deleteAsync(unit.notesUrl, { idempotent: true });
    }

    // Remove from storage
    await AsyncStorage.removeItem(`offline_unit_${unitId}`);
    await AsyncStorage.removeItem(`offline_flashcards_${unitId}`);

    // Update index
    await removeFromOfflineIndex(unitId);
  } catch (e) {
    console.error('Error removing offline unit', e);
  }
};

export const isUnitOffline = async (unitId) => {
  const units = await getOfflineUnits();
  return units.some(u => u.id === unitId);
};
