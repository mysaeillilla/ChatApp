import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/message_model.dart';
import '../models/user_model.dart';
import '../providers/chat_provider.dart';
import '../utils/date_utils.dart';
import '../widgets/user_avatar.dart';

class ChatScreen extends ConsumerStatefulWidget {
  final String conversationId;
  final UserModel user;

  const ChatScreen({
    super.key,
    required this.conversationId,
    required this.user,
  });

  @override
  ConsumerState<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends ConsumerState<ChatScreen> {
  final _messageController = TextEditingController();
  final _scrollController = ScrollController();
  final _focusNode = FocusNode();

  late final ChatArgs _args;

  @override
  void initState() {
    super.initState();

    _args = ChatArgs(
      conversationId: widget.conversationId,
      otherUser: widget.user,
    );

    // The list is reversed, so "top" (older messages) is maxScrollExtent.
    _scrollController.addListener(() {
      final position = _scrollController.position;
      if (position.pixels >= position.maxScrollExtent - 200) {
        ref.read(chatProvider(_args)).loadMore();
      }
    });
  }

  @override
  void dispose() {
    _messageController.dispose();
    _scrollController.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _messageController.text.trim();
    if (text.isEmpty) return;

    final sent = await ref.read(chatProvider(_args)).send(text);

    if (!mounted || !sent) return;

    _messageController.clear();

    if (_scrollController.hasClients) {
      _scrollController.animateTo(
        0,
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    }
  }

  String _statusText(ChatProvider chat) {
    if (chat.otherTyping) return 'typing...';
    if (chat.otherOnline) return 'online';
    return lastSeenText(chat.otherLastSeen);
  }

  @override
  Widget build(BuildContext context) {
    final chat = ref.watch(chatProvider(_args));
    final highlight = chat.otherTyping || chat.otherOnline;

    return Scaffold(
      appBar: AppBar(
        titleSpacing: 0,
        title: Row(
          children: [
            UserAvatar(
              name: widget.user.username,
              imageUrl: widget.user.profileImage,
              radius: 18,
              isOnline: chat.otherOnline,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.user.username,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  Text(
                    _statusText(chat),
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      fontStyle: chat.otherTyping
                          ? FontStyle.italic
                          : FontStyle.normal,
                      color: highlight ? Colors.green : Colors.grey,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      body: Column(
        children: [
          Expanded(child: _buildMessages(chat)),
          if (chat.errorMessage != null)
            Container(
              width: double.infinity,
              color: Colors.red.shade50,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(
                chat.errorMessage!,
                style: const TextStyle(color: Colors.red, fontSize: 13),
              ),
            ),
          _buildInputBar(chat),
        ],
      ),
    );
  }

  Widget _buildMessages(ChatProvider chat) {
    if (chat.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (chat.messages.isEmpty) {
      return Center(
        child: Text(
          'No messages yet.\nSay hi to ${widget.user.username}!',
          textAlign: TextAlign.center,
          style: const TextStyle(color: Colors.grey),
        ),
      );
    }

    final messages = chat.messages;

    return ListView.builder(
      controller: _scrollController,
      reverse: true,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      itemCount: messages.length + (chat.isLoadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        // Loader for older messages sits at the very top
        if (index == messages.length) {
          return const Padding(
            padding: EdgeInsets.all(12),
            child: Center(
              child: SizedBox(
                width: 22,
                height: 22,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          );
        }

        // reverse: true  ->  index 0 is the newest message
        final i = messages.length - 1 - index;
        final message = messages[i];
        final showDay =
            i == 0 || !isSameDay(messages[i - 1].sentAt, message.sentAt);

        return Column(
          children: [
            if (showDay) _DayChip(label: dayLabel(message.sentAt)),
            _MessageBubble(
              message: message,
              isMe: message.senderId == chat.currentUserId,
            ),
          ],
        );
      },
    );
  }

  Widget _buildInputBar(ChatProvider chat) {
    return SafeArea(
      top: false,
      child: Container(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: Colors.grey.shade200)),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: TextField(
                controller: _messageController,
                focusNode: _focusNode,
                minLines: 1,
                maxLines: 5, // long messages still wrap and grow the box
                // "text" (not "multiline") makes Enter a submit action
                // instead of a new line, on mobile and desktop/web.
                keyboardType: TextInputType.text,
                textInputAction: TextInputAction.send,
                textCapitalization: TextCapitalization.sentences,
                onChanged: chat.onTextChanged,
                // Keep the keyboard open / field focused after sending.
                onEditingComplete: () {},
                onSubmitted: (_) => _send(),
                decoration: InputDecoration(
                  hintText: 'Type a message',
                  filled: true,
                  fillColor: Colors.grey.shade100,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 18,
                    vertical: 12,
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(24),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
            ),
            const SizedBox(width: 8),
            CircleAvatar(
              radius: 24,
              backgroundColor: Colors.blue,
              child: chat.isSending
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : IconButton(
                      icon: const Icon(Icons.send, color: Colors.white),
                      onPressed: _send,
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class _DayChip extends StatelessWidget {
  final String label;

  const _DayChip({required this.label});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        decoration: BoxDecoration(
          color: Colors.grey.shade200,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          label,
          style: TextStyle(fontSize: 12, color: Colors.grey.shade700),
        ),
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  final MessageModel message;
  final bool isMe;

  const _MessageBubble({required this.message, required this.isMe});

  @override
  Widget build(BuildContext context) {
    final maxWidth = MediaQuery.of(context).size.width * 0.75;

    return Align(
      alignment: isMe ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        constraints: BoxConstraints(maxWidth: maxWidth),
        margin: const EdgeInsets.symmetric(vertical: 3),
        padding: const EdgeInsets.fromLTRB(14, 8, 14, 6),
        decoration: BoxDecoration(
          color: isMe ? Colors.blue : Colors.grey.shade200,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(16),
            topRight: const Radius.circular(16),
            bottomLeft: Radius.circular(isMe ? 16 : 4),
            bottomRight: Radius.circular(isMe ? 4 : 16),
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Align(
              alignment: Alignment.centerLeft,
              child: Text(
                message.content,
                style: TextStyle(
                  fontSize: 15,
                  color: isMe ? Colors.white : Colors.black87,
                ),
              ),
            ),
            const SizedBox(height: 3),
            Text(
              formatClock(message.sentAt),
              style: TextStyle(
                fontSize: 11,
                color: isMe ? Colors.white70 : Colors.grey.shade600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
