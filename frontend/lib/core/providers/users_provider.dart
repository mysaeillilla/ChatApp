import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../models/message_model.dart';
import '../models/user_model.dart';
import '../services/chat_hub_service.dart';
import '../services/chat_service.dart';
import '../services/user_service.dart';
import 'auth_provider.dart';

final usersProvider = ChangeNotifierProvider.autoDispose<UsersProvider>((ref) {
  final me = ref.read(authProvider).authResponse!.user.id;
  return UsersProvider(currentUserId: me)..init();
});

class UsersProvider extends ChangeNotifier {
  static const _pageSize = 20;

  UsersProvider({required this.currentUserId});

  final String currentUserId;

  final UserService _userService = UserService();
  final ChatService _chatService = ChatService();
  final ChatHubService _hub = ChatHubService.instance;

  final List<UserModel> users = [];

  /// otherUserId -> number of unread messages from that user
  final Map<String, int> unreadByUser = {};

  bool isLoading = false;
  bool isLoadingMore = false;
  bool hasMore = true;
  String? errorMessage;

  String _search = '';
  int _page = 1;
  int _requestId = 0;
  Timer? _debounce;
  StreamSubscription<PresenceEvent>? _presenceSub;
  StreamSubscription<MessageModel>? _messageSub;
  StreamSubscription<void>? _reconnectSub;
  bool _disposed = false;

  int unreadFor(String userId) => unreadByUser[userId] ?? 0;

  void init() {
    // Live online/offline dots
    _presenceSub = _hub.onPresence.listen(_onPresence);

    // Live unread bells
    _messageSub = _hub.onMessage.listen(_onMessage);
    _reconnectSub = _hub.onReconnected.listen((_) => loadUnread());

    // Connecting also marks this user as online for everybody else
    unawaited(_hub.connect().catchError((_) {}));

    loadFirstPage();
    loadUnread();
  }

  // ---------------------------------------------------------------
  // Unread messages
  // ---------------------------------------------------------------

  /// Reads unreadCount from GET api/chat/conversations.
  Future<void> loadUnread() async {
    try {
      final conversations = await _chatService.getConversations();

      unreadByUser
        ..clear()
        ..addEntries(
          conversations
              .where(
                (c) => !c.isGroup && c.otherUserId != null && c.unreadCount > 0,
              )
              .map((c) => MapEntry(c.otherUserId!, c.unreadCount)),
        );

      notifyListeners();
    } catch (_) {
      // The bell is a nice-to-have; don't break the list if this fails.
    }
  }

  void clearUnread(String userId) {
    if (unreadByUser.remove(userId) != null) notifyListeners();
  }

  void _onMessage(MessageModel message) {
    if (message.senderId == currentUserId) return;

    unreadByUser[message.senderId] = unreadFor(message.senderId) + 1;
    notifyListeners();
  }

  // ---------------------------------------------------------------
  // Users list
  // ---------------------------------------------------------------

  void onSearchChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () {
      _search = value.trim();
      loadFirstPage();
    });
  }

  Future<void> loadFirstPage() async {
    final requestId = ++_requestId;
    _page = 1;
    isLoading = true;
    isLoadingMore = false;
    errorMessage = null;
    notifyListeners();

    try {
      final result = await _userService.getUsers(
        search: _search,
        page: 1,
        pageSize: _pageSize,
      );
      if (requestId != _requestId) return;

      users
        ..clear()
        ..addAll(result.users);
      hasMore = result.hasMore;
    } catch (e) {
      if (requestId != _requestId) return;
      errorMessage = e.toString();
    } finally {
      if (requestId == _requestId) {
        isLoading = false;
        notifyListeners();
      }
    }
  }

  Future<void> loadMore() async {
    if (isLoading || isLoadingMore || !hasMore) return;

    final requestId = _requestId;
    isLoadingMore = true;
    notifyListeners();

    try {
      final result = await _userService.getUsers(
        search: _search,
        page: _page + 1,
        pageSize: _pageSize,
      );
      if (requestId != _requestId) return;

      _page++;
      users.addAll(result.users);
      hasMore = result.hasMore;
    } catch (_) {
      // Keep what is already loaded; the user can scroll again to retry.
    } finally {
      if (requestId == _requestId) {
        isLoadingMore = false;
        notifyListeners();
      }
    }
  }

  /// Returns the conversation id for a 1:1 chat with [userId] (creating it if needed).
  Future<String> startChat(String userId) {
    return _chatService.startDirectConversation(userId);
  }

  void _onPresence(PresenceEvent event) {
    final index = users.indexWhere((u) => u.id == event.userId);
    if (index == -1) return;

    users[index] = users[index].copyWith(
      isOnline: event.isOnline,
      lastSeen: event.lastSeen,
    );
    notifyListeners();
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _debounce?.cancel();
    _presenceSub?.cancel();
    _messageSub?.cancel();
    _reconnectSub?.cancel();
    super.dispose();
  }
}
