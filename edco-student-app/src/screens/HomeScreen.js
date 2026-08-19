import React, { useEffect, useState, useContext, useCallback, useMemo } from 'react';
import {
  View, Text, TouchableOpacity, FlatList, StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { spacing } from '../theme';
import { getSubjects } from '../services/apiService';
import { AuthContext } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { useFocusEffect } from '@react-navigation/native';
import { getOfflineUnits } from '../services/offlineService';

export default function HomeScreen({ navigation }) {
  const { userData } = useContext(AuthContext);
  const { colors } = useTheme();
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [offlineUnits, setOfflineUnits] = useState([]);

  const loadOfflineUnits = useCallback(async () => {
    try {
      const units = await getOfflineUnits();
      setOfflineUnits(units);
    } catch (e) {
      console.error(e);
    }
  }, []);

  const loadSubjects = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getSubjects();
      setSubjects(data);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      loadOfflineUnits();
      loadSubjects();
    }, [loadOfflineUnits, loadSubjects])
  );

  const subjectIcons = ['📐', '🔬', '📖', '🌍', '🎨', '💻', '🧮', '🎵'];

  const renderSubject = ({ item, index }) => (
    <TouchableOpacity
      style={styles.card}
      activeOpacity={0.7}
      onPress={() => navigation.navigate('SubjectDetail', { subject: item })}
    >
      <View style={styles.cardIcon}>
        <Text style={styles.iconText}>{subjectIcons[index % subjectIcons.length]}</Text>
      </View>
      <View style={styles.cardContent}>
        <Text style={styles.cardTitle}>{item.name}</Text>
        <Text style={styles.cardSubtitle}>Tap to view chapters & units</Text>
      </View>
      <Text style={styles.chevron}>›</Text>
    </TouchableOpacity>
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.greeting}>Hello, {userData?.fullName?.split(' ')[0] || 'Student'}! 👋</Text>
          <TouchableOpacity onPress={() => navigation.navigate('Profile')} style={styles.profileBtn}>
            <Text style={styles.profileIconText}>👤</Text>
          </TouchableOpacity>
        </View>
        <Text style={styles.headerTitle}>Your Subjects</Text>
        <Text style={styles.headerSubtitle}>Choose a subject to start learning</Text>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={styles.loadingText}>Loading subjects...</Text>
        </View>
      ) : error ? (
        <View style={styles.center}>
          <Text style={styles.errorEmoji}>😕</Text>
          <Text style={styles.errorText}>{error}</Text>
          <TouchableOpacity style={styles.retryBtn} onPress={loadSubjects}>
            <Text style={styles.retryText}>Retry</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <FlatList
          data={subjects}
          keyExtractor={(item) => item.id.toString()}
          renderItem={renderSubject}
          contentContainerStyle={styles.list}
          showsVerticalScrollIndicator={false}
          ListFooterComponent={() => {
            if (offlineUnits.length === 0) return null;
            return (
              <View style={styles.offlineSection}>
                <Text style={styles.offlineHeader}>Downloaded Units</Text>
                <Text style={styles.offlineSubHeader}>Available offline</Text>
                {offlineUnits.map(unit => (
                  <TouchableOpacity
                    key={`offline_${unit.id}`}
                    style={styles.offlineCard}
                    activeOpacity={0.7}
                    onPress={() => navigation.navigate('UnitDetail', { unitId: unit.id, isOffline: true })}
                  >
                    <View style={styles.cardIconOffline}>
                      <Text style={styles.iconText}>💾</Text>
                    </View>
                    <View style={styles.cardContent}>
                      <Text style={styles.cardTitle}>{unit.title}</Text>
                      <Text style={styles.cardSubtitle}>Saved: {new Date(unit.downloadedAt).toLocaleDateString()}</Text>
                    </View>
                    <Text style={styles.chevron}>›</Text>
                  </TouchableOpacity>
                ))}
              </View>
            );
          }}
        />
      )}
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: 10,
    paddingBottom: spacing.md,
  },
  greeting: { fontSize: 14, color: colors.textSecondary, marginBottom: 8 },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  profileBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.surface,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border,
    cursor: 'pointer',
  },
  profileIconText: {
    fontSize: 18,
  },
  headerTitle: { fontSize: 28, fontWeight: '800', color: colors.text },
  headerSubtitle: { fontSize: 14, color: colors.textMuted, marginTop: spacing.xs },
  list: { paddingHorizontal: spacing.lg, paddingBottom: spacing.xl },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: 16,
    padding: spacing.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    cursor: 'pointer',
  },
  cardIcon: {
    width: 52,
    height: 52,
    borderRadius: 14,
    backgroundColor: colors.surfaceLight,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
  iconText: { fontSize: 24 },
  cardContent: { flex: 1 },
  cardTitle: { fontSize: 17, fontWeight: '700', color: colors.text },
  cardSubtitle: { fontSize: 13, color: colors.textMuted, marginTop: 2 },
  chevron: { fontSize: 24, color: colors.textMuted, fontWeight: '300' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  loadingText: { color: colors.textSecondary, marginTop: spacing.md },
  errorEmoji: { fontSize: 48, marginBottom: spacing.md },
  errorText: { color: colors.textSecondary, fontSize: 16, textAlign: 'center', marginBottom: spacing.md },
  retryBtn: {
    backgroundColor: colors.primary,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    borderRadius: 10,
  },
  retryText: { color: '#fff', fontWeight: '700' },
  offlineSection: {
    marginTop: spacing.xl,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border,
  },
  offlineHeader: { fontSize: 22, fontWeight: '800', color: colors.text },
  offlineSubHeader: { fontSize: 14, color: colors.textMuted, marginTop: 4, marginBottom: spacing.md },
  offlineCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surfaceLight,
    borderRadius: 16,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border,
    borderStyle: 'dashed',
  },
  cardIconOffline: {
    width: 52,
    height: 52,
    borderRadius: 14,
    backgroundColor: colors.surface,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
});
