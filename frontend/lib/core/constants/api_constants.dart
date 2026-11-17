class ApiConstants {
  static const String baseUrl = 'https://localhost:5001/api';

  // SignalR hub (note: NOT under /api)
  static const String hubUrl = 'https://localhost:5001/hubs/chat';

  // Auth
  static const String login = '/auth/login';
  static const String register = '/auth/register';
  static const String me = '/auth/me';

  // Users
  static const String users = '/users';

  // Chat
  static const String conversations = '/chat/conversations';
  static const String directConversation = '/chat/conversations/direct';

  static String conversationMessages(String conversationId) =>
      '/chat/conversations/$conversationId/messages';
}
