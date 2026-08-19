import React, { useContext, useEffect } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { ActivityIndicator, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

import { AuthProvider, AuthContext } from './src/context/AuthContext';
import { ThemeProvider, useTheme } from './src/context/ThemeContext';
import { initCrashReporter } from './src/services/crashReporter';
import { ErrorBoundary } from './src/components/ErrorBoundary';
import { OfflineBanner } from './src/components/OfflineBanner';
import { WebContainer } from './src/components/WebContainer';

import HomeScreen from './src/screens/HomeScreen';
import SubjectDetailScreen from './src/screens/SubjectDetailScreen';
import AiTutorScreen from './src/screens/AiTutorScreen';
import QuizScreen from './src/screens/QuizScreen';
import UnitDetailScreen from './src/screens/UnitDetailScreen';
import LoginScreen from './src/screens/LoginScreen';
import RegisterScreen from './src/screens/RegisterScreen';
import SubscriptionScreen from './src/screens/SubscriptionScreen';
import QuantitativeTutorScreen from './src/screens/QuantitativeTutorScreen';
import QuantitativeTutorHistoryScreen from './src/screens/QuantitativeTutorHistoryScreen';
import ProfileScreen from './src/screens/ProfileScreen';

const Stack = createNativeStackNavigator();

function AppNavigator() {
  const { isLoading, isLoggedIn } = useContext(AuthContext);
  const { colors } = useTheme();

  if (isLoading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      {isLoggedIn() ? (
        // Main Stack
        <>
          <Stack.Screen name="Home" component={HomeScreen} />
          <Stack.Screen name="SubjectDetail" component={SubjectDetailScreen} />
          <Stack.Screen name="UnitDetail" component={UnitDetailScreen} />
          <Stack.Screen name="AiTutor" component={AiTutorScreen} />
          <Stack.Screen name="QuantitativeTutor" component={QuantitativeTutorScreen} />
          <Stack.Screen name="QuantitativeTutorHistory" component={QuantitativeTutorHistoryScreen} />
          <Stack.Screen name="Quiz" component={QuizScreen} />
          <Stack.Screen name="Subscription" component={SubscriptionScreen} options={{ presentation: 'modal' }} />
          <Stack.Screen name="Profile" component={ProfileScreen} />
        </>
      ) : (
        // Auth Stack
        <>
          <Stack.Screen name="Login" component={LoginScreen} />
          <Stack.Screen name="Register" component={RegisterScreen} />
        </>
      )}
    </Stack.Navigator>
  );
}

function MainApp() {
  const { colors, isDark } = useTheme();

  return (
    <>
      <StatusBar style={isDark ? 'light' : 'dark'} backgroundColor={colors.background} />
      <NavigationContainer>
        <AppNavigator />
      </NavigationContainer>
    </>
  );
}

export default function App() {
  useEffect(() => {
    initCrashReporter();
  }, []);

  return (
    <ErrorBoundary>
      <SafeAreaProvider>
        <ThemeProvider>
          <OfflineBanner />
          <AuthProvider>
            <WebContainer>
              <MainApp />
            </WebContainer>
          </AuthProvider>
        </ThemeProvider>
      </SafeAreaProvider>
    </ErrorBoundary>
  );
}


