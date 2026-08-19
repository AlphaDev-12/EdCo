import React, { useState, useContext, useEffect, useMemo } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, ActivityIndicator, KeyboardAvoidingView, Platform, ScrollView, Image } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../services/apiService';
import { useTheme } from '../context/ThemeContext';

export default function RegisterScreen({ navigation }) {
  const { register, login } = useContext(AuthContext);
  const { colors } = useTheme();
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [gradeLevelId, setGradeLevelId] = useState(null);
  
  const [grades, setGrades] = useState([]);
  const [loading, setLoading] = useState(false);
  const [fetchingGrades, setFetchingGrades] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchGrades = async () => {
      try {
        const res = await fetch(`${BASE_URL}/Auth/grade-levels`);
        const data = await res.json();
        if (data.success && Array.isArray(data.data) && data.data.length > 0) {
          setGrades(data.data);
          setGradeLevelId(data.data[0].id);
        } else {
          const defaultGrades = [{ id: 1, name: 'Form 1' }, { id: 2, name: 'Form 4' }];
          setGrades(defaultGrades);
          setGradeLevelId(defaultGrades[0].id);
        }
      } catch (err) {
        console.error("Failed to fetch grades", err);
        const defaultGrades = [{ id: 1, name: 'Form 1' }, { id: 2, name: 'Form 4' }];
        setGrades(defaultGrades);
        setGradeLevelId(defaultGrades[0].id);
      } finally {
        setFetchingGrades(false);
      }
    };
    fetchGrades();
  }, []);

  const handleRegister = async () => {
    if (!fullName || !email || !password || !gradeLevelId) {
      setError('Please fill in all fields and select a grade.');
      return;
    }
    setError('');
    setLoading(true);
    try {
      await register(fullName, email, password, gradeLevelId);
      await login(email, password);
    } catch (e) {
      setError(e.message || 'Registration failed.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.safeArea}>
      <KeyboardAvoidingView 
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        style={styles.container}
      >
        <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={styles.scrollContent}>
          <View style={styles.header}>
            <View style={styles.logoContainer}>
              <Image 
                source={require('../../assets/logo.png')} 
                style={styles.logo} 
                resizeMode="contain"
              />
            </View>
            <Text style={styles.title}>Join EdCo</Text>
            <Text style={styles.subtitle}>Create an account to personalize your learning.</Text>
          </View>

          <View style={styles.form}>
            {error ? <Text style={styles.errorText}>{error}</Text> : null}
            
            <Text style={styles.label}>Full Name</Text>
            <TextInput
              style={styles.input}
              placeholder="John Doe"
              placeholderTextColor={colors.textMuted}
              value={fullName}
              onChangeText={setFullName}
            />

            <Text style={styles.label}>Email Address</Text>
            <TextInput
              style={styles.input}
              placeholder="student@edco.edu"
              placeholderTextColor={colors.textMuted}
              autoCapitalize="none"
              keyboardType="email-address"
              value={email}
              onChangeText={setEmail}
            />

            <Text style={styles.label}>Password</Text>
            <View style={styles.passwordWrapper}>
              <TextInput
                style={styles.passwordInput}
                placeholder="••••••••"
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

            <Text style={styles.label}>Select Your Grade Level</Text>
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

            <TouchableOpacity style={styles.button} onPress={handleRegister} disabled={loading || fetchingGrades}>
              {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Sign Up</Text>}
            </TouchableOpacity>

            <View style={styles.footer}>
              <Text style={styles.footerText}>Already have an account? </Text>
              <TouchableOpacity onPress={() => navigation.navigate('Login')}>
                <Text style={styles.footerLink}>Log In</Text>
              </TouchableOpacity>
            </View>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  container: { flex: 1 },
  scrollContent: { padding: 24, paddingBottom: 40, flexGrow: 1, justifyContent: 'center' },
  header: { marginBottom: 24, alignItems: 'center' },
  logoContainer: { marginBottom: 12, alignItems: 'center' },
  logo: { width: 100, height: 100, resizeMode: 'contain' },
  title: { fontSize: 26, fontWeight: '800', color: colors.text, marginBottom: 8, textAlign: 'center' },
  subtitle: { fontSize: 15, color: colors.textSecondary, lineHeight: 22, textAlign: 'center' },
  form: { width: '100%' },
  label: { fontSize: 14, fontWeight: '600', color: colors.text, marginBottom: 8 },
  input: {
    backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderRadius: 12,
    padding: 16, fontSize: 16, color: colors.text, marginBottom: 20,
  },
  passwordWrapper: {
    flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface,
    borderWidth: 1, borderColor: colors.border, borderRadius: 12, marginBottom: 20,
  },
  passwordInput: { flex: 1, padding: 16, fontSize: 16, color: colors.text },
  eyeButton: { paddingHorizontal: 14, paddingVertical: 12 },
  eyeButtonText: { fontSize: 13, fontWeight: '700', color: colors.primary },
  gradeScrollContainer: { marginVertical: 8, flexGrow: 0, width: '100%' },
  gradeContainer: { flexDirection: 'row', alignItems: 'center', paddingVertical: 4, paddingBottom: 16 },
  gradePill: {
    paddingHorizontal: 20, paddingVertical: 12, borderRadius: 24,
    backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, marginRight: 12,
  },
  gradePillActive: {
    backgroundColor: colors.primary, borderColor: colors.primary,
  },
  gradePillText: { color: colors.textSecondary, fontWeight: '600' },
  gradePillTextActive: { color: '#fff' },
  button: { backgroundColor: colors.primary, padding: 16, borderRadius: 12, alignItems: 'center', marginTop: 8 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: 'bold' },
  errorText: { color: '#dc3545', marginBottom: 16, textAlign: 'center' },
  footer: { flexDirection: 'row', justifyContent: 'center', marginTop: 24 },
  footerText: { color: colors.textSecondary },
  footerLink: { color: colors.primary, fontWeight: 'bold' }
});
