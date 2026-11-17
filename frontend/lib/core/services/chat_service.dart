import 'package:dio/dio.dart';

import '../constants/api_constants.dart';
import '../models/conversation_model.dart';
import '../models/message_model.dart';
import '../network/api_client.dart';
import '../network/api_error.dart';

/// REST part of the chat (inbox, history, opening a conversation).
/// Live messaging goes through ChatHubService (SignalR).
class ChatService {
  final Dio _dio = ApiClient.dio;

  /// GET api/chat/conversations
  /// Each item has unreadCount, lastMessage and the other user's id.
  Future<List<ConversationModel>> getConversations() async {
    try {
      final response = await _dio.get(ApiConstants.conversations);

      return (response.data as List)
          .map(
            (e) =>
                ConversationModel.fromJson(Map<String, dynamic>.from(e as Map)),
          )
          .toList();
    } on DioException catch (e) {
      throw apiErrorMessage(e);
    }
  }

  /// POST api/chat/conversations/direct/{otherUserId}
  /// Returns the existing 1:1 conversation id, or creates one.
  Future<String> startDirectConversation(String otherUserId) async {
    try {
      final response = await _dio.post(
        '${ApiConstants.directConversation}/$otherUserId',
      );
      return response.data['conversationId'] as String;
    } on DioException catch (e) {
      throw apiErrorMessage(e);
    }
  }

  /// GET api/chat/conversations/{id}/messages?before=&take=
  /// Returned oldest -> newest. Pass [before] (sentAt of the oldest loaded
  /// message) to load the previous page.
  Future<List<MessageModel>> getMessages(
    String conversationId, {
    DateTime? before,
    int take = 30,
  }) async {
    try {
      final response = await _dio.get(
        ApiConstants.conversationMessages(conversationId),
        queryParameters: {
          'take': take,
          if (before != null) 'before': before.toUtc().toIso8601String(),
        },
      );

      return (response.data as List)
          .map(
            (e) => MessageModel.fromJson(Map<String, dynamic>.from(e as Map)),
          )
          .toList();
    } on DioException catch (e) {
      throw apiErrorMessage(e);
    }
  }
}
