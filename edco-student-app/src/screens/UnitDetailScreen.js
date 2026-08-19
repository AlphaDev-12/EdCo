import React, { useState, useEffect } from 'react';
import {
  View, Text, StyleSheet, TouchableOpacity, ScrollView,
  ActivityIndicator, TextInput, Dimensions, KeyboardAvoidingView, Platform, StatusBar
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { AppWebView as WebView } from '../components/AppWebView';
import { useVideoPlayer, VideoView } from 'expo-video';
import { colors, spacing } from '../theme';
import { getUnitDetails, gradeQuestion, getFlashcards, masterFlashcard, submitQuizAttempts, getPerformance, resetPerformance } from '../services/apiService';
import { downloadUnit, getOfflineUnitDetails, isUnitOffline, removeOfflineUnit } from '../services/offlineService';

const { width } = Dimensions.get('window');

export default function UnitDetailScreen({ route, navigation }) {
  const { unitId, highlightText, isOffline } = route.params;
  
  const [unit, setUnit] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState(highlightText ? 'notes' : 'notes'); 
  const [isVideoVisible, setIsVideoVisible] = useState(true);
  
  useEffect(() => {
    if (highlightText) {
      setActiveTab('notes');
    }
  }, [highlightText]);
  
  // Quiz State
  const [quizAnswers, setQuizAnswers] = useState({});
  const [quizResults, setQuizResults] = useState({});
  const [grading, setGrading] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  // Flashcards State
  const [flashcardsData, setFlashcardsData] = useState({ active: [], mastered: [] });
  const [showMastered, setShowMastered] = useState(false);
  const [flippedCards, setFlippedCards] = useState({});

  // Offline State
  const [isDownloaded, setIsDownloaded] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [downloadProgress, setDownloadProgress] = useState('');

  // Performance State
  const [unitPerformance, setUnitPerformance] = useState(null);

  const player = useVideoPlayer(unit?.videoUrl, player => {
    player.loop = false;
  });

  useEffect(() => {
    loadUnit();
    loadPerformance();
  }, [unitId]);

  const loadPerformance = async () => {
    try {
      const data = await getPerformance();
      const perf = data.unitPerformance.find(p => p.unitId === unitId);
      setUnitPerformance(perf || { totalAttempts: 0, correct: 0 });
    } catch (e) {
      console.error("Failed to load performance", e);
    }
  };

  const loadUnit = async () => {
    try {
      setLoading(true);
      setError(null);
      setSubmitted(false);
      setQuizAnswers({});
      setQuizResults({});
      
      if (isOffline) {
        const offlineData = await getOfflineUnitDetails(unitId);
        setUnit(offlineData.unit);
        setFlashcardsData(offlineData.flashcards);
      } else {
        const data = await getUnitDetails(unitId);
        setUnit(data);
      }
      
      const offlineStatus = await isUnitOffline(unitId);
      setIsDownloaded(offlineStatus);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const loadFlashcardsData = async () => {
    if (isOffline) return; // Loaded in loadUnit
    try {
      const data = await getFlashcards(unitId);
      setFlashcardsData(data || { active: [], mastered: [] });
    } catch (e) {
      console.error("Failed to load flashcards", e);
    }
  };

  useEffect(() => {
    if (!loading && activeTab === 'flashcards') {
      loadFlashcardsData();
    }
  }, [activeTab, loading]);

  const setAnswer = (questionId, val) => {
    setQuizAnswers(prev => ({ ...prev, [questionId]: val }));
  };

  const submitQuiz = async () => {
    setGrading(true);
    const newResults = {};
    const attempts = [];

    for (const question of unit.questions) {
      const answer = quizAnswers[question.id] || '';
      if (question.questionType === 'MultipleChoice') {
        const expectedText = question[`option${question.correctOption}`];
        const isCorrect = answer === expectedText;
        newResults[question.id] = {
          pointsAwarded: isCorrect ? question.points : 0,
          feedback: isCorrect ? 'Correct! ✅' : `Incorrect.`,
        };
        attempts.push({ questionId: question.id, isCorrect });
      } else {
        try {
          const result = await gradeQuestion(question.id, answer);
          newResults[question.id] = result;
          attempts.push({ questionId: question.id, isCorrect: result.pointsAwarded > 0 });
        } catch (e) {
          newResults[question.id] = { pointsAwarded: 0, feedback: 'Could not grade this question.' };
          attempts.push({ questionId: question.id, isCorrect: false });
        }
      }
    }

    try {
      if (!isOffline) {
        await submitQuizAttempts(attempts);
        await loadPerformance(); // Refresh performance
      }
    } catch (e) {
      console.error("Failed to save attempts", e);
    }

    setQuizResults(newResults);
    setSubmitted(true);
    setGrading(false);
  };

  const handleResetUnit = async () => {
    try {
      setLoading(true);
      await resetPerformance({ unitId });
      await loadUnit();
      await loadPerformance();
    } catch (e) {
      console.error("Failed to reset unit", e);
      setLoading(false);
    }
  };

  const toggleOffline = async () => {
    try {
      if (isDownloaded) {
        setDownloading(true);
        setDownloadProgress('Removing...');
        await removeOfflineUnit(unitId);
        setIsDownloaded(false);
      } else {
        setDownloading(true);
        await downloadUnit(unit, flashcardsData, setDownloadProgress);
        setIsDownloaded(true);
      }
    } catch (e) {
      alert("Failed to toggle offline status.");
    } finally {
      setDownloading(false);
      setDownloadProgress('');
    }
  };

  if (loading) {
    return (
      <View style={s.center}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (error) {
    return (
      <View style={s.center}>
        <Text style={{ color: colors.error }}>{error}</Text>
        <TouchableOpacity style={s.retryBtn} onPress={loadUnit}>
          <Text style={s.retryBtnText}>Retry</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (!unit) return null;

  const renderQuizTab = () => {
    if (!unit.questions || unit.questions.length === 0) {
      return (
        <View style={s.tabContentCenter}>
          <Text style={s.emptyText}>No practice questions available for this unit.</Text>
          {unitPerformance && unitPerformance.totalAttempts > 0 && (
            <View style={s.perfContainer}>
              <Text style={s.perfTitle}>Unit Performance</Text>
              <Text style={s.perfScore}>{unitPerformance.correct} / {unitPerformance.totalAttempts} Correct</Text>
              <TouchableOpacity style={s.resetBtn} onPress={handleResetUnit}>
                <Text style={s.resetBtnText}>Reset Unit Progress</Text>
              </TouchableOpacity>
            </View>
          )}
        </View>
      );
    }

    if (grading) {
      return (
        <View style={s.tabContentCenter}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={s.gradingText}>🤖 AI is grading your answers...</Text>
        </View>
      );
    }

    if (submitted) {
      const totalScore = Object.values(quizResults).reduce((s, r) => s + (r.pointsAwarded || 0), 0);
      const maxScore = unit.questions.reduce((s, q) => s + q.points, 0);

      return (
        <ScrollView contentContainerStyle={s.scrollTab}>
          <View style={s.scoreCard}>
            <Text style={s.scoreNum}>{totalScore}/{maxScore}</Text>
            <Text style={s.scoreLabel}>Points Earned</Text>
          </View>
          
          {unit.questions.map((q) => (
            <View key={q.id} style={s.resultCard}>
              <Text style={s.resultQ}>{q.questionText}</Text>
              <Text style={s.resultAnswer}>Your answer: {quizAnswers[q.id] || 'No answer'}</Text>
              <View style={s.resultFeedbackBox}>
                <Text style={s.resultPoints}>
                  {quizResults[q.id]?.pointsAwarded || 0}/{q.points} pts
                </Text>
                <Text style={s.resultFeedback}>{quizResults[q.id]?.feedback}</Text>
              </View>
            </View>
          ))}

          <TouchableOpacity style={s.submitBtn} onPress={loadUnit}>
            <Text style={s.submitBtnText}>Practice More</Text>
          </TouchableOpacity>

          {unitPerformance && unitPerformance.totalAttempts > 0 && (
            <View style={s.perfContainer}>
              <Text style={s.perfTitle}>Overall Unit Performance</Text>
              <Text style={s.perfScore}>{unitPerformance.correct} / {unitPerformance.totalAttempts} Correct</Text>
              <TouchableOpacity style={s.resetBtn} onPress={handleResetUnit}>
                <Text style={s.resetBtnText}>Reset Unit Progress</Text>
              </TouchableOpacity>
            </View>
          )}
        </ScrollView>
      );
    }

    return (
      <ScrollView contentContainerStyle={s.scrollTab}>
        {unit.questions.map((q, i) => (
          <View key={q.id} style={s.questionCard}>
            <View style={s.questionTypeBadge}>
              <Text style={s.questionTypeText}>{q.questionType} • {q.points} pts</Text>
            </View>
            <Text style={s.questionText}>{i + 1}. {q.questionText}</Text>

            {q.questionType === 'MultipleChoice' ? (
              [q.optionA, q.optionB, q.optionC, q.optionD].filter(Boolean).map((opt) => (
                <TouchableOpacity
                  key={opt}
                  style={[s.optionBtn, quizAnswers[q.id] === opt && s.optionSelected]}
                  onPress={() => setAnswer(q.id, opt)}
                >
                  <View style={[s.radio, quizAnswers[q.id] === opt && s.radioSelected]} />
                  <Text style={[s.optionText, quizAnswers[q.id] === opt && { color: colors.primary }]}>{opt}</Text>
                </TouchableOpacity>
              ))
            ) : (
              <TextInput
                style={[s.textArea, q.questionType === 'Essay' && { height: 120 }]}
                placeholder="Type your answer here..."
                placeholderTextColor={colors.textMuted}
                value={quizAnswers[q.id] || ''}
                onChangeText={(t) => setAnswer(q.id, t)}
                multiline
                textAlignVertical="top"
              />
            )}
          </View>
        ))}

        <TouchableOpacity style={s.submitBtn} onPress={submitQuiz}>
          <Text style={s.submitBtnText}>Submit Answers for AI Grading</Text>
        </TouchableOpacity>
      </ScrollView>
    );
  };

  const renderHighlightedMarkdown = (text, highlight) => {
    if (!highlight || !text) return <Text style={s.markdownText}>{text}</Text>;
    
    // Simple case-insensitive split
    // Escape regex special chars
    const escapedHighlight = highlight.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const parts = text.split(new RegExp(`(${escapedHighlight})`, 'gi'));
    return (
      <Text style={s.markdownText}>
        {parts.map((part, i) => (
          part.toLowerCase() === highlight.toLowerCase() 
            ? <Text key={i} style={{backgroundColor: '#fff3cd', fontWeight: 'bold', color: '#856404'}}>{part}</Text> 
            : <Text key={i}>{part}</Text>
        ))}
      </Text>
    );
  };

  const renderNotesTab = () => {
    if (unit.notesMarkdown && unit.notesMarkdown.trim().length > 0) {
      return (
        <ScrollView contentContainerStyle={s.scrollTab}>
          {renderHighlightedMarkdown(unit.notesMarkdown, highlightText)}
        </ScrollView>
      );
    } else if (unit.notesUrl) {
      const jsCode = highlightText 
        ? `setTimeout(() => { window.find('${highlightText.replace(/'/g, "\\'")}'); }, 1000); true;` 
        : '';
      return <WebView 
        source={{ uri: unit.notesUrl }} 
        style={{ flex: 1 }} 
        injectedJavaScript={jsCode} 
        allowFileAccess={true}
        originWhitelist={['*']}
      />;
    }
    return (
      <View style={s.tabContentCenter}>
        <Text style={s.emptyText}>No notes available for this unit.</Text>
      </View>
    );
  };

  const renderFlashcardsTab = () => {
    const cards = showMastered ? flashcardsData.mastered : flashcardsData.active;

    return (
      <View style={{ flex: 1 }}>
        <View style={s.fcToggleRow}>
          <TouchableOpacity 
            style={[s.fcToggleBtn, !showMastered && s.fcToggleActive]} 
            onPress={() => setShowMastered(false)}>
            <Text style={[s.fcToggleText, !showMastered && s.fcToggleTextActive]}>
              Active Deck ({flashcardsData.active?.length || 0})
            </Text>
          </TouchableOpacity>
          <TouchableOpacity 
            style={[s.fcToggleBtn, showMastered && s.fcToggleActive]} 
            onPress={() => setShowMastered(true)}>
            <Text style={[s.fcToggleText, showMastered && s.fcToggleTextActive]}>
              Mastered ({flashcardsData.mastered?.length || 0})
            </Text>
          </TouchableOpacity>
        </View>

        <ScrollView contentContainerStyle={s.scrollTab}>
          {!cards || cards.length === 0 ? (
            <View style={s.tabContentCenter}>
              <Text style={s.emptyText}>
                {showMastered ? "You haven't mastered any flashcards yet." : "No active flashcards left!"}
              </Text>
            </View>
          ) : (
            cards.map(card => {
              const isFlipped = flippedCards[card.id];
              return (
                <View key={card.id} style={s.fcCard}>
                  <TouchableOpacity activeOpacity={0.8} onPress={() => setFlippedCards(p => ({...p, [card.id]: !isFlipped}))}>
                    <View style={s.fcContent}>
                      {!isFlipped ? (
                        <Text style={s.fcTextFront}>{card.frontContent}</Text>
                      ) : (
                        <Text style={s.fcTextBack}>{card.backContent}</Text>
                      )}
                    </View>
                  </TouchableOpacity>

                  {isFlipped && !showMastered && (
                    <View style={s.fcActions}>
                      <TouchableOpacity style={s.fcBtnNeedsReview} onPress={() => setFlippedCards(p => ({...p, [card.id]: false}))}>
                        <Text style={s.fcBtnTextDark}>Needs Review</Text>
                      </TouchableOpacity>
                      <TouchableOpacity style={s.fcBtnGotIt} onPress={async () => {
                        await masterFlashcard(card.id);
                        loadFlashcardsData();
                      }}>
                        <Text style={s.fcBtnTextLight}>Got It</Text>
                      </TouchableOpacity>
                    </View>
                  )}
                </View>
              );
            })
          )}
        </ScrollView>
      </View>
    );
  };

  return (
    <SafeAreaView style={s.container}>
      {/* Header */}
      <View style={s.header}>
        <View style={{flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between'}}>
          <TouchableOpacity onPress={() => navigation.goBack()}>
            <Text style={s.back}>← Back</Text>
          </TouchableOpacity>
          {!isOffline && (
            <TouchableOpacity onPress={toggleOffline} disabled={downloading} style={s.offlineBtn}>
              {downloading ? (
                <Text style={s.offlineBtnText}>{downloadProgress}</Text>
              ) : (
                <Text style={s.offlineBtnText}>{isDownloaded ? 'Remove Download' : 'Save for Offline'}</Text>
              )}
            </TouchableOpacity>
          )}
        </View>
      </View>

      {/* Video Player */}
      {isVideoVisible && (
        <View style={s.videoContainer}>
          {unit.videoUrl ? (
            unit.videoUrl.startsWith('file://') ? (
              <VideoView
                style={s.video}
                player={player}
                allowsFullscreen
                allowsPictureInPicture
                nativeControls={true}
                contentFit="contain"
              />
            ) : (
              <WebView
                source={{ uri: unit.videoUrl }}
                style={s.video}
                allowsInlineMediaPlayback={true}
                allowsFullscreenVideo={true}
                mediaPlaybackRequiresUserAction={false}
                javaScriptEnabled={true}
                domStorageEnabled={true}
                allowFileAccess={true}
                allowFileAccessFromFileURLs={true}
                allowUniversalAccessFromFileURLs={true}
                originWhitelist={['*']}
              />
            )
          ) : (
            <View style={s.noVideoBox}>
              <Text style={s.emptyText}>No Video Provided</Text>
            </View>
          )}
        </View>
      )}

      {/* Header Info */}
      <View style={[s.headerInfo, { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }]}>
        <Text style={[s.unitTitle, { flex: 1 }]}>
          <Text style={{ color: colors.primary }}>{unit.chapterTitle}: </Text>
          {unit.title}
        </Text>
        {unit.videoUrl && (
          <TouchableOpacity onPress={() => setIsVideoVisible(!isVideoVisible)} style={s.toggleVideoBtn}>
            <Ionicons name={isVideoVisible ? "chevron-up" : "chevron-down"} size={24} color={colors.primary} />
          </TouchableOpacity>
        )}
      </View>

      {/* Custom Tabs */}
      <View style={s.tabRow}>
        <TouchableOpacity 
          style={[s.tabBtn, activeTab === 'notes' && s.tabActive]} 
          onPress={() => setActiveTab('notes')}
        >
          <Text style={[s.tabText, activeTab === 'notes' && s.tabTextActive]}>Notes</Text>
        </TouchableOpacity>
        <TouchableOpacity 
          style={[s.tabBtn, activeTab === 'flashcards' && s.tabActive]} 
          onPress={() => setActiveTab('flashcards')}
        >
          <Text style={[s.tabText, activeTab === 'flashcards' && s.tabTextActive]}>Flashcards</Text>
        </TouchableOpacity>
        <TouchableOpacity 
          style={[s.tabBtn, activeTab === 'quiz' && s.tabActive]} 
          onPress={() => setActiveTab('quiz')}
        >
          <Text style={[s.tabText, activeTab === 'quiz' && s.tabTextActive]}>Quizzes</Text>
        </TouchableOpacity>
      </View>

      {/* Tab Content */}
      <KeyboardAvoidingView style={s.tabContentContainer} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        {activeTab === 'notes' && renderNotesTab()}
        {activeTab === 'quiz' && renderQuizTab()}
        {activeTab === 'flashcards' && renderFlashcardsTab()}
      </KeyboardAvoidingView>

      {/* AI Tutor FAB */}
      <TouchableOpacity 
        style={s.fab}
        onPress={() => navigation.navigate('AiTutor', { unitId: unit.id, unitTitle: unit.title })}
      >
        <Text style={s.fabIcon}>🤖</Text>
      </TouchableOpacity>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  container: { 
    flex: 1, 
    backgroundColor: colors.background,
    paddingTop: 10
  },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: { paddingHorizontal: spacing.lg, paddingTop: 4, paddingBottom: spacing.sm },
  back: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  videoContainer: {
    width: width,
    height: width * (9 / 16), // 16:9 Aspect Ratio
    backgroundColor: '#000',
  },
  video: {
    flex: 1,
  },
  noVideoBox: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.surfaceLight,
  },
  headerInfo: {
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  chapterTitle: {
    fontSize: 13,
    color: colors.primaryLight,
    fontWeight: '700',
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  unitTitle: {
    fontSize: 22,
    fontWeight: '800',
    color: colors.text,
  },
  toggleVideoBtn: {
    padding: 8,
    backgroundColor: colors.surfaceLight,
    borderRadius: 20,
    marginLeft: 10,
    justifyContent: 'center',
    alignItems: 'center',
  },
  tabRow: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 14,
    alignItems: 'center',
  },
  tabActive: {
    borderBottomWidth: 3,
    borderBottomColor: colors.primary,
  },
  tabText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.textSecondary,
  },
  tabTextActive: {
    color: colors.primary,
    fontWeight: '700',
  },
  tabContentContainer: {
    flex: 1,
  },
  tabContentCenter: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
  },
  scrollTab: {
    padding: spacing.lg,
    paddingBottom: 100, // Make room for FAB
  },
  emptyText: {
    color: colors.textMuted,
    fontSize: 15,
  },
  retryBtn: {
    marginTop: spacing.md,
    backgroundColor: colors.surfaceLight,
    paddingHorizontal: 20,
    paddingVertical: 10,
    borderRadius: 8,
  },
  retryBtnText: {
    color: colors.text,
    fontWeight: '600',
  },
  // Quiz Styles
  questionCard: {
    marginBottom: spacing.xl,
  },
  questionTypeBadge: { backgroundColor: colors.primary + '20', paddingHorizontal: 12, paddingVertical: 4, borderRadius: 8, alignSelf: 'flex-start', marginBottom: spacing.md },
  questionTypeText: { color: colors.primaryLight, fontSize: 12, fontWeight: '700' },
  questionText: { fontSize: 17, fontWeight: '700', color: colors.text, lineHeight: 24, marginBottom: spacing.md },
  optionBtn: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, marginBottom: spacing.sm, borderWidth: 1, borderColor: colors.border },
  optionSelected: { borderColor: colors.primary, backgroundColor: colors.primary + '15' },
  radio: { width: 20, height: 20, borderRadius: 10, borderWidth: 2, borderColor: colors.textMuted, marginRight: spacing.md },
  radioSelected: { borderColor: colors.primary, backgroundColor: colors.primary },
  optionText: { fontSize: 15, color: colors.text, flex: 1 },
  textArea: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, color: colors.text, fontSize: 15, height: 80, borderWidth: 1, borderColor: colors.border },
  submitBtn: { backgroundColor: colors.primary, paddingVertical: 14, borderRadius: 12, alignItems: 'center', marginTop: spacing.md },
  submitBtnText: { color: '#fff', fontWeight: '700', fontSize: 16 },
  scoreCard: { backgroundColor: colors.primary, borderRadius: 16, padding: spacing.lg, alignItems: 'center', marginBottom: spacing.lg },
  scoreNum: { fontSize: 36, fontWeight: '800', color: '#fff' },
  scoreLabel: { color: '#ffffffbb', fontSize: 14 },
  resultCard: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, marginBottom: spacing.md, borderWidth: 1, borderColor: colors.border },
  resultQ: { fontSize: 15, fontWeight: '700', color: colors.text, marginBottom: spacing.sm },
  resultAnswer: { fontSize: 13, color: colors.textSecondary, marginBottom: spacing.sm },
  resultFeedbackBox: { backgroundColor: colors.surfaceLight, borderRadius: 8, padding: spacing.sm },
  resultPoints: { fontSize: 13, fontWeight: '700', color: colors.success, marginBottom: 4 },
  resultFeedback: { fontSize: 13, color: colors.textSecondary, lineHeight: 18 },
  gradingText: { color: colors.textSecondary, marginTop: spacing.md, fontSize: 16 },
  markdownText: { color: colors.text, fontSize: 15, lineHeight: 24 },
  // FAB
  fab: {
    position: 'absolute',
    bottom: spacing.xl,
    right: spacing.xl,
    backgroundColor: colors.primary,
    width: 60,
    height: 60,
    borderRadius: 30,
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 5,
    elevation: 8,
  },
  fabIcon: {
    fontSize: 28,
  },
  // Flashcard Styles
  fcToggleRow: {
    flexDirection: 'row',
    padding: spacing.md,
    backgroundColor: colors.surfaceLight,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  fcToggleBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
  },
  fcToggleActive: {
    backgroundColor: colors.primary + '20',
  },
  fcToggleText: {
    color: colors.textSecondary,
    fontWeight: '600',
    fontSize: 14,
  },
  fcToggleTextActive: {
    color: colors.primary,
    fontWeight: '700',
  },
  fcCard: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  fcContent: {
    padding: spacing.xl,
    minHeight: 180,
    justifyContent: 'center',
    alignItems: 'center',
  },
  fcTextFront: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.text,
    textAlign: 'center',
    lineHeight: 26,
  },
  fcTextBack: {
    fontSize: 16,
    color: colors.textSecondary,
    textAlign: 'center',
    lineHeight: 24,
  },
  fcActions: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  fcBtnNeedsReview: {
    flex: 1,
    paddingVertical: 16,
    alignItems: 'center',
    backgroundColor: colors.surfaceLight,
  },
  fcBtnGotIt: {
    flex: 1,
    paddingVertical: 16,
    alignItems: 'center',
    backgroundColor: colors.success,
  },
  fcBtnTextDark: {
    color: colors.text,
    fontWeight: '700',
    fontSize: 15,
  },
  fcBtnTextLight: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 15,
  },
  perfContainer: {
    marginTop: spacing.xl,
    padding: spacing.lg,
    backgroundColor: colors.surfaceLight,
    borderRadius: 12,
    alignItems: 'center',
    width: '100%',
    borderWidth: 1,
    borderColor: colors.border,
  },
  perfTitle: {
    fontSize: 14,
    color: colors.textMuted,
    fontWeight: '600',
    marginBottom: 4,
  },
  perfScore: {
    fontSize: 24,
    fontWeight: '800',
    color: colors.primary,
    marginBottom: spacing.md,
  },
  resetBtn: {
    backgroundColor: colors.error + '20',
    paddingVertical: 10,
    paddingHorizontal: spacing.xl,
    borderRadius: 8,
  },
  resetBtnText: {
    color: colors.error,
    fontWeight: '700',
  },
  offlineBtn: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
    backgroundColor: colors.primary + '20',
  },
  offlineBtnText: {
    color: colors.primary,
    fontSize: 12,
    fontWeight: '700',
  }
});
