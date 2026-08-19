import { API_URL } from '../config/env';
import { getToken, getRefreshToken, setToken, setRefreshToken, clearTokens } from '../utils/authStorage';

export const BASE_URL = API_URL;

async function fetchWithAuth(endpoint, options = {}) {
  let token = await getToken();
  
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
    ...options.headers,
  };

  let response = await fetch(`${BASE_URL}${endpoint}`, {
    ...options,
    headers,
  });

  // Handle transparent 401 Unauthorized token refresh logic
  if (response.status === 401 && !endpoint.includes('/Auth/login') && !endpoint.includes('/Auth/refresh-token')) {
    const refreshToken = await getRefreshToken();
    if (refreshToken) {
      try {
        const refreshResponse = await fetch(`${BASE_URL}/Auth/refresh-token`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        });

        if (refreshResponse.ok) {
          const refreshData = await refreshResponse.json();
          if (refreshData.success && refreshData.token) {
            await setToken(refreshData.token);
            if (refreshData.refreshToken) {
              await setRefreshToken(refreshData.refreshToken);
            }
            // Retry original API request with fresh Bearer token
            headers['Authorization'] = `Bearer ${refreshData.token}`;
            response = await fetch(`${BASE_URL}${endpoint}`, {
              ...options,
              headers,
            });
          }
        } else {
          await clearTokens();
        }
      } catch (e) {
        console.error('[ApiService] Token refresh failed:', e);
        await clearTokens();
      }
    }
  }

  return response;
}

export async function updateProfile(profileData) {
  const res = await fetchWithAuth('/Auth/profile', {
    method: 'PUT',
    body: JSON.stringify(profileData),
  });
  if (!res.ok) {
    const errorText = await res.text();
    throw new Error(errorText || 'Failed to update profile');
  }
  return await res.json();
}

export async function getSubjects() {
  try {
    const res = await fetchWithAuth('/Curriculum/subjects');
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`API Error: ${res.status} - ${text.substring(0, 50)}`);
    }
    return await res.json();
  } catch (err) {
    throw new Error(`Fetch failed: ${err.message}`);
  }
}

export const getSubjectManifest = async (subjectId) => {
  const res = await fetchWithAuth(`/Curriculum/subjects/${subjectId}/manifest`);
  if (!res.ok) return [];
  const json = await res.json();
  if (Array.isArray(json)) return json;
  if (json && Array.isArray(json.data)) return json.data;
  return [];
};

export const getSubjectExams = async (subjectId) => {
  const res = await fetchWithAuth(`/Curriculum/subjects/${subjectId}/exams`);
  if (res.status === 404 || !res.ok) return [];
  const json = await res.json();
  if (Array.isArray(json)) return json;
  if (json && Array.isArray(json.data)) return json.data;
  return [];
};

export const getUnitDetails = async (unitId) => {
  const res = await fetchWithAuth(`/Curriculum/units/${unitId}`);
  if (res.status === 403) {
    throw new Error('SUBSCRIPTION_REQUIRED');
  }
  if (!res.ok) throw new Error('Failed to fetch unit details');
  return await res.json();
};

export const getQuizDetails = async (quizId) => {
  const res = await fetchWithAuth(`/Curriculum/quizzes/${quizId}`);
  if (res.status === 403) {
    throw new Error('SUBSCRIPTION_REQUIRED');
  }
  if (!res.ok) throw new Error('Failed to fetch quiz details');
  return await res.json();
};

export const askAiTutor = async (subjectId, unitId, message) => {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 seconds timeout

  try {
    const res = await fetchWithAuth('/ai/tutor', {
      method: 'POST',
      body: JSON.stringify({ subjectId, unitId, message }),
      signal: controller.signal
    });
    if (!res.ok) throw new Error('Failed to contact AI Tutor');
    return await res.json();
  } finally {
    clearTimeout(timeoutId);
  }
};

export const gradeQuestion = async (questionId, studentAnswer) => {
  const res = await fetchWithAuth('/ai/Grading/grade-question', {
    method: 'POST',
    body: JSON.stringify({ questionId, studentAnswer })
  });
  if (!res.ok) throw new Error('Failed to grade question');
  return await res.json();
};

