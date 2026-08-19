import React, { useEffect, useState } from 'react';
import {
  View, Text, TouchableOpacity, ScrollView, StyleSheet,
  ActivityIndicator, FlatList, Alert
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing } from '../theme';
import { getQuantitativeSessions, deleteQuantitativeSession } from '../services/apiService';

export default function QuantitativeTutorHistoryScreen({ route, navigation }) {
  const { subjectId, subjectTitle } = route.params;
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadSessions();
  }, []);

  const loadSessions = async () => {
    try {
      setLoading(true);
      const data = await getQuantitativeSessions(subjectId);
      setSessions(data);
    } catch (e) {
      console.error('Failed to load sessions', e);
    } finally {
      setLoading(false);
    }
  };

  const navigateToChat = (sessionId = null) => {
    navigation.navigate('QuantitativeTutor', { 
      subjectId, 
      subjectTitle,
      sessionId 
    });
  };

  const handleDeleteSession = (sessionId) => {
    Alert.alert(
      "Delete Chat",
      "Are you sure you want to delete this conversation? This cannot be undone.",
      [
        { text: "Cancel", style: "cancel" },
        { 
          text: "Delete", 
          style: "destructive",
          onPress: async () => {
            try {
              await deleteQuantitativeSession(sessionId);
              setSessions(prev => prev.filter(s => s.id !== sessionId));
            } catch (e) {
              Alert.alert("Error", "Failed to delete the chat.");
            }
          }
        }
      ]
    );
  };

  const renderSessionItem = ({ item }) => {
    const date = new Date(item.lastInteractionAt);
    const dateString = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    
    return (
      <TouchableOpacity 
        style={styles.sessionCard}
        onPress={() => navigateToChat(item.id)}
      >
        <View style={styles.sessionIcon}>
          <Text style={styles.sessionIconText}>💬</Text>
        </View>
        <View style={styles.sessionInfo}>
          <Text style={styles.sessionTopic} numberOfLines={1}>{item.topic}</Text>
          <Text style={styles.sessionDate}>{dateString}</Text>
        </View>
        <TouchableOpacity 
          style={styles.deleteBtn} 
          onPress={(e) => {
            e.stopPropagation();
            handleDeleteSession(item.id);
          }}
        >
          <Ionicons name="trash-outline" size={20} color={colors.error} />
        </TouchableOpacity>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backBtn}>
            <Text style={styles.backText}>← Back</Text>
          </TouchableOpacity>
        </View>
        <Text style={styles.headerTitle}>Tutor History</Text>
        <Text style={styles.headerSubtitle}>{subjectTitle}</Text>
      </View>

      <View style={styles.actionsContainer}>
        <TouchableOpacity style={styles.newChatBtn} onPress={() => navigateToChat(null)}>
          <Text style={styles.newChatBtnText}>+ Start New Chat</Text>
        </TouchableOpacity>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      ) : sessions.length === 0 ? (
        <View style={styles.emptyState}>
          <Text style={styles.emptyIcon}>🤖</Text>
          <Text style={styles.emptyTitle}>No previous chats</Text>
          <Text style={styles.emptySub}>Start a new conversation to get help with your studies.</Text>
        </View>
      ) : (
        <FlatList
          data={sessions}
          keyExtractor={item => item.id}
          renderItem={renderSessionItem}
          contentContainerStyle={styles.listContent}
          showsVerticalScrollIndicator={false}
        />
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: 10,
    paddingBottom: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  backBtn: {},
  backText: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  headerTitle: { fontSize: 26, fontWeight: '800', color: colors.text },
  headerSubtitle: { fontSize: 14, color: colors.textMuted, marginTop: 2 },
  actionsContainer: {
    padding: spacing.lg,
  },
  newChatBtn: {
    backgroundColor: colors.primary,
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: 'center',
    shadowColor: colors.primary,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 4,
  },
  newChatBtnText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '700',
  },
  listContent: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.xl,
  },
  sessionCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    padding: spacing.md,
    borderRadius: 12,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
  },
  sessionIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.surfaceLight,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
  sessionIconText: { fontSize: 18 },
  sessionInfo: { flex: 1 },
  sessionTopic: { fontSize: 16, fontWeight: '600', color: colors.text, marginBottom: 4 },
  sessionDate: { fontSize: 13, color: colors.textMuted },
  deleteBtn: {
    padding: spacing.sm,
    marginLeft: spacing.sm,
  },
  emptyState: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.xl,
  },
  emptyIcon: { fontSize: 48, marginBottom: spacing.md },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: colors.text, marginBottom: spacing.sm },
  emptySub: { fontSize: 14, color: colors.textMuted, textAlign: 'center', lineHeight: 20 },
});
