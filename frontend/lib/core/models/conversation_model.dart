import 'message_model.dart';

class ConversationModel {
  final String id;
  final String? name;
  final bool isGroup;
  final String? otherUserId;
  final String? otherUsername;
  final bool otherIsOnline;
  final MessageModel? lastMessage;
  final int unreadCount;

  ConversationModel({
    required this.id,
    this.name,
    required this.isGroup,
    this.otherUserId,
    this.otherUsername,
    this.otherIsOnline = false,
    this.lastMessage,
    this.unreadCount = 0,
  });

  factory ConversationModel.fromJson(Map<String, dynamic> json) {
    final last = json['lastMessage'];

    return ConversationModel(
      id: json['id'],
      name: json['name'],
      isGroup: json['isGroup'] ?? false,
      otherUserId: json['otherUserId'],
      otherUsername: json['otherUsername'],
      otherIsOnline: json['otherIsOnline'] ?? false,
      lastMessage: last == null
          ? null
          : MessageModel.fromJson(Map<String, dynamic>.from(last as Map)),
      unreadCount: json['unreadCount'] ?? 0,
    );
  }
}
