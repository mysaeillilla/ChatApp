import 'package:dio/dio.dart';

/// Turns a DioException into a readable message
/// (reads the { "error": "..." } / { "message": "..." } bodies the API returns).
String apiErrorMessage(DioException e) {
  final data = e.response?.data;

  if (data is Map) {
    return data['error']?.toString() ??
        data['message']?.toString() ??
        'Something went wrong.';
  }

  if (e.response?.statusCode == 401) {
    return 'Your session has expired. Please log in again.';
  }

  return e.message ?? 'Unable to connect to server.';
}
