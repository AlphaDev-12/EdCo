import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  View, Text, TextInput, TouchableOpacity, ScrollView,
  StyleSheet, ActivityIndicator, Alert, Image,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import * as ImagePicker from 'expo-image-picker';
import { Ionicons } from '@expo/vector-icons';
import { spacing } from '../theme';
import { useTheme } from '../context/ThemeContext';
import { getQuizDetails, submitQuizAttempts, gradeQuestion, gradeQuestionWithImage } from '../services/apiService';
import MathJax from 'react-native-mathjax';

export default function QuizScreen({ route, navigation }) {
  const { quizId, title } = route.params;
  const { colors } = useTheme();
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [questions, setQuestions] = useState([]);
  const [quizData, setQuizData] = useState(null);
  const [loading, setLoading] = useState(true);
  
  const [currentIdx, setCurrentIdx] = useState(0);
  const [answers, setAnswers] = useState({});
  const [imageAttachments, setImageAttachments] = useState({});
  const [grading, setGrading] = useState(false);
  const [results, setResults] = useState({});
  const [submitted, setSubmitted] = useState(false);

  const loadQuiz = useCallback(async () => {
    try {
      const data = await getQuizDetails(quizId);
      setQuizData(data);
      setQuestions(data.questions || []);
    } catch (e) {
      if (e.message === 'SUBSCRIPTION_REQUIRED') {
        navigation.navigate('Subscription');
      } else {
        Alert.alert('Error', 'Failed to load quiz');
      }
    } finally {
      setLoading(false);
    }
  }, [quizId, navigation]);

  useEffect(() => {
    loadQuiz();
  }, [loadQuiz]);

  const q = questions[currentIdx];

  const setAnswer = useCallback((val) => {
    if (!q) return;
    setAnswers(prev => ({ ...prev, [q.id]: val }));
  }, [q]);

  const launchCamera = useCallback(async (questionId) => {
    const { status } = await ImagePicker.requestCameraPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission Needed', 'Camera permission is required to take photos of your solutions.');
      return;
    }

    const result = await ImagePicker.launchCameraAsync({
      allowsEditing: true,
      quality: 0.4,
      base64: true,
    });

    if (!result.canceled && result.assets?.[0]) {
      setImageAttachments(prev => ({
        ...prev,
        [questionId]: {
          uri: result.assets[0].uri,
          base64: `data:image/jpeg;base64,${result.assets[0].base64}`,
        }
      }));
    }
  }, []);

  const launchGallery = useCallback(async (questionId) => {
    const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission Needed', 'Gallery permission is required to select photos.');
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      allowsEditing: true,
      quality: 0.4,
      base64: true,
    });

    if (!result.canceled && result.assets?.[0]) {
      setImageAttachments(prev => ({
        ...prev,
        [questionId]: {
          uri: result.assets[0].uri,
          base64: `data:image/jpeg;base64,${result.assets[0].base64}`,
        }
      }));
    }
  }, []);

  const pickImageForQuestion = useCallback((questionId) => {
    Alert.alert(
      '📷 Attach Solution Photo',
      'How would you like to capture your solution?',
      [
        { text: 'Take Photo', onPress: () => launchCamera(questionId) },
        { text: 'Choose from Gallery', onPress: () => launchGallery(questionId) },
        { text: 'Cancel', style: 'cancel' },
      ]
    );
  }, [launchCamera, launchGallery]);

  const removeImage = useCallback((questionId) => {
    setImageAttachments(prev => {
      const copy = { ...prev };
      delete copy[questionId];
      return copy;
    });
  }, []);

  const submitQuiz = useCallback(async () => {
    setGrading(true);
    const newResults = {};
    const attempts = [];

    for (const question of questions) {
      const answer = answers[question.id] || '';
      const image = imageAttachments[question.id];
      
      let isCorrect = false;

      if (question.questionType === 'MultipleChoice') {
        const correct = answer === question.correctAnswer;
        isCorrect = correct;
        newResults[question.id] = {
          pointsAwarded: correct ? question.points : 0,
          feedback: correct ? 'Correct' : 'Incorrect',
        };
      } else if (image?.base64) {
        try {
          const res = await gradeQuestionWithImage(question.id, image.base64);
          isCorrect = res.pointsAwarded > 0;
          newResults[question.id] = {
            pointsAwarded: res.pointsAwarded || 0,
            feedback: res.feedback || 'Graded by AI (photo)',
          };
        } catch (e) {
          console.error(e);
          newResults[question.id] = {
            pointsAwarded: 0,
            feedback: 'Photo grading failed. Please try again.',
          };
        }
      } else {
        try {
          const res = await gradeQuestion(question.id, answer);
          isCorrect = res.pointsAwarded > 0;
          newResults[question.id] = {
            pointsAwarded: res.pointsAwarded || 0,
            feedback: res.feedback || 'Graded by AI',
          };
        } catch (e) {
          console.error(e);
          newResults[question.id] = {
            pointsAwarded: 0,
            feedback: 'Grading failed. Please try again.',
          };
        }
      }

      attempts.push({
        questionId: question.id,
        isCorrect: isCorrect
      });
    }

    try {
      await submitQuizAttempts(attempts);
    } catch (e) {
      console.error("Failed to submit attempts", e);
    }

    setResults(newResults);
    setSubmitted(true);
    setGrading(false);
  }, [questions, answers, imageAttachments]);

  const totalScore = useMemo(() => {
    return Object.values(results).reduce((s, r) => s + (r.pointsAwarded || 0), 0);
  }, [results]);

  const maxScore = useMemo(() => {
    return questions.reduce((s, q) => s + q.points, 0);
  }, [questions]);

  if (grading) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={styles.gradingText}>🤖 AI is grading your answers...</Text>
          <Text style={styles.gradingSubtext}>This may take a moment for photo submissions</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (submitted) {
    return (
      <SafeAreaView style={styles.container}>
        <ScrollView contentContainerStyle={styles.resultsContainer}>
          <Text style={styles.resultsTitle}>Quiz Results</Text>
          <View style={styles.scoreCard}>
            <Text style={styles.scoreNum}>{totalScore}/{maxScore}</Text>
            <Text style={styles.scoreLabel}>Points Earned</Text>
          </View>
          {questions.map((question) => (
            <View key={question.id} style={styles.resultCard}>
              <MathJax
                html={`
                  <div style="font-size: 15px; font-family: sans-serif; font-weight: bold; color: ${colors.text}; margin-bottom: ${spacing.sm}px; padding: 4px 0;">
                    ${question.questionText}
                  </div>
                `}
                style={{ backgroundColor: 'transparent', marginVertical: spacing.sm }}
              />
              
              {question.imageUrl ? (
                <Image
                  source={{ uri: question.imageUrl }}
                  style={{ width: '100%', height: 150, resizeMode: 'contain', marginBottom: spacing.md, borderRadius: 8 }}
                />
              ) : null}
              {imageAttachments[question.id] ? (
                <View style={styles.resultImageRow}>
                  <Image 
                    source={{ uri: imageAttachments[question.id].uri }} 
                    style={styles.resultImageThumb}
                  />
                  <Text style={styles.resultPhotoLabel}>📷 Photo submission</Text>
                </View>
              ) : (
                <Text style={styles.resultAnswer}>Your answer: {answers[question.id] || 'No answer'}</Text>
              )}
              {question.correctAnswer ? (
                <View style={{ marginBottom: spacing.sm }}>
                  <Text style={{ fontSize: 13, color: colors.success, fontWeight: '600', marginBottom: 4 }}>Correct Answer:</Text>
                  <MathJax
                    html={`
                      <div style="font-size: 14px; font-family: sans-serif; color: ${colors.success}; padding-left: 8px; border-left: 2px solid ${colors.success};">
                        ${question.correctAnswer.replace(/\\n/g, '<br/>')}
                      </div>
                    `}
                    style={{ backgroundColor: 'transparent' }}
                  />
                </View>
              ) : null}
              <View style={styles.resultFeedbackBox}>
                <Text style={styles.resultPoints}>
                  {results[question.id]?.pointsAwarded || 0}/{question.points} pts
                </Text>
                <Text style={styles.resultFeedback}>{results[question.id]?.feedback}</Text>
              </View>
            </View>
          ))}
          <TouchableOpacity style={styles.doneBtn} onPress={() => navigation.goBack()}>
            <Text style={styles.doneBtnText}>Done</Text>
          </TouchableOpacity>
        </ScrollView>
      </SafeAreaView>
    );
  }

  if (loading) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      </SafeAreaView>
    );
  }

  if (!q) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.header}>
          <TouchableOpacity onPress={() => navigation.goBack()}>
            <Text style={styles.backText}>← Back</Text>
          </TouchableOpacity>
        </View>
        <View style={styles.center}>
          <Text style={{color: colors.text}}>No questions available for this quiz.</Text>
        </View>
      </SafeAreaView>
    );
  }

  const isQuantitative = q.questionType === 'ShortAnswer' || q.questionType === 'Essay';
  const currentImage = imageAttachments[q.id];

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} style={{ marginBottom: 4 }}>
          <Text style={styles.backText}>← Back</Text>
        </TouchableOpacity>
        <Text style={styles.headerTitle}>{quizData?.title || title || 'Quiz'}</Text>
        <Text style={styles.progress}>Question {currentIdx + 1} of {questions.length}</Text>
      </View>

      <ScrollView contentContainerStyle={styles.questionArea}>
        <View style={styles.questionTypeBadge}>
          <Text style={styles.questionTypeText}>{q.questionType} • {q.points} pts</Text>
        </View>
        <MathJax
          html={`
            <div style="font-size: 18px; font-family: sans-serif; font-weight: bold; color: ${colors.text}; line-height: 26px; padding: 4px 0;">
              ${q.questionText}
            </div>
          `}
          style={styles.mathJaxContainer}
        />

        {q.imageUrl ? (
          <Image
            source={{ uri: q.imageUrl }}
            style={{ width: '100%', height: 200, resizeMode: 'contain', marginBottom: spacing.lg, borderRadius: 12 }}
          />
        ) : null}

        {q.questionType === 'MultipleChoice' && q.options?.map((opt) => (
          <TouchableOpacity
            key={opt}
            style={[styles.optionBtn, answers[q.id] === opt && styles.optionSelected]}
            onPress={() => setAnswer(opt)}
          >
            <View style={[styles.radio, answers[q.id] === opt && styles.radioSelected]} />
            <MathJax
              html={`
                <div style="font-size: 15px; font-family: sans-serif; color: ${answers[q.id] === opt ? colors.primary : colors.text}; padding: 2px 0;">
                  ${opt}
                </div>
              `}
              style={styles.mathJaxOptionContainer}
            />
          </TouchableOpacity>
        ))}

        {isQuantitative && (
          <View>
            <TextInput
              style={[styles.textArea, q.questionType === 'Essay' && { height: 160 }]}
              placeholder="Type your answer here..."
              placeholderTextColor={colors.textMuted}
              value={answers[q.id] || ''}
              onChangeText={(t) => setAnswer(t)}
              multiline
              textAlignVertical="top"
            />

            {/* Camera capture section */}
            <View style={styles.photoSection}>
              <Text style={styles.photoSectionLabel}>Or photograph your solution:</Text>
              
              {currentImage ? (
                <View style={styles.imagePreviewContainer}>
                  <Image source={{ uri: currentImage.uri }} style={styles.imagePreview} />
                  <TouchableOpacity style={styles.removeImageBtn} onPress={() => removeImage(q.id)}>
                    <Ionicons name="close-circle" size={28} color={colors.error} />
                  </TouchableOpacity>
                  <View style={styles.imageAttachedBadge}>
                    <Ionicons name="checkmark-circle" size={16} color={colors.success} />
                    <Text style={styles.imageAttachedText}>Photo attached — will be graded by AI</Text>
                  </View>
                </View>
              ) : (
                <TouchableOpacity style={styles.cameraBtn} onPress={() => pickImageForQuestion(q.id)}>
                  <Ionicons name="camera" size={24} color={colors.primary} />
                  <Text style={styles.cameraBtnText}>📷 Take Photo of Solution</Text>
                </TouchableOpacity>
              )}
            </View>
          </View>
        )}
      </ScrollView>

      <View style={styles.navRow}>
        {currentIdx > 0 ? (
          <TouchableOpacity style={styles.navBtn} onPress={() => setCurrentIdx(prev => prev - 1)}>
            <Text style={styles.navBtnText}>Previous</Text>
          </TouchableOpacity>
        ) : <View style={{ flex: 1 }} />}

        {currentIdx < questions.length - 1 ? (
          <TouchableOpacity style={styles.navBtnPrimary} onPress={() => setCurrentIdx(prev => prev + 1)}>
            <Text style={styles.navBtnPrimaryText}>Next</Text>
          </TouchableOpacity>
        ) : (
          <TouchableOpacity style={styles.submitBtn} onPress={submitQuiz}>
            <Text style={styles.submitBtnText}>Submit Quiz</Text>
          </TouchableOpacity>
        )}
      </View>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  gradingText: { color: colors.textSecondary, marginTop: spacing.md, fontSize: 16 },
  gradingSubtext: { color: colors.textMuted, marginTop: 4, fontSize: 13 },
  header: { paddingHorizontal: spacing.lg, paddingTop: 10, paddingBottom: spacing.md, borderBottomWidth: 1, borderBottomColor: colors.border },
  backText: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  headerTitle: { fontSize: 20, fontWeight: '800', color: colors.text },
  progress: { fontSize: 13, color: colors.textMuted, marginTop: 2 },
  questionArea: { padding: spacing.lg },
  questionTypeBadge: { backgroundColor: colors.primary + '20', paddingHorizontal: 12, paddingVertical: 4, borderRadius: 8, alignSelf: 'flex-start', marginBottom: spacing.md },
  questionTypeText: { color: colors.primaryLight, fontSize: 12, fontWeight: '700' },
  questionText: { fontSize: 18, fontWeight: '700', color: colors.text, lineHeight: 26, marginBottom: spacing.lg },
  optionBtn: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, marginBottom: spacing.sm, borderWidth: 1, borderColor: colors.border },
  optionSelected: { borderColor: colors.primary, backgroundColor: colors.primary + '15' },
  radio: { width: 20, height: 20, borderRadius: 10, borderWidth: 2, borderColor: colors.textMuted, marginRight: spacing.md },
  radioSelected: { borderColor: colors.primary, backgroundColor: colors.primary },
  optionText: { fontSize: 15, color: colors.text },
  mathJaxContainer: { backgroundColor: 'transparent', marginVertical: spacing.sm },
  mathJaxOptionContainer: { backgroundColor: 'transparent', width: '100%' },
  textArea: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, color: colors.text, fontSize: 15, height: 100, borderWidth: 1, borderColor: colors.border },
  
  photoSection: { marginTop: spacing.md },
  photoSectionLabel: { color: colors.textMuted, fontSize: 13, marginBottom: spacing.sm, fontWeight: '600' },
  cameraBtn: { 
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, 
    borderWidth: 1.5, borderColor: colors.primary + '40', borderStyle: 'dashed',
  },
  cameraBtnText: { color: colors.primary, fontSize: 15, fontWeight: '700', marginLeft: spacing.sm },
  imagePreviewContainer: { position: 'relative' },
  imagePreview: { width: '100%', height: 200, borderRadius: 12, resizeMode: 'cover' },
  removeImageBtn: { 
    position: 'absolute', top: 8, right: 8, 
    backgroundColor: colors.surface, borderRadius: 14, 
  },
  imageAttachedBadge: { 
    flexDirection: 'row', alignItems: 'center', 
    marginTop: spacing.sm, paddingVertical: 4 
  },
  imageAttachedText: { color: colors.success, fontSize: 12, fontWeight: '600', marginLeft: 4 },

  navRow: { flexDirection: 'row', justifyContent: 'space-between', padding: spacing.lg, borderTopWidth: 1, borderTopColor: colors.border },
  navBtn: { paddingVertical: 12, paddingHorizontal: 20 },
  navBtnText: { color: colors.textSecondary, fontWeight: '600' },
  navBtnPrimary: { backgroundColor: colors.surfaceLight, paddingVertical: 12, paddingHorizontal: 20, borderRadius: 12 },
  navBtnPrimaryText: { color: colors.text, fontWeight: '700' },
  submitBtn: { backgroundColor: colors.primary, paddingVertical: 12, paddingHorizontal: 24, borderRadius: 12 },
  submitBtnText: { color: '#fff', fontWeight: '700', fontSize: 15 },
  resultsContainer: { padding: spacing.lg },
  resultsTitle: { fontSize: 24, fontWeight: '800', color: colors.text, textAlign: 'center', marginBottom: spacing.md },
  scoreCard: { backgroundColor: colors.primary, borderRadius: 16, padding: spacing.lg, alignItems: 'center', marginBottom: spacing.lg },
  scoreNum: { fontSize: 36, fontWeight: '800', color: '#fff' },
  scoreLabel: { color: '#ffffffbb', fontSize: 14 },
  resultCard: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, marginBottom: spacing.md, borderWidth: 1, borderColor: colors.border },
  resultQ: { fontSize: 15, fontWeight: '700', color: colors.text, marginBottom: spacing.sm },
  resultAnswer: { fontSize: 13, color: colors.textSecondary, marginBottom: spacing.sm },
  resultCorrectAnswer: { fontSize: 13, color: colors.success, marginBottom: spacing.sm, fontWeight: '600' },
  resultImageRow: { flexDirection: 'row', alignItems: 'center', marginBottom: spacing.sm },
  resultImageThumb: { width: 50, height: 50, borderRadius: 8, marginRight: spacing.sm },
  resultPhotoLabel: { fontSize: 13, color: colors.textSecondary },
  resultFeedbackBox: { backgroundColor: colors.surfaceLight, borderRadius: 8, padding: spacing.sm },
  resultPoints: { fontSize: 13, fontWeight: '700', color: colors.success, marginBottom: 4 },
  resultFeedback: { fontSize: 13, color: colors.textSecondary, lineHeight: 18 },
  doneBtn: { backgroundColor: colors.primary, borderRadius: 12, paddingVertical: 14, alignItems: 'center', marginTop: spacing.md },
  doneBtnText: { color: '#fff', fontWeight: '700', fontSize: 16 },
});