export const gradeQuestionWithImage = async (questionId, base64Image, base64Images = []) => {
  const payload = { questionId };
  if (Array.isArray(base64Images) && base64Images.length > 0) {
    payload.base64Images = base64Images;
    payload.base64Image = base64Images[0] || '';
  } else if (base64Image) {
    payload.base64Image = base64Image;
    payload.base64Images = [base64Image];
  }

  const res = await fetchWithAuth('/ai/Grading/grade-question-image', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
  if (!res.ok) throw new Error('Failed to grade question image');
  return await res.json();
};

export const getOfflineQuestions = async (unitId) => {
  const res = await fetchWithAuth(`/Curriculum/units/${unitId}/offline-questions`, {
    method: 'GET',
  });
  if (!res.ok) throw new Error('Failed to fetch offline questions');
  return res.json();
};

export const getFlashcards = async (unitId) => {
  const res = await fetchWithAuth(`/Curriculum/units/${unitId}/flashcards`, {
    method: 'GET',
  });
  if (!res.ok) throw new Error('Failed to fetch flashcards');
  return res.json();
};

export const masterFlashcard = async (flashcardId) => {
  const res = await fetchWithAuth(`/Curriculum/flashcards/${flashcardId}/master`, {
    method: 'POST',
  });
  if (!res.ok) throw new Error('Failed to master flashcard');
  return res.json();
};



export const submitQuizAttempts = async (attempts) => {
  const res = await fetchWithAuth('/Curriculum/quiz/submit-attempts', {
    method: 'POST',
    body: JSON.stringify(attempts)
  });
  if (!res.ok) throw new Error('Failed to submit quiz attempts');
  return res.json();
};

export const getPerformance = async () => {
  const res = await fetchWithAuth('/Curriculum/performance', {
    method: 'GET'
  });
  if (!res.ok) throw new Error('Failed to get performance');
  return res.json();
};

export const resetPerformance = async (params) => {
  const res = await fetchWithAuth('/Curriculum/performance/reset', {
    method: 'POST',
    body: JSON.stringify(params)
  });
  if (!res.ok) throw new Error('Failed to reset performance');
  return res.json();
};

export const createQuantitativeSession = async (subjectId, topic) => {
  const res = await fetchWithAuth('/QuantitativeTutor/session', {
    method: 'POST',
    body: JSON.stringify({ subjectId, topic })
  });
  if (!res.ok) throw new Error('Failed to create quantitative session');
  return await res.json();
};

export const getQuantitativeSessions = async (subjectId) => {
  const res = await fetchWithAuth(`/QuantitativeTutor/sessions/${subjectId}`, {
    method: 'GET'
  });
  if (!res.ok) throw new Error('Failed to fetch quantitative sessions');
  return await res.json();
};

export const getQuantitativeSessionById = async (sessionId) => {
  const res = await fetchWithAuth(`/QuantitativeTutor/session/${sessionId}`, {
    method: 'GET'
  });
  if (!res.ok) throw new Error('Failed to fetch quantitative session');
  return await res.json();
};

export const deleteQuantitativeSession = async (sessionId) => {
  const res = await fetchWithAuth(`/QuantitativeTutor/session/${sessionId}`, {
    method: 'DELETE'
  });
  if (!res.ok) throw new Error('Failed to delete session');
  return true;
};

export const interactQuantitativeTutor = async (sessionId, userMessage, uploadedImageUrl = null, mathExpressionLatex = null) => {
  const payload = {
    sessionId,
    userMessage,
  };
  
  if (uploadedImageUrl) payload.uploadedImageUrl = uploadedImageUrl;
  if (mathExpressionLatex) payload.mathExpressionLatex = mathExpressionLatex;

  const res = await fetchWithAuth('/QuantitativeTutor/interact', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
  
  if (!res.ok) {
    const errorText = await res.text();
    throw new Error(errorText || 'Failed to communicate with tutor');
  }
  
  return await res.json();
};

export const getAiUsage = async () => {
  const res = await fetchWithAuth('/Auth/ai-usage', {
    method: 'GET'
  });
  if (!res.ok) throw new Error('Failed to fetch AI usage');
  return await res.json();
};
