import React, { useState, useEffect, useRef } from 'react';
import {
  View, Text, TextInput, TouchableOpacity,
  StyleSheet, KeyboardAvoidingView, Platform, ActivityIndicator, Image, ScrollView
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppWebView as WebView } from '../components/AppWebView';
import * as ImagePicker from 'expo-image-picker';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing } from '../theme';
import { createQuantitativeSession, interactQuantitativeTutor, getQuantitativeSessionById } from '../services/apiService';

export default function QuantitativeTutorScreen({ route, navigation }) {
  const { subjectId, subjectTitle, sessionId: paramSessionId } = route.params || {};

  const [sessionId, setSessionId] = useState(paramSessionId || null);
  const [messages, setMessages] = useState([]);
  const [inputText, setInputText] = useState('');
  const [loading, setLoading] = useState(true);
  const [selectedImage, setSelectedImage] = useState(null);
  const [cursorPosition, setCursorPosition] = useState(0);
  const webviewRef = useRef(null);
  
  const mathSymbols = ['+', '-', '×', '÷', '=', '^2', '√', '/', '(', ')', 'π'];

  const insertMathSymbol = (symbol) => {
    const textBefore = inputText.substring(0, cursorPosition);
    const textAfter = inputText.substring(cursorPosition);
    setInputText(textBefore + symbol + textAfter);
    setCursorPosition(cursorPosition + symbol.length);
  };

  useEffect(() => {
    initSession();
  }, []);

  const initSession = async () => {
    try {
      setLoading(true);
      let session;
      
      if (paramSessionId) {
        // Load existing session
        session = await getQuantitativeSessionById(paramSessionId);
        // Ensure state is set
        setSessionId(session.id);
      } else {
        // Start a brand new session
        session = await createQuantitativeSession(subjectId, `Help with ${subjectTitle}`);
        setSessionId(session.id);
      }
      
      if (session.interactions && session.interactions.length > 0) {
        // Map backend interactions to frontend message format
        const history = [{ 
          id: 'welcome', 
          role: 'assistant', 
          content: `Hello! I'm your Math & Science AI Tutor. You can upload a photo of your problem or type it here.` 
        }];
        
        session.interactions.sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp)).forEach(interaction => {
          // Add user message (including legacy MathLive latex if it exists)
          const userContent = interaction.userMessage + 
            (interaction.mathExpressionLatex ? `\n\\[ ${interaction.mathExpressionLatex} \\]` : '');
            
          history.push({
            id: interaction.id + '_user',
            role: 'user',
            content: userContent.trim(),
            imageUri: interaction.uploadedImageUrl
          });
          // Add AI response
          history.push({
            id: interaction.id + '_ai',
            role: 'assistant',
            content: interaction.aiResponse
          });
        });
        setMessages(history);
      } else {
        setMessages([{ 
          id: 'welcome', 
          role: 'assistant', 
          content: `Hello! I'm your Math & Science AI Tutor. You can upload a photo of your problem or type it here.` 
        }]);
      }
    } catch (e) {
      setMessages([{ id: 'error', role: 'assistant', content: 'Failed to connect to the tutor engine.' }]);
    } finally {
      setLoading(false);
    }
  };

  const pickImage = async () => {
    const permissionResult = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permissionResult.granted) {
      alert("Permission to access camera roll is required!");
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      allowsEditing: true,
      quality: 0.4,
      base64: true,
    });

    if (!result.canceled && result.assets && result.assets.length > 0) {
      setSelectedImage({
        uri: result.assets[0].uri,
        base64: `data:image/jpeg;base64,${result.assets[0].base64}`
      });
    }
  };

  const sendMessage = async () => {
    if ((!inputText.trim() && !selectedImage) || loading || !sessionId) return;
    
    const userMsg = inputText.trim() || "Please solve this image.";
    const imgBase64 = selectedImage?.base64;
    const imgUri = selectedImage?.uri;
    
    setInputText('');
    setSelectedImage(null);
    
    const newMsgId = Date.now().toString();
    setMessages(prev => [...prev, { id: newMsgId, role: 'user', content: userMsg, imageUri: imgUri }]);
    setLoading(true);

    try {
      const response = await interactQuantitativeTutor(sessionId, userMsg, imgBase64);
      setMessages(prev => [...prev, { 
        id: response.id || Date.now().toString() + 'ai', 
        role: 'assistant', 
        content: response.aiResponse 
      }]);
    } catch (e) {
      setMessages(prev => [...prev, { 
        id: Date.now().toString() + 'err', 
        role: 'assistant', 
        content: 'Error processing your request.' 
      }]);
    } finally { 
      setLoading(false); 
    }
  };

  const generateHtml = () => {
    // Generate HTML for the WebView containing all messages
    let messagesHtml = '';
    
    messages.forEach(m => {
      const isUser = m.role === 'user';
      const bubbleClass = isUser ? 'user-bubble' : 'ai-bubble';
      
      messagesHtml += `<div class="message-container ${isUser ? 'right' : 'left'}">`;
      messagesHtml += `<div class="bubble ${bubbleClass}">`;
      
      if (m.imageUri) {
        messagesHtml += `<img src="${m.imageUri}" style="max-width: 100%; border-radius: 8px; margin-bottom: 8px;" />`;
      }
      
      let formattedContent = m.content.replace(/\n/g, '<br/>');
      
      // Basic Markdown parsing
      formattedContent = formattedContent.replace(/\*\*(.*?)\*\*/g, '<b>$1</b>');
      formattedContent = formattedContent.replace(/\*(.*?)\*/g, '<i>$1</i>');

      messagesHtml += `<span>${formattedContent}</span>`;
      
      messagesHtml += `</div></div>`;
    });

    if (loading) {
      messagesHtml += `<div class="message-container left"><div class="bubble ai-bubble"><span>Thinking...</span></div></div>`;
    }

    return `
      <!DOCTYPE html>
      <html>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
        <script>
          window.MathJax = {
            tex: {
              inlineMath: [['$', '$'], ['\\\\(', '\\\\)']],
              displayMath: [['$$', '$$'], ['\\\\[', '\\\\]']]
            },
            startup: {
              pageReady: () => {
                return MathJax.startup.defaultPageReady().then(() => {
                  window.scrollTo(0, document.body.scrollHeight);
                });
              }
            }
          };
        </script>
        <script id="MathJax-script" async src="https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js"></script>
        <style>
          body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            background-color: ${colors.background};
            margin: 0;
            padding: 16px;
            color: ${colors.text};
          }
          .message-container {
            display: flex;
            width: 100%;
            margin-bottom: 12px;
          }
          .right {
            justify-content: flex-end;
          }
          .left {
            justify-content: flex-start;
          }
          .bubble {
            max-width: 85%;
            padding: 12px;
            border-radius: 16px;
            font-size: 15px;
            line-height: 1.4;
          }
          .user-bubble {
            background-color: ${colors.primary};
            color: white;
            border-bottom-right-radius: 4px;
          }
          .ai-bubble {
            background-color: ${colors.surface};
            border: 1px solid ${colors.border};
            border-bottom-left-radius: 4px;
          }
        </style>
      </head>
      <body>
        <div id="chat-container">
          ${messagesHtml}
        </div>
        <script>
          window.scrollTo(0, document.body.scrollHeight);
        </script>
      </body>
      </html>
    `;
  };

  return (
    <SafeAreaView style={s.container}>
      <KeyboardAvoidingView style={{flex:1}} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <View style={s.header}>
          <View style={s.headerTextContainer}>
            <Text style={s.title}>🧮 Quantitative Tutor</Text>
            <Text style={s.sub} numberOfLines={1}>{subjectTitle}</Text>
          </View>
          <TouchableOpacity onPress={() => navigation.goBack()} style={s.closeBtn}>
            <Text style={s.back}>✕ Close</Text>
          </TouchableOpacity>
        </View>

        <View style={{flex: 1}}>
          <WebView 
            ref={webviewRef}
            source={{ html: generateHtml(), baseUrl: 'https://localhost' }}
            style={{flex: 1, backgroundColor: colors.background}}
            originWhitelist={['*']}
            javaScriptEnabled={true}
            mixedContentMode="always"
          />
        </View>

        <View style={s.inputWrapper}>
          {selectedImage && (
            <View style={s.imagePreviewContainer}>
              <Image source={{uri: selectedImage.uri}} style={s.imagePreview} />
              <TouchableOpacity style={s.removeImageBtn} onPress={() => setSelectedImage(null)}>
                <Ionicons name="close-circle" size={24} color={colors.error} />
              </TouchableOpacity>
            </View>
          )}
          {/* Math Accessory Toolbar */}
          <View style={s.mathToolbarContainer}>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} keyboardShouldPersistTaps="always">
              {mathSymbols.map((sym, index) => (
                <TouchableOpacity key={index} style={s.mathBtn} onPress={() => insertMathSymbol(sym)}>
                  <Text style={s.mathBtnText}>{sym}</Text>
                </TouchableOpacity>
              ))}
            </ScrollView>
          </View>

          <View style={s.inputRow}>
            <TouchableOpacity style={s.cameraBtn} onPress={pickImage}>
              <Ionicons name="camera" size={24} color={colors.primary} />
            </TouchableOpacity>
            
            <TextInput 
              style={s.input} 
              placeholder="Ask a question or upload math..." 
              placeholderTextColor={colors.textMuted}
              value={inputText} 
              onChangeText={setInputText} 
              onSelectionChange={(event) => setCursorPosition(event.nativeEvent.selection.start)}
              multiline
            />
            
            <TouchableOpacity 
              style={[s.sendBtn, (!inputText.trim() && !selectedImage || loading) && {backgroundColor:colors.surfaceLight}]}
              onPress={sendMessage} 
              disabled={(!inputText.trim() && !selectedImage) || loading}
            >
              <Ionicons name="send" size={20} color={(!inputText.trim() && !selectedImage || loading) ? colors.textMuted : '#fff'} />
            </TouchableOpacity>
          </View>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.background,
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
  inputWrapper: {
    borderTopWidth: 1,
    borderTopColor: colors.border,
    backgroundColor: colors.surface,
  },
  imagePreviewContainer: {
    padding: spacing.md,
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  imagePreview: {
    width: 80,
    height: 80,
    borderRadius: 8,
  },
  removeImageBtn: {
    position: 'absolute',
    top: spacing.sm,
    left: spacing.md + 65,
    backgroundColor: 'white',
    borderRadius: 12,
  },
  mathToolbarContainer: {
    backgroundColor: colors.surfaceLight,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    paddingVertical: 8,
  },
  mathBtn: {
    backgroundColor: colors.surface,
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 6,
    marginHorizontal: 4,
    borderWidth: 1,
    borderColor: colors.border,
  },
  mathBtnText: {
    fontSize: 16,
    color: colors.text,
    fontWeight: '600',
  },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.md,
    paddingBottom: Platform.OS === 'ios' ? spacing.xl : spacing.md,
  },
  cameraBtn: {
    padding: spacing.sm,
  },
  input: {
    flex: 1,
    backgroundColor: colors.surfaceLight,
    borderRadius: 20,
    paddingHorizontal: spacing.md,
    paddingVertical: 10,
    color: colors.text,
    fontSize: 15,
    maxHeight: 100,
    borderWidth: 1,
    borderColor: colors.border,
  },
  sendBtn: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.primary,
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: spacing.sm,
  },
});
