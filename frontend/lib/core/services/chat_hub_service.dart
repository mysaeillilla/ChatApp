import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../constants/api_constants.dart';
import '../models/message_model.dart';
import '../utils/date_utils.dart';
import 'token_storage.dart';

class TypingEvent {
  final String conversationId;
  final String userId;

  TypingEvent({required this.conversationId, required this.userId});
}

class PresenceEvent {
  final String userId;
  final bool isOnline;
  final DateTime? lastSeen;

  PresenceEvent({
    required this.userId,
    required this.isOnline,
    required this.lastSeen,
  });
}

/// One shared SignalR connection for the whole app.
///
/// Server -> client events are exposed as broadcast streams.
/// Client -> server calls are plain async methods.
class ChatHubService {
  ChatHubService._();

  static final ChatHubService instance = ChatHubService._();

  HubConnection? _connection;
  Future<void>? _starting;

  // ---------------------------------------------------------------
  // Server -> client event streams
  // ---------------------------------------------------------------

  final StreamController<MessageModel> _messages =
      StreamController<MessageModel>.broadcast();

  final StreamController<TypingEvent> _typing =
      StreamController<TypingEvent>.broadcast();

  final StreamController<PresenceEvent> _presence =
      StreamController<PresenceEvent>.broadcast();

  final StreamController<void> _reconnected =
      StreamController<void>.broadcast();

  /// Incoming messages.
  /// Can be listened to by multiple widgets.
  Stream<MessageModel> get onMessage => _messages.stream;

  Stream<TypingEvent> get onTyping => _typing.stream;

  Stream<PresenceEvent> get onPresence => _presence.stream;

  Stream<void> get onReconnected => _reconnected.stream;

  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  // ---------------------------------------------------------------
  // Connection
  // ---------------------------------------------------------------

  /// Safe to call many times; concurrent callers share the same attempt.
  Future<void> connect() {
    if (isConnected) return Future.value();

    return _starting ??= _start().whenComplete(() => _starting = null);
  }

  Future<void> _start() async {
    final hub = _connection ??= _build();

    if (hub.state == HubConnectionState.Disconnected) {
      await hub.start();
    }
  }

  Future<void> disconnect() async {
    final hub = _connection;
    _connection = null;

    await hub?.stop();
  }

  HubConnection _build() {
    final hub = HubConnectionBuilder()
        .withUrl(
          ApiConstants.hubUrl,
          options: HttpConnectionOptions(
            // Sent as ?access_token=... on the WebSocket.
            accessTokenFactory: () async =>
                (await TokenStorage.getToken()) ?? '',
          ),
        )
        .withAutomaticReconnect()
        .build();

    // -------------------------------------------------------------
    // ReceiveMessage
    // -------------------------------------------------------------

    hub.on('ReceiveMessage', (args) {
      final map = _firstMap(args);

      if (map != null) {
        final message = MessageModel.fromJson(map);

        // Publish incoming message to all listeners.
        _messages.add(message);
      }
    });

    // -------------------------------------------------------------
    // UserTyping
    // -------------------------------------------------------------

    hub.on('UserTyping', (args) {
      final map = _firstMap(args);

      if (map == null) return;

      _typing.add(
        TypingEvent(
          conversationId: map['conversationId'] as String,
          userId: map['userId'] as String,
        ),
      );
    });

    // -------------------------------------------------------------
    // PresenceChanged
    // -------------------------------------------------------------

    hub.on('PresenceChanged', (args) {
      final map = _firstMap(args);

      if (map == null) return;

      final lastSeen = map['lastSeen'] as String?;

      _presence.add(
        PresenceEvent(
          userId: map['userId'] as String,
          isOnline: map['isOnline'] as bool,
          lastSeen: lastSeen == null ? null : parseServerDate(lastSeen),
        ),
      );
    });

    // -------------------------------------------------------------
    // Reconnected
    // -------------------------------------------------------------

    hub.onreconnected(({connectionId}) => _reconnected.add(null));

    return hub;
  }

  // ---------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------

  Map<String, dynamic>? _firstMap(List<Object?>? args) {
    if (args == null || args.isEmpty) return null;

    final first = args.first;

    if (first is Map) {
      return Map<String, dynamic>.from(first);
    }

    return null;
  }

  Future<Object?> _invoke(String method, List<Object> args) async {
    await connect();

    return _connection!.invoke(method, args: args);
  }

  // ---------------------------------------------------------------
  // Client -> server
  // ---------------------------------------------------------------

  /// Hub: SendMessage(SendMessageRequest).
  /// Returns the saved message.
  Future<MessageModel> sendMessage(
    String conversationId,
    String content, {
    String? replyToMessageId,
  }) async {
    final result = await _invoke('SendMessage', [
      {
        'conversationId': conversationId,
        'content': content,
        if (replyToMessageId != null) 'replyToMessageId': replyToMessageId,
      },
    ]);

    return MessageModel.fromJson(Map<String, dynamic>.from(result as Map));
  }

  /// Hub: MarkRead(conversationId).
  Future<void> markRead(String conversationId) async {
    await _invoke('MarkRead', [conversationId]);
  }

  /// Hub: Typing(conversationId).
  Future<void> typing(String conversationId) async {
    await _invoke('Typing', [conversationId]);
  }

  // ---------------------------------------------------------------
  // Stream cleanup
  // ---------------------------------------------------------------

  /// Call only when permanently disposing the shared service,
  /// not when navigating away from individual screens.
  Future<void> dispose() async {
    await disconnect();

    await _messages.close();
    await _typing.close();
    await _presence.close();
    await _reconnected.close();
  }
}
