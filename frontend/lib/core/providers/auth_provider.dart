import 'package:flutter/material.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../models/auth_response.dart';
import '../services/auth_service.dart';

final authProvider = ChangeNotifierProvider<AuthProvider>((ref) {
  return AuthProvider();
});

class AuthProvider extends ChangeNotifier {
  final AuthService _service = AuthService();

  bool isLoading = false;
  String? errorMessage;
  AuthResponse? authResponse;

  Future<bool> login(String usernameOrEmail, String password) async {
    return _execute(() async {
      authResponse = await _service.login(
        usernameOrEmail: usernameOrEmail,
        password: password,
      );
    });
  }

  Future<bool> register(String username, String email, String password) async {
    return _execute(() async {
      authResponse = await _service.register(
        username: username,
        email: email,
        password: password,
      );
    });
  }

  Future<bool> _execute(Future<void> Function() action) async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();

    try {
      await action();
      return true;
    } catch (e) {
      errorMessage = e.toString();
      return false;
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    await _service.logout();
    authResponse = null;
    errorMessage = null;
    notifyListeners();
  }

  void clearError() {
    errorMessage = null;
    notifyListeners();
  }
}
