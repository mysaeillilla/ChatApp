import 'package:dio/dio.dart';

import '../constants/api_constants.dart';
import '../network/api_client.dart';
import '../models/auth_response.dart';
import 'token_storage.dart';

class AuthService {
  final Dio _dio = ApiClient.dio;

  Future<AuthResponse> register({
    required String username,
    required String email,
    required String password,
  }) async {
    try {
      final response = await _dio.post(
        ApiConstants.register,
        data: {'username': username, 'email': email, 'password': password},
      );

      final result = AuthResponse.fromJson(response.data);

      await TokenStorage.saveToken(result.token);

      return result;
    } on DioException catch (e) {
      throw _getErrorMessage(e);
    }
  }

  Future<AuthResponse> login({
    required String usernameOrEmail,
    required String password,
  }) async {
    try {
      final response = await _dio.post(
        ApiConstants.login,
        data: {'usernameOrEmail': usernameOrEmail, 'password': password},
      );

      final result = AuthResponse.fromJson(response.data);

      await TokenStorage.saveToken(result.token);

      return result;
    } on DioException catch (e) {
      throw _getErrorMessage(e);
    }
  }

  String _getErrorMessage(DioException e) {
    final data = e.response?.data;

    if (data is Map) {
      return data['error']?.toString() ??
          data['message']?.toString() ??
          'Something went wrong.';
    }

    return e.message ?? 'Unable to connect to server.';
  }

  Future<void> logout() async {
    await TokenStorage.deleteToken();
  }
}
