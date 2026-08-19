import React, { useState, useContext, useEffect, useMemo } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, ActivityIndicator, Linking } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../services/apiService';
import { useTheme } from '../context/ThemeContext';
import AsyncStorage from '@react-native-async-storage/async-storage';

export default function SubscriptionScreen({ navigation }) {
  const { userData, updateSubscriptionStatus } = useContext(AuthContext);
  const { colors } = useTheme();
  const styles = useMemo(() => getStyles(colors), [colors]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [isChecking, setIsChecking] = useState(false);
  const [tierPrice, setTierPrice] = useState(0);
  const [durationDays, setDurationDays] = useState(90);
  const [pollUrl, setPollUrl] = useState('');

  // When user returns to the app, check if subscription was paid
  const checkStatus = async () => {
    setIsChecking(true);
    try {
      const token = await AsyncStorage.getItem('userToken');
      
      // If we have a pollUrl, ask the backend to manually verify with Paynow
      if (pollUrl) {
        const verifyRes = await fetch(`${BASE_URL}/Subscription/verify`, {
          method: 'POST',
          headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
          body: JSON.stringify({ pollUrl })
        });
        const verifyData = await verifyRes.json();
        if (verifyData.success && verifyData.isSubscribed) {
          updateSubscriptionStatus(true);
          navigation.goBack(); // Return to where they were before paywall
          return;
        }
      }

      // Always fetch the tier price and fallback status
      const res = await fetch(`${BASE_URL}/Subscription/status`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      if (data.success) {
        setTierPrice(data.tierPrice || 0);
        if (data.subscriptionDurationDays) {
          setDurationDays(data.subscriptionDurationDays);
        }
        if (data.isSubscribed) {
          updateSubscriptionStatus(true);
          navigation.goBack();
        }
      }
    } catch (e) {
      console.log('Error checking subscription status', e);
    } finally {
      setIsChecking(false);
    }
  };

  useEffect(() => {
    checkStatus();
  }, []);

  const handleSubscribe = async () => {
    setLoading(true);
    setError('');
    try {
      const token = await AsyncStorage.getItem('userToken');
      const res = await fetch(`${BASE_URL}/Subscription/initiate`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      if (data.success && data.browserUrl) {
        // Save the pollUrl so we can manually verify later
        setPollUrl(data.pollUrl);
        // Open Paynow checkout page in the mobile browser
        Linking.openURL(data.browserUrl);
      } else {
        throw new Error(data.message || 'Payment initiation failed.');
      }
    } catch (e) {
      setError(e.message || 'Could not connect to payment gateway.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.safeArea}>
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.icon}>🎓</Text>
          <Text style={styles.title}>Unlock Premium Learning</Text>
          <Text style={styles.subtitle}>
            You need an active subscription to access premium curriculum materials, video lessons, and the AI Tutor.
          </Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.planName}>{durationDays % 30 === 0 ? `${Math.round(durationDays / 30)}-Month Pass` : `${durationDays}-Day Pass`}</Text>
          <Text style={styles.price}>${Number(tierPrice || 0).toFixed(2)} <Text style={styles.priceMonth}>/ {durationDays} days</Text></Text>
          
          <View style={styles.features}>
            <Text style={styles.featureItem}>✓ Full Video Lesson Library</Text>
            <Text style={styles.featureItem}>✓ Unlimited AI Tutor Prompts</Text>
            <Text style={styles.featureItem}>✓ Exam Past Papers</Text>
            <Text style={styles.featureItem}>✓ Progress Tracking</Text>
          </View>
        </View>

        {error ? <Text style={styles.errorText}>{error}</Text> : null}

        <View style={styles.actions}>
          <TouchableOpacity style={styles.button} onPress={handleSubscribe} disabled={loading || isChecking}>
            {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Subscribe via Paynow</Text>}
          </TouchableOpacity>

          <TouchableOpacity style={styles.secondaryButton} onPress={checkStatus} disabled={loading || isChecking}>
            {isChecking ? <ActivityIndicator color={colors.primary} /> : <Text style={styles.secondaryButtonText}>I've already paid (Refresh Status)</Text>}
          </TouchableOpacity>

          <TouchableOpacity style={styles.cancelButton} onPress={() => navigation.goBack()}>
            <Text style={styles.cancelButtonText}>Maybe Later</Text>
          </TouchableOpacity>
        </View>
      </View>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  safeArea: { flex: 1, backgroundColor: colors.background },
  container: { flex: 1, padding: 24, justifyContent: 'center' },
  header: { alignItems: 'center', marginBottom: 32 },
  icon: { fontSize: 64, marginBottom: 16 },
  title: { fontSize: 28, fontWeight: '800', color: colors.text, marginBottom: 12, textAlign: 'center' },
  subtitle: { fontSize: 16, color: colors.textSecondary, textAlign: 'center', lineHeight: 24, paddingHorizontal: 16 },
  card: {
    backgroundColor: colors.surface, borderRadius: 24, padding: 32, marginBottom: 32,
    borderWidth: 2, borderColor: colors.primary,
  },
  planName: { fontSize: 18, fontWeight: '700', color: colors.textSecondary, marginBottom: 8 },
  price: { fontSize: 40, fontWeight: '800', color: colors.text, marginBottom: 24 },
  priceMonth: { fontSize: 18, color: colors.textSecondary },
  features: { gap: 12 },
  featureItem: { fontSize: 16, color: colors.text, fontWeight: '500' },
  actions: { width: '100%', gap: 16 },
  button: { backgroundColor: colors.primary, padding: 18, borderRadius: 16, alignItems: 'center' },
  buttonText: { color: '#fff', fontSize: 18, fontWeight: 'bold' },
  secondaryButton: { backgroundColor: colors.surfaceLight, padding: 18, borderRadius: 16, alignItems: 'center', borderWidth: 1, borderColor: colors.border },
  secondaryButtonText: { color: colors.text, fontSize: 16, fontWeight: '600' },
  cancelButton: { padding: 16, alignItems: 'center' },
  cancelButtonText: { color: colors.textSecondary, fontSize: 16, fontWeight: '600' },
  errorText: { color: '#dc3545', textAlign: 'center', marginBottom: 16 }
});
