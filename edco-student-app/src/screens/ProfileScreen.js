import React, { useState, useContext, useEffect, useMemo } from 'react';
import { 
  View, Text, TextInput, TouchableOpacity, StyleSheet, 
  ActivityIndicator, KeyboardAvoidingView, Platform, ScrollView, Alert
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AuthContext } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { BASE_URL, updateProfile, getPerformance, getAiUsage } from '../services/apiService';
import { spacing } from '../theme';

export default function ProfileScreen({ navigation }) {
  const { userData, logout, updateUserLocally } = useContext(AuthContext);
  const { theme, isDark, colors, setTheme } = useTheme();
  
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [fullName, setFullName] = useState(userData?.fullName || '');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [gradeLevelId, setGradeLevelId] = useState(userData?.gradeLevelId || null);
  
  const [grades, setGrades] = useState([]);
  const [fetchingGrades, setFetchingGrades] = useState(true);
  
  const [performanceData, setPerformanceData] = useState(null);
  const [loadingPerformance, setLoadingPerformance] = useState(true);
  
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  const [aiUsage, setAiUsage] = useState(null);
  const [loadingAiUsage, setLoadingAiUsage] = useState(true);

  useEffect(() => {
    const fetchGrades = async () => {
      try {
        const res = await fetch(`${BASE_URL}/Auth/grade-levels`);
        const data = await res.json();
        if (data.success && Array.isArray(data.data) && data.data.length > 0) {
          setGrades(data.data);
        } else {
          setGrades([{ id: 1, name: 'Form 1' }, { id: 2, name: 'Form 4' }]);
        }
      } catch (err) {
        console.error("Failed to fetch grades", err);
        setGrades([{ id: 1, name: 'Form 1' }, { id: 2, name: 'Form 4' }]);
      } finally {
        setFetchingGrades(false);
      }
    };

    const fetchPerformance = async () => {
      try {
        const data = await getPerformance();
        setPerformanceData(data);
      } catch (err) {
        console.error("Failed to fetch performance", err);
      } finally {
        setLoadingPerformance(false);
      }
    };

    const fetchAiUsage = async () => {
      try {
        const data = await getAiUsage();
        if (data.success) {
          setAiUsage(data.data);
        }
      } catch (err) {
        console.error("Failed to fetch AI usage", err);
      } finally {
        setLoadingAiUsage(false);
      }
    };

    fetchGrades();
    fetchPerformance();
    fetchAiUsage();
  }, []);

  const handleSave = async () => {
    setMessage('');
    setSaving(true);
    try {
      const payload = { 
        fullName, 
        gradeLevelId 
      };
      if (password) {
        payload.password = password;
      }
      
      const res = await updateProfile(payload);
      if (res.success) {
        await updateUserLocally({ ...res.user, token: res.token });
        setMessage('Profile updated successfully!');
        setPassword(''); // clear password field
      }
    } catch (e) {
      Alert.alert('Error', e.message || 'Failed to update profile.');
    } finally {
      setSaving(false);
    }
  };

  const handleLogout = () => {
    Alert.alert('Log Out', 'Are you sure you want to log out?', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Log Out', style: 'destructive', onPress: logout }
    ]);
  };

  const handleDeleteAccount = () => {
    Alert.alert(
      'Delete Account',
      'Are you sure you want to permanently delete your StudyPro account and personal data? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        { 
          text: 'Delete Account', 
          style: 'destructive', 
          onPress: () => {
            Alert.alert(
              'Account Deletion Requested', 
              'Your account deletion request has been submitted. You will now be logged out.',
              [{ text: 'OK', onPress: logout }]
            );
          } 
        }
      ]
    );
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'No active subscription';
    const d = new Date(dateString);
    if (d < new Date()) return 'Expired on ' + d.toLocaleDateString();
    return d.toLocaleDateString();
  };

  const renderPerformanceItem = (item) => {
    const percent = item.totalAttempts > 0 ? Math.round((item.correct / item.totalAttempts) * 100) : 0;
    return (
      <View key={item.subjectId || item.unitId} style={styles.perfCard}>
        <View style={styles.perfHeader}>
          <Text style={styles.perfTitle}>{item.subjectName} {item.unitTitle ? `- ${item.unitTitle}` : ''}</Text>
          <Text style={styles.perfPercent}>{percent}%</Text>
        </View>
        <View style={styles.progressBarBg}>
          <View style={[styles.progressBarFill, { width: `${percent}%`, backgroundColor: percent >= 70 ? colors.primary : '#ff9f43' }]} />
        </View>
        <Text style={styles.perfStats}>{item.correct} correct out of {item.totalAttempts} attempts</Text>
      </View>
    );
  };

  return (
    <SafeAreaView style={styles.safeArea}>
      <KeyboardAvoidingView 
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.container}
      >
        <View style={styles.header}>
          <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
            <Text style={styles.backBtnText}>←</Text>
          </TouchableOpacity>
          <Text style={styles.headerTitle}>Your Profile</Text>
          <View style={{ width: 40 }} />
        </View>

        <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scrollContent}>
          
          {/* App Appearance Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>App Appearance</Text>
            <View style={styles.themeToggleContainer}>
              <TouchableOpacity
                style={[
                  styles.themeOption,
                  !isDark && styles.themeOptionActive
                ]}
                onPress={() => setTheme('light')}
                activeOpacity={0.8}
              >
                <Text style={styles.themeOptionIcon}>☀️</Text>
                <Text style={[styles.themeOptionText, !isDark && styles.themeOptionTextActive]}>Light Theme</Text>
              </TouchableOpacity>

              <TouchableOpacity
                style={[
                  styles.themeOption,
                  isDark && styles.themeOptionActive
                ]}
                onPress={() => setTheme('dark')}
                activeOpacity={0.8}
              >
                <Text style={styles.themeOptionIcon}>🌙</Text>
                <Text style={[styles.themeOptionText, isDark && styles.themeOptionTextActive]}>Dark Theme</Text>
              </TouchableOpacity>
            </View>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Account Details</Text>
            
            {message ? <Text style={styles.successText}>{message}</Text> : null}

            <Text style={styles.label}>Full Name</Text>
            <TextInput
              style={styles.input}
              placeholder="Your Name"
              placeholderTextColor={colors.textMuted}
              value={fullName}
              onChangeText={setFullName}
            />

            <Text style={styles.label}>New Password (Optional)</Text>
            <View style={styles.passwordWrapper}>
              <TextInput
                style={styles.passwordInput}
                placeholder="Leave blank to keep current"
                placeholderTextColor={colors.textMuted}
                secureTextEntry={!showPassword}
                value={password}
                onChangeText={setPassword}
              />
              <TouchableOpacity 
                style={styles.eyeButton} 
                onPress={() => setShowPassword(!showPassword)}
                activeOpacity={0.7}
              >
                <Text style={styles.eyeButtonText}>{showPassword ? '👁️ Hide' : '👁️ View'}</Text>
              </TouchableOpacity>
            </View>

            <Text style={styles.label}>Grade Level</Text>
            {fetchingGrades ? (
               <ActivityIndicator color={colors.primary} style={{ marginVertical: 12 }} />
            ) : (
              <ScrollView 
                horizontal 
                showsHorizontalScrollIndicator={false} 
                style={styles.gradeScrollContainer}
                contentContainerStyle={styles.gradeContainer}
              >
                {grades.map(grade => (
                  <TouchableOpacity 
                    key={grade.id} 
                    style={[styles.gradePill, gradeLevelId === grade.id && styles.gradePillActive]}
                    onPress={() => setGradeLevelId(grade.id)}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.gradePillText, gradeLevelId === grade.id && styles.gradePillTextActive]}>
                      {grade.name}
                    </Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            )}

            <TouchableOpacity style={styles.button} onPress={handleSave} disabled={saving}>
              {saving ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Save Changes</Text>}
            </TouchableOpacity>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Subscription</Text>
            <View style={styles.subCard}>
              <Text style={styles.subStatus}>
                Status: {userData?.isSubscribed ? <Text style={styles.activeText}>Active</Text> : <Text style={styles.inactiveText}>Inactive</Text>}
              </Text>
              <Text style={styles.subDate}>
                Deadline: {formatDate(userData?.subscriptionEndDate)}
              </Text>
            </View>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Monthly AI Usage</Text>
            {loadingAiUsage ? (
              <ActivityIndicator color={colors.primary} />
            ) : (
              <View style={styles.perfCard}>
                <View style={styles.perfHeader}>
                  <Text style={styles.perfTitle}>Token Usage</Text>
                  <Text style={styles.perfPercent}>
                    {aiUsage ? Math.min(Math.round((aiUsage.costUsed / aiUsage.costLimit) * 100), 100) : 0}%
                  </Text>
                </View>
                <View style={styles.progressBarBg}>
                  <View 
                    style={[
                      styles.progressBarFill, 
                      { 
                        width: `${aiUsage ? Math.min(Math.round((aiUsage.costUsed / aiUsage.costLimit) * 100), 100) : 0}%`, 
                        backgroundColor: (aiUsage && (aiUsage.costUsed / aiUsage.costLimit) > 0.8) ? '#dc3545' : colors.primary 
                      }
                    ]} 
                  />
                </View>
                <Text style={styles.perfStats}>
                  Of monthly free tier allowance
                </Text>
              </View>
            )}
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Subject Performance</Text>
            {loadingPerformance ? (
              <ActivityIndicator color={colors.primary} style={{ marginTop: 20 }} />
            ) : performanceData?.subjectPerformance && performanceData.subjectPerformance.length > 0 ? (
              performanceData.subjectPerformance.map(renderPerformanceItem)
            ) : (
              <Text style={styles.emptyText}>No quizzes completed yet.</Text>
            )}
          </View>

          <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
            <Text style={styles.logoutButtonText}>Log Out</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.deleteButton} onPress={handleDeleteAccount}>
            <Text style={styles.deleteButtonText}>Delete Account & Data</Text>
          </TouchableOpacity>

        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  container: { flex: 1 },
  header: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingHorizontal: spacing.lg, paddingVertical: spacing.md,
    borderBottomWidth: 1, borderBottomColor: colors.border,
  },
  backBtn: { width: 40, height: 40, justifyContent: 'center' },
  backBtnText: { fontSize: 24, color: colors.text, fontWeight: 'bold' },
  headerTitle: { fontSize: 20, fontWeight: '700', color: colors.text },
  scrollContent: { padding: spacing.lg, paddingBottom: 60 },
  section: {
    backgroundColor: colors.surface, borderRadius: 16, padding: spacing.lg,
    marginBottom: spacing.lg, borderWidth: 1, borderColor: colors.border,
  },
  sectionTitle: { fontSize: 18, fontWeight: '800', color: colors.text, marginBottom: spacing.md },
  
  themeToggleContainer: { flexDirection: 'row', gap: 12 },
  themeOption: {
    flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    paddingVertical: 14, paddingHorizontal: 12, borderRadius: 12,
    backgroundColor: colors.surfaceLight, borderWidth: 1, borderColor: colors.border, gap: 8,
  },
  themeOptionActive: {
    backgroundColor: colors.primary + '18', borderColor: colors.primary, borderWidth: 2,
  },
  themeOptionIcon: { fontSize: 18 },
  themeOptionText: { fontSize: 14, fontWeight: '600', color: colors.textSecondary },
  themeOptionTextActive: { color: colors.primary, fontWeight: '700' },

  label: { fontSize: 14, fontWeight: '600', color: colors.text, marginBottom: 8, marginTop: 8 },
  input: {
    backgroundColor: colors.surfaceLight, borderWidth: 1, borderColor: colors.border, borderRadius: 10,
    padding: 14, fontSize: 16, color: colors.text,
  },
  passwordWrapper: {
    flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surfaceLight,
    borderWidth: 1, borderColor: colors.border, borderRadius: 10,
  },
  passwordInput: { flex: 1, padding: 14, fontSize: 16, color: colors.text },
  eyeButton: { paddingHorizontal: 14, paddingVertical: 12 },
  eyeButtonText: { fontSize: 13, fontWeight: '700', color: colors.primary },
  gradeScrollContainer: { marginVertical: 8, flexGrow: 0, width: '100%' },
  gradeContainer: { flexDirection: 'row', alignItems: 'center', paddingVertical: 4 },
  gradePill: {
    paddingHorizontal: 16, paddingVertical: 10, borderRadius: 20,
    backgroundColor: colors.surfaceLight, borderWidth: 1, borderColor: colors.border, marginRight: 8,
  },
  gradePillActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  gradePillText: { color: colors.textSecondary, fontWeight: '600' },
  gradePillTextActive: { color: '#fff' },
  button: { backgroundColor: colors.primary, padding: 14, borderRadius: 10, alignItems: 'center', marginTop: 16 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: 'bold' },
  successText: { color: colors.success || '#28a745', marginBottom: 12, fontWeight: 'bold', textAlign: 'center' },
  
  subCard: { backgroundColor: colors.surfaceLight, padding: 16, borderRadius: 12 },
  subStatus: { fontSize: 16, fontWeight: '600', color: colors.text, marginBottom: 4 },
  subDate: { fontSize: 14, color: colors.textSecondary },
  activeText: { color: colors.success || '#28a745' },
  inactiveText: { color: colors.error || '#dc3545' },
  
  perfCard: {
    backgroundColor: colors.surfaceLight, borderRadius: 12, padding: 16, marginBottom: 12,
  },
  perfHeader: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 },
  perfTitle: { fontSize: 16, fontWeight: '700', color: colors.text, flex: 1 },
  perfPercent: { fontSize: 16, fontWeight: '800', color: colors.primary, marginLeft: 8 },
  progressBarBg: { height: 8, backgroundColor: colors.border, borderRadius: 4, overflow: 'hidden', marginBottom: 8 },
  progressBarFill: { height: '100%', borderRadius: 4 },
  perfStats: { fontSize: 12, color: colors.textMuted },
  emptyText: { color: colors.textMuted, fontStyle: 'italic', textAlign: 'center', marginVertical: 10 },
  
  logoutButton: { 
    backgroundColor: colors.surfaceLight, borderWidth: 1, borderColor: '#ff6b6b',
    padding: 16, borderRadius: 12, alignItems: 'center', marginTop: spacing.md, marginBottom: spacing.xs
  },
  logoutButtonText: { color: '#ff6b6b', fontSize: 16, fontWeight: 'bold' },
  deleteButton: {
    padding: 14, alignItems: 'center', marginBottom: spacing.xl
  },
  deleteButtonText: { color: colors.error || '#dc3545', fontSize: 14, fontWeight: '600', textDecorationLine: 'underline' }
});

