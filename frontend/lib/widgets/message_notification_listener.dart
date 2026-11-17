import 'package:flutter/material.dart';
import 'package:frontend/core/services/chat_service.dart';

/// One conversation with unread messages.
class UnreadSummary {
  final String senderName;
  final String preview;
  final int count;

  const UnreadSummary({
    required this.senderName,
    required this.preview,
    required this.count,
  });
}

/// Builds the unread list from the existing conversations endpoint.
Future<List<UnreadSummary>> loadUnreadSummaries(ChatService service) async {
  final conversations = await service.getConversations();

  return conversations
      .where((c) => c.unreadCount > 0)
      .map(
        (c) => UnreadSummary(
          // The unread message is from the other user, so the last
          // message's sender is the right name to show.
          senderName: (c.lastMessage?.senderName.isNotEmpty ?? false)
              ? c.lastMessage!.senderName
              : 'New message',
          preview: (c.lastMessage?.messageType ?? 'text') == 'text'
              ? (c.lastMessage?.content ?? '')
              : 'Sent an attachment',
          count: c.unreadCount,
        ),
      )
      .toList();
}

class UnreadMessageNotifier extends StatefulWidget {
  /// Id of the logged-in user. null = logged out.
  final String? currentUserId;

  /// Fetches unread conversations from your API.
  final Future<List<UnreadSummary>> Function() loadUnread;

  final Widget child;

  const UnreadMessageNotifier({
    super.key,
    required this.currentUserId,
    required this.loadUnread,
    required this.child,
  });

  @override
  State<UnreadMessageNotifier> createState() => _UnreadMessageNotifierState();
}

class _UnreadMessageNotifierState extends State<UnreadMessageNotifier> {
  String? _checkedForUserId;

  @override
  void initState() {
    super.initState();
    _checkUnread();
  }

  @override
  void didUpdateWidget(covariant UnreadMessageNotifier oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.currentUserId != widget.currentUserId) {
      // Logged out, or a different user logged in.
      if (widget.currentUserId == null) _checkedForUserId = null;
      _checkUnread();
    }
  }

  Future<void> _checkUnread() async {
    final userId = widget.currentUserId;

    // Only once per login/launch.
    if (userId == null || _checkedForUserId == userId) return;
    _checkedForUserId = userId;

    try {
      final unread = await widget.loadUnread();
      if (!mounted || unread.isEmpty) return;
      _show(unread);
    } catch (_) {
      // Notification is best-effort; never block the app on it.
    }
  }

  void _show(List<UnreadSummary> unread) {
    final messenger = ScaffoldMessenger.maybeOf(context);
    if (messenger == null) return;

    final total = unread.fold<int>(0, (sum, u) => sum + u.count);

    final String title;
    final String body;

    if (unread.length == 1) {
      final u = unread.first;
      title = u.senderName;
      body = u.count == 1 ? u.preview : '${u.count} new messages: ${u.preview}';
    } else {
      title = 'You have $total unread messages';
      final names = unread.map((u) => u.senderName).take(3).join(', ');
      body = unread.length > 3 ? 'From $names and others' : 'From $names';
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

  @override
  Widget build(BuildContext context) => widget.child;
}
