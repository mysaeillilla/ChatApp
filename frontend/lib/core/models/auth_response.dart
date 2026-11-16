import 'user_model.dart';

class AuthResponse {
  final String token;
  final DateTime expiresAt;
  final UserModel user;

  AuthResponse({
    required this.token,
    required this.expiresAt,
    required this.user,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      token: json['token'],
      expiresAt: DateTime.parse(json['expiresAt']),
      user: UserModel.fromJson(json['user']),
    );
  }
}
