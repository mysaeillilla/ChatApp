import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/user_model.dart';
import '../providers/auth_provider.dart';
import '../providers/users_provider.dart';
import '../services/chat_hub_service.dart';
import '../utils/date_utils.dart';
import '../widgets/user_avatar.dart';
import 'chat_screen.dart';
import 'login_screen.dart';

/// Lists every other user. Tapping one opens (or creates) the 1:1 chat.
/// Users who sent you unread messages get a notification bell with a count.
class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key});

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  String? _openingUserId;

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(() {
      final position = _scrollController.position;
      if (position.pixels >= position.maxScrollExtent - 200) {
        ref.read(usersProvider).loadMore();
      }
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _logout() async {
    await ChatHubService.instance.disconnect();
    await ref.read(authProvider).logout();

    if (!mounted) return;

    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (route) => false,
    );
  }

  Future<void> _openChat(UserModel user) async {
    if (_openingUserId != null) return;

    setState(() => _openingUserId = user.id);

    try {
      final conversationId = await ref.read(usersProvider).startChat(user.id);

      if (!mounted) return;

      await Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) =>
              ChatScreen(conversationId: conversationId, user: user),
        ),
      );

      // Back from the chat: it marked everything as read, so drop the bell
      // right away and then sync with the server.
      if (!mounted) return;
      final users = ref.read(usersProvider);
      users.clearUnread(user.id);
      users.loadUnread();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.toString())));
    } finally {
      if (mounted) setState(() => _openingUserId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final me = ref.watch(authProvider).authResponse?.user;
    final users = ref.watch(usersProvider);

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Chats'),
            if (me != null)
              Text(
                'Signed in as ${me.username}',
                style: const TextStyle(fontSize: 12, color: Colors.grey),
              ),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Logout',
            onPressed: _logout,
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: TextField(
              controller: _searchController,
              onChanged: (value) {
                setState(() {}); // refresh the clear button
                ref.read(usersProvider).onSearchChanged(value);
              },
              decoration: InputDecoration(
                hintText: 'Search users',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.close),
                        onPressed: () {
                          _searchController.clear();
                          setState(() {});
                          ref.read(usersProvider).onSearchChanged('');
                        },
                      ),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
              ),
            ),
          ),
          Expanded(child: _buildList(users)),
        ],
      ),
    );
  }

  Widget _buildList(UsersProvider users) {
    if (users.isLoading && users.users.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (users.errorMessage != null && users.users.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                users.errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.red),
              ),
              const SizedBox(height: 12),
              ElevatedButton(
                onPressed: users.loadFirstPage,
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    if (users.users.isEmpty) {
      return const Center(
        child: Text('No users found.', style: TextStyle(color: Colors.grey)),
      );
    }

    return RefreshIndicator(
      onRefresh: () async {
        await Future.wait([users.loadFirstPage(), users.loadUnread()]);
      },
      child: ListView.separated(
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: users.users.length + (users.isLoadingMore ? 1 : 0),
        separatorBuilder: (_, _) => const Divider(height: 1, indent: 76),
        itemBuilder: (context, index) {
          if (index == users.users.length) {
            return const Padding(
              padding: EdgeInsets.all(16),
              child: Center(child: CircularProgressIndicator()),
            );
          }

          final user = users.users[index];
          final isOpening = _openingUserId == user.id;
          final unread = users.unreadFor(user.id);

          return ListTile(
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 6,
            ),
            leading: UserAvatar(
              name: user.username,
              imageUrl: user.profileImage,
              isOnline: user.isOnline,
            ),
            title: Text(
              user.username,
              style: TextStyle(
                fontWeight: unread > 0 ? FontWeight.w800 : FontWeight.w600,
              ),
            ),
            subtitle: unread > 0
                ? Text(
                    unread == 1 ? '1 new message' : '$unread new messages',
                    style: const TextStyle(
                      color: Colors.blue,
                      fontWeight: FontWeight.w600,
                    ),
                  )
                : Text(
                    user.isOnline ? 'online' : lastSeenText(user.lastSeen),
                    style: TextStyle(
                      color: user.isOnline ? Colors.green : Colors.grey,
                    ),
                  ),
            trailing: isOpening
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : unread > 0
                ? Badge(
                    label: Text(unread > 99 ? '99+' : '$unread'),
                    child: const Icon(
                      Icons.notifications_active,
                      color: Colors.blue,
                      size: 28,
                    ),
                  )
                : const Icon(Icons.chevron_right),
            onTap: () => _openChat(user),
          );
        },
      ),
    );
  }
}
