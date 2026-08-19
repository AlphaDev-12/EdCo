import React, { useState, useRef, useMemo } from 'react';
import {
  View, Text, TextInput, TouchableOpacity, ScrollView,
  StyleSheet, KeyboardAvoidingView, Platform, ActivityIndicator,
  StatusBar
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { spacing } from '../theme';
import { useTheme } from '../context/ThemeContext';
import { askAiTutor } from '../services/apiService';

import { Ionicons } from '@expo/vector-icons';

export default function AiTutorScreen({ route, navigation }) {
  const { colors } = useTheme();
  const s = useMemo(() => getStyles(colors), [colors]);

  const { subjectId, subjectTitle, unitId, unitTitle } = route.params || {};
  const isUnitMode = !!unitId;
  const contextTitle = isUnitMode ? unitTitle : subjectTitle;

  const [messages, setMessages] = useState([
    { role: 'assistant', content: `Hi! I'm your AI Tutor for ${contextTitle}. Ask me anything!` },
  ]);
  const [inputText, setInputText] = useState('');
  const [loading, setLoading] = useState(false);
  const scrollRef = useRef();

  const sendMessage = async () => {
    if (!inputText.trim() || loading) return;
    const userMsg = inputText.trim();
    setInputText('');
    setMessages(prev => [...prev, { role: 'user', content: userMsg }]);
    setLoading(true);
    try {
      const data = await askAiTutor(subjectId, unitId, userMsg);
      const reply = data.choices?.[0]?.message?.content || data.reply || 'No response.';
      setMessages(prev => [...prev, { role: 'assistant', content: reply }]);
    } catch (e) {
      let errorMessage = 'Error connecting to AI Tutor. Please try again.';
      if (e.name === 'AbortError' || (e.message && (e.message.includes('Network request failed') || e.message.includes('Network') || e.message.includes('aborted')))) {
        errorMessage = 'It looks like you are offline. Please check your internet connection to use the AI Tutor.';
      }
      setMessages(prev => [...prev, { role: 'assistant', content: errorMessage }]);
    } finally { setLoading(false); }
  };

  const viewSource = (text) => {
    if (!unitId) return;
    navigation.navigate('UnitDetail', { unitId, highlightText: text });
  };

  const renderMessageContent = (m) => {
    if (m.role === 'user') {
      return <Text style={[s.msgText, {color: '#fff'}]}>{m.content}</Text>;
    }
    
    const parts = m.content.split(/(<source>.*?<\/source>)/g);
    return (
      <Text style={[s.msgText, {color: colors.text}]}>
        {parts.map((part, index) => {
          if (part.startsWith('<source>') && part.endsWith('</source>')) {
            const innerText = part.substring(8, part.length - 9);
            return (
              <Text key={index} style={s.sourceBtnText} onPress={() => viewSource(innerText)}>
                {'\n'}[📖 View Source]{'\n'}
              </Text>
            );
          }
          return <Text key={index}>{part}</Text>;
        })}
      </Text>
    );
  };

  return (
    <SafeAreaView style={s.container}>
      <KeyboardAvoidingView style={{flex:1}} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <View style={s.header}>
          <View style={s.headerTextContainer}>
            <Text style={s.title}>🤖 AI Tutor</Text>
            <Text style={s.sub} numberOfLines={1}>{contextTitle}</Text>
          </View>
          <TouchableOpacity onPress={() => navigation.goBack()} style={s.closeBtn}>
            <Text style={s.back}>✕ Close</Text>
          </TouchableOpacity>
        </View>
        <ScrollView ref={scrollRef} style={{flex:1}} contentContainerStyle={s.msgs}
          onContentSizeChange={() => scrollRef.current?.scrollToEnd({animated:true})}>
          {messages.map((m,i) => (
            <View key={i} style={[s.bubble, m.role==='user'?s.userB:s.aiB]}>
              {m.role==='assistant' && <Text style={s.label}>AI Tutor</Text>}
              {renderMessageContent(m)}
            </View>
          ))}
          {loading && <View style={[s.bubble,s.aiB]}><ActivityIndicator color={colors.primary}/></View>}
        </ScrollView>
        <View style={s.inputRow}>
          <TextInput style={s.input} placeholder="Ask a question..." placeholderTextColor={colors.textMuted}
            value={inputText} onChangeText={setInputText} onSubmitEditing={sendMessage} multiline/>
          <TouchableOpacity style={[s.sendBtn, (!inputText.trim()||loading)&&{backgroundColor:colors.surfaceLight}]}
            onPress={sendMessage} disabled={!inputText.trim()||loading}>
            <Ionicons name="send" size={20} color={(!inputText.trim()||loading) ? colors.textMuted : '#fff'} />
          </TouchableOpacity>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const getStyles = (colors) => StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background,
    paddingTop: 10,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.md,
    paddingBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  headerTextContainer: {
    flex: 1,
  },
  closeBtn: {
    paddingLeft: spacing.md,
  },
  back: { color: colors.primary, fontSize: 15, fontWeight: '600' },
  title: { fontSize: 18, fontWeight: '800', color: colors.text },
  sub: { fontSize: 13, color: colors.textMuted },
  msgs:{padding:spacing.lg},
  bubble:{maxWidth:'85%',borderRadius:16,padding:spacing.md,marginBottom:spacing.md},
  userB:{alignSelf:'flex-end',backgroundColor:colors.primary,borderBottomRightRadius:4},
  aiB:{alignSelf:'flex-start',backgroundColor:colors.surface,borderBottomLeftRadius:4,borderWidth:1,borderColor:colors.border},
  label:{fontSize:11,color:colors.primaryLight,fontWeight:'700',marginBottom:4},
  msgText:{fontSize:15,lineHeight:22},
  inputRow:{flexDirection:'row',alignItems:'flex-end',padding:spacing.sm,paddingBottom:spacing.xl,borderTopWidth:1,borderTopColor:colors.border,backgroundColor:colors.surface},
  input:{flex:1,backgroundColor:colors.surfaceLight,borderRadius:20,paddingHorizontal:spacing.md,paddingVertical:10,color:colors.text,fontSize:15,maxHeight:100,borderWidth:1,borderColor:colors.border},
  sendBtn:{width:40,height:40,borderRadius:20,backgroundColor:colors.primary,justifyContent:'center',alignItems:'center',marginLeft:spacing.sm},
  sourceBtnText: {
    color: colors.primary,
    fontWeight: 'bold',
    fontSize: 14,
    marginVertical: 4,
  }
});
