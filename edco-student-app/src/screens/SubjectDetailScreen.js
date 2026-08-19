import React, { useEffect, useState, useMemo } from 'react';
import {
  View, Text, TouchableOpacity, ScrollView, StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { spacing } from '../theme';
import { getSubjectManifest, getSubjectExams, resetPerformance } from '../services/apiService';
import { useTheme } from '../context/ThemeContext';

export default function SubjectDetailScreen({ route, navigation }) {
  const { subject } = route.params;
  const { colors } = useTheme();
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [chapters, setChapters] = useState([]);
  const [exams, setExams] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedChapter, setExpandedChapter] = useState(null);
  const [activeTab, setActiveTab] = useState('exams');

  useEffect(() => {
    loadManifest();
  }, []);

  const loadManifest = async () => {
    try {
      const [chapterData, examData] = await Promise.all([
        getSubjectManifest(subject.id).catch(() => []),
        getSubjectExams(subject.id).catch(() => [])
      ]);
      const safeChapters = Array.isArray(chapterData) ? chapterData : (chapterData?.data && Array.isArray(chapterData.data)) ? chapterData.data : [];
      const safeExams = Array.isArray(examData) ? examData : (examData?.data && Array.isArray(examData.data)) ? examData.data : [];
      
      setChapters(safeChapters);
      setExams(safeExams);
      if (safeChapters.length > 0) setExpandedChapter(safeChapters[0].id);
    } catch (e) {
      console.error(e);
      setChapters([]);
      setExams([]);
    } finally {
      setLoading(false);
    }
  };

  const toggleChapter = (id) => {
    setExpandedChapter(expandedChapter === id ? null : id);
  };

  const handleResetSubject = async () => {
    try {
      setLoading(true);
      await resetPerformance({ subjectId: subject.id });
    } catch (e) {
      console.error("Failed to reset subject performance", e);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      </SafeAreaView>
    );
  }

  const safeExamsList = Array.isArray(exams) ? exams : [];

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
            <Text style={styles.backText}>← Back</Text>
          </TouchableOpacity>
          <TouchableOpacity onPress={handleResetSubject} style={styles.resetBtn}>
            <Text style={styles.resetBtnText}>Reset Progress</Text>
          </TouchableOpacity>
        </View>
        <Text style={styles.headerTitle}>{subject.name}</Text>
        <Text style={styles.headerSubtitle}>{(Array.isArray(chapters) ? chapters : []).length} Chapters</Text>
      </View>

      <View style={styles.tabContainer}>
        <TouchableOpacity 
          style={[styles.tabBtn, activeTab === 'exams' && styles.tabActive]} 
          onPress={() => setActiveTab('exams')}
        >
          <Text style={[styles.tabText, activeTab === 'exams' && styles.tabTextActive]}>Exams</Text>
        </TouchableOpacity>
        <TouchableOpacity 
          style={[styles.tabBtn, activeTab === 'chapters' && styles.tabActive]} 
          onPress={() => setActiveTab('chapters')}
        >
          <Text style={[styles.tabText, activeTab === 'chapters' && styles.tabTextActive]}>Chapters</Text>
        </TouchableOpacity>
      </View>

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        {activeTab === 'chapters' && (
          <View style={styles.comingSoonContainer}>
            <Text style={styles.comingSoonIcon}>🚧</Text>
            <Text style={styles.comingSoonTitle}>Chapters Coming Soon!</Text>
            <Text style={styles.comingSoonText}>We are working hard to bring you the best learning materials. Stay tuned!</Text>
          </View>
        )}

        {activeTab === 'exams' && safeExamsList.length === 0 && (
          <View style={styles.emptyState}>
            <Text style={styles.emptyStateText}>No exams available for this subject.</Text>
          </View>
        )}

        {activeTab === 'exams' && safeExamsList.map((exam) => (
          <TouchableOpacity
            key={exam.id}
            style={styles.examCard}
            activeOpacity={0.7}
            onPress={() => navigation.navigate('Quiz', { quizId: exam.id, title: exam.title })}
          >
            <View style={styles.examHeader}>
              <View style={styles.examIcon}>
                <Text style={styles.examIconText}>⭐</Text>
              </View>
              <View style={styles.examInfo}>
                <Text style={styles.examTitle}>{exam.title}</Text>
                <Text style={styles.examSubtitle}>{exam.questionCount} Questions</Text>
              </View>
              <View style={styles.playBtn}>
                <Text style={styles.playBtnText}>Start</Text>
              </View>
            </View>
          </TouchableOpacity>
        ))}
      </ScrollView>

      {/* AI Tutor FAB for Subject Context */}
      <TouchableOpacity 
        style={styles.fab}
        onPress={() => {
          const isQuantitative = subject.name.toLowerCase().includes('math') || 
                                 subject.name.toLowerCase().includes('physics') ||
                                 subject.name.toLowerCase().includes('accounting') ||
                                 subject.name.toLowerCase().includes('chemistry');
                                 
          if (isQuantitative) {
            navigation.navigate('QuantitativeTutorHistory', { subjectId: subject.id, subjectTitle: subject.name });
          } else {
            navigation.navigate('AiTutor', { subjectId: subject.id, subjectTitle: subject.name });
          }
        }}
      >
        <Text style={styles.fabIcon}>🤖</Text>
      </TouchableOpacity>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: 10,
    paddingBottom: spacing.sm,
  },
  backBtn: { marginBottom: spacing.sm },
  backText: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  headerTitle: { fontSize: 26, fontWeight: '800', color: colors.text },
  headerSubtitle: { fontSize: 14, color: colors.textMuted, marginTop: 2 },
  scrollContent: { paddingHorizontal: spacing.lg, paddingBottom: spacing.xl },
  chapterCard: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    overflow: 'hidden',
  },
  chapterHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.md,
  },
  chapterBadge: {
    width: 32,
    height: 32,
    borderRadius: 8,
    backgroundColor: colors.primary,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  chapterBadgeText: { color: '#fff', fontWeight: '800', fontSize: 14 },
  chapterTitle: { flex: 1, fontSize: 16, fontWeight: '700', color: colors.text },
  expandIcon: { fontSize: 18, color: colors.textMuted },
  unitsList: { paddingHorizontal: spacing.md, paddingBottom: spacing.md },
  unitCard: {
    backgroundColor: colors.surfaceLight,
    borderRadius: 12,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  unitHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: spacing.sm },
  unitDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: colors.success,
    marginRight: spacing.sm,
  },
  unitTitle: { fontSize: 14, fontWeight: '600', color: colors.text, flex: 1 },
  unitActions: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 8,
  },
  badgeText: { fontSize: 12, fontWeight: '600' },
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
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  resetBtn: {
    backgroundColor: colors.error + '20',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
  },
  resetBtnText: {
    color: colors.error,
    fontSize: 12,
    fontWeight: '700',
  },
  tabContainer: {
    flexDirection: 'row',
    paddingHorizontal: spacing.lg,
    marginBottom: spacing.md,
    gap: spacing.sm,
  },
  tabBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
  },
  tabActive: {
    backgroundColor: colors.primary,
    borderColor: colors.primary,
  },
  tabText: {
    color: colors.text,
    fontWeight: '600',
  },
  tabTextActive: {
    color: '#fff',
  },
  emptyState: {
    padding: spacing.xl,
    alignItems: 'center',
  },
  emptyStateText: {
    color: colors.textMuted,
    fontSize: 15,
  },
  comingSoonContainer: {
    padding: spacing.xl,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: spacing.xl,
  },
  comingSoonIcon: {
    fontSize: 48,
    marginBottom: spacing.md,
  },
  comingSoonTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text,
    marginBottom: spacing.sm,
  },
  comingSoonText: {
    fontSize: 15,
    color: colors.textMuted,
    textAlign: 'center',
    lineHeight: 22,
  },
  examCard: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    padding: spacing.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.warning + '50',
  },
  examHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  examIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.warning + '20',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
  examIconText: {
    fontSize: 18,
  },
  examInfo: {
    flex: 1,
  },
  examTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.text,
    marginBottom: 2,
  },
  examSubtitle: {
    fontSize: 13,
    color: colors.textMuted,
  },
  playBtn: {
    backgroundColor: colors.primary,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
  },
  playBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 13,
  }
});

