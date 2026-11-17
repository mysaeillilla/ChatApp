import '../utils/date_utils.dart';

class MessageModel {
  final String id;
  final String conversationId;
  final String senderId;
  final String senderName;
  final String content;
  final String messageType;
  final DateTime sentAt;
  final DateTime? editedAt;
  final String? replyToMessageId;

  MessageModel({
    required this.id,
    required this.conversationId,
    required this.senderId,
    required this.senderName,
    required this.content,
    required this.messageType,
    required this.sentAt,
    this.editedAt,
    this.replyToMessageId,
  });

  factory MessageModel.fromJson(Map<String, dynamic> json) {
    final editedAt = json['editedAt'] as String?;

    return MessageModel(
      id: json['id'],
      conversationId: json['conversationId'],
      senderId: json['senderId'],
      senderName: json['senderName'] ?? '',
      content: json['content'] ?? '',
      messageType: json['messageType'] ?? 'text',
      sentAt: parseServerDate(json['sentAt']),
      editedAt: editedAt == null ? null : parseServerDate(editedAt),
      replyToMessageId: json['replyToMessageId'],
    );
  }
}
