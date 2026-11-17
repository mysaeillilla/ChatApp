import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../models/message_model.dart';
import '../models/user_model.dart';
import '../services/chat_hub_service.dart';
import '../services/chat_service.dart';
import 'auth_provider.dart';

/// Family key: two args are equal when they point to the same conversation.
class ChatArgs {
  final String conversationId;
  final UserModel otherUser;

  const ChatArgs({required this.conversationId, required this.otherUser});

  @override
  bool operator ==(Object other) =>
      other is ChatArgs && other.conversationId == conversationId;

  @override
  int get hashCode => conversationId.hashCode;
}

final chatProvider = ChangeNotifierProvider.autoDispose
    .family<ChatProvider, ChatArgs>((ref, args) {
      final me = ref.read(authProvider).authResponse!.user.id;
      return ChatProvider(args: args, currentUserId: me)..init();
    });

class ChatProvider extends ChangeNotifier {
  static const _pageSize = 30;

  ChatProvider({required this.args, required this.currentUserId})
    : otherOnline = args.otherUser.isOnline,
      otherLastSeen = args.otherUser.lastSeen;

  final ChatArgs args;
  final String currentUserId;

  final ChatService _service = ChatService();
  final ChatHubService _hub = ChatHubService.instance;

  /// Oldest -> newest
  final List<MessageModel> messages = [];

  bool isLoading = true;
  bool isLoadingMore = false;
  bool hasMore = true;
  bool isSending = false;
  bool otherTyping = false;
  bool otherOnline;
  DateTime? otherLastSeen;
  String? errorMessage;

  StreamSubscription<MessageModel>? _messageSub;
  StreamSubscription<TypingEvent>? _typingSub;
  StreamSubscription<PresenceEvent>? _presenceSub;
  StreamSubscription<void>? _reconnectSub;
  Timer? _typingTimer;
  DateTime? _lastTypingSent;
  bool _disposed = false;

  String get conversationId => args.conversationId;

  // ---------------------------------------------------------------
  // Lifecycle
  // ---------------------------------------------------------------

  Future<void> init() async {
    _messageSub = _hub.onMessage.listen(_onMessage);
    _typingSub = _hub.onTyping.listen(_onTyping);
    _presenceSub = _hub.onPresence.listen(_onPresence);
    _reconnectSub = _hub.onReconnected.listen((_) => _refreshLatest());

    try {
      await _hub.connect();
    } catch (_) {
      errorMessage = 'Could not connect to the chat server.';
    }

    await _loadInitial();
    _safe(() => _hub.markRead(conversationId));
  }

  Future<void> _loadInitial() async {
    try {
      final result = await _service.getMessages(
        conversationId,
        take: _pageSize,
      );
      _merge(result);
      hasMore = result.length == _pageSize;
    } catch (e) {
      errorMessage = e.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  /// Older messages (called when the user scrolls to the top).
  Future<void> loadMore() async {
    if (isLoading || isLoadingMore || !hasMore || messages.isEmpty) return;

    isLoadingMore = true;
    notifyListeners();

    try {
      final result = await _service.getMessages(
        conversationId,
        before: messages.first.sentAt,
        take: _pageSize,
      );
      _merge(result);
      hasMore = result.length == _pageSize;
    } catch (_) {
      // ignore - user can scroll again to retry
    } finally {
      isLoadingMore = false;
      notifyListeners();
    }
  }

  /// After a reconnect, fetch whatever was missed while offline.
  Future<void> _refreshLatest() async {
    try {
      final latest = await _service.getMessages(
        conversationId,
        take: _pageSize,
      );
      _merge(latest);
      _safe(() => _hub.markRead(conversationId));
      notifyListeners();
    } catch (_) {}
  }

  // ---------------------------------------------------------------
  // Sending
  // ---------------------------------------------------------------

  /// Returns true when the message was saved by the server.
  Future<bool> send(String text) async {
    final content = text.trim();
    if (content.isEmpty || isSending) return false;

    isSending = true;
    errorMessage = null;
    notifyListeners();

    try {
      final message = await _hub.sendMessage(conversationId, content);
      _merge([message]); // the same message also arrives via ReceiveMessage
      return true;
    } catch (e) {
      errorMessage = _cleanError(e);
      return false;
    } finally {
      isSending = false;
      notifyListeners();
    }
  }

  /// Call from TextField.onChanged. Throttled to one event every 2 seconds.
  void onTextChanged(String text) {
    if (text.trim().isEmpty) return;

    final now = DateTime.now();
    if (_lastTypingSent != null &&
        now.difference(_lastTypingSent!) < const Duration(seconds: 2)) {
      return;
    }

    _lastTypingSent = now;
    _safe(() => _hub.typing(conversationId));
  }

  // ---------------------------------------------------------------
  // Server events
  // ---------------------------------------------------------------

  void _onMessage(MessageModel message) {
    if (message.conversationId != conversationId) return;

    _merge([message]);

    if (message.senderId != currentUserId) {
      otherTyping = false;
      // The chat is open, so the message is read immediately
      _safe(() => _hub.markRead(conversationId));
    }

    notifyListeners();
  }

  void _onTyping(TypingEvent event) {
    if (event.conversationId != conversationId ||
        event.userId != args.otherUser.id) {
      return;
    }

    otherTyping = true;
    notifyListeners();

    _typingTimer?.cancel();
    _typingTimer = Timer(const Duration(seconds: 3), () {
      otherTyping = false;
      notifyListeners();
    });
  }

  void _onPresence(PresenceEvent event) {
    if (event.userId != args.otherUser.id) return;

    otherOnline = event.isOnline;
    otherLastSeen = event.lastSeen ?? otherLastSeen;
    notifyListeners();
  }

  // ---------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------

  void _merge(List<MessageModel> incoming) {
    final ids = messages.map((m) => m.id).toSet();
    for (final message in incoming) {
      if (ids.add(message.id)) messages.add(message);
    }
    messages.sort((a, b) => a.sentAt.compareTo(b.sentAt));
  }

  void _safe(Future<void> Function() action) {
    action().catchError((_) {});
  }

  String _cleanError(Object e) {
    final text = e.toString();
    final index = text.lastIndexOf('HubException:');
    if (index != -1)
      return text.substring(index + 'HubException:'.length).trim();
    return 'Failed to send the message. Please try again.';
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _typingTimer?.cancel();
    _messageSub?.cancel();
    _typingSub?.cancel();
    _presenceSub?.cancel();
    _reconnectSub?.cancel();
    super.dispose();
  }
}
