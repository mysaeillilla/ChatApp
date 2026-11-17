import 'package:dio/dio.dart';

import '../constants/api_constants.dart';
import '../models/user_model.dart';
import '../network/api_client.dart';
import '../network/api_error.dart';

class UsersPage {
  final List<UserModel> users;
  final int totalCount;
  final int page;
  final int totalPages;

  UsersPage({
    required this.users,
    required this.totalCount,
    required this.page,
    required this.totalPages,
  });

  bool get hasMore => page < totalPages;
}

class UserService {
  final Dio _dio = ApiClient.dio;

  /// GET api/users?search=&page=&pageSize=   (current user is excluded by the API)
  Future<UsersPage> getUsers({
    String? search,
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final response = await _dio.get(
        ApiConstants.users,
        queryParameters: {
          if (search != null && search.isNotEmpty) 'search': search,
          'page': page,
          'pageSize': pageSize,
        },
      );

      final data = Map<String, dynamic>.from(response.data as Map);

      return UsersPage(
        users: (data['users'] as List)
            .map((e) => UserModel.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
        totalCount: data['totalCount'] ?? 0,
        page: data['page'] ?? page,
        totalPages: data['totalPages'] ?? 1,
      );
    } on DioException catch (e) {
      throw apiErrorMessage(e);
    }
  }
}
