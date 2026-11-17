import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../core/services/chat_service.dart'; // adjust paths

/// Attach this key to MaterialApp so snackbars can be shown without a context.
final rootMessengerKey = GlobalKey<ScaffoldMessengerState>();

class UnreadNotifier {
  static bool _running = false;
  static bool _shownThisSession = false;

  /// Call from logout so the next login shows unread messages again.
  static void reset() => _shownThisSession = false;

  /// Call once from the screen shown after login.
  static Future<void> check() async {
    if (_running || _shownThisSession) return;
    _running = true;

    try {
      final conversations = await ChatService().getConversations();
      final unread = conversations.where((c) => c.unreadCount > 0).toList();
      _shownThisSession = true; // checked once, even if nothing is unread
      if (unread.isEmpty) return;

      final total = unread.fold<int>(0, (sum, c) => sum + c.unreadCount);

      final String title;
      final String body;

      if (unread.length == 1) {
        final c = unread.first;
        final last = c.lastMessage;

        title = (last?.senderName.isNotEmpty ?? false)
            ? last!.senderName
            : 'New message';

        final preview = (last?.messageType ?? 'text') == 'text'
            ? (last?.content ?? '')
            : 'Sent an attachment';

        body = c.unreadCount == 1
            ? preview
            : '${c.unreadCount} new messages: $preview';
      } else {
        title = 'You have $total unread messages';

        final names = unread
            .map((c) => c.lastMessage?.senderName ?? '')
            .where((n) => n.isNotEmpty)
            .toSet()
            .take(3)
            .join(', ');

        body = names.isEmpty ? 'Open your inbox to read them' : 'From $names';
      }

      _show(title, body);
    } catch (e) {
      debugPrint('UnreadNotifier failed: $e');
    } finally {
      _running = false;
    }
  }

  static void _show(String title, String body) {
    final messenger = rootMessengerKey.currentState;
    if (messenger == null) {
      debugPrint('UnreadNotifier: no ScaffoldMessenger yet');
      return;
    }

    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 5),
          backgroundColor: const Color(0xFF252525),
          margin: const EdgeInsets.all(12),
          content: Row(
            children: [
              const CircleAvatar(
                backgroundColor: Colors.blue,
                child: Icon(Icons.message, color: Colors.white),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      body,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(color: Colors.white70),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
  }
}
