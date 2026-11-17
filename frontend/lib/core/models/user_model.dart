import '../utils/date_utils.dart';

class UserModel {
  final String id;
  final String username;
  final String email;
  final String? profileImage;
  final bool isOnline;
  final DateTime? lastSeen;

  UserModel({
    required this.id,
    required this.username,
    required this.email,
    this.profileImage,
    this.isOnline = false,
    this.lastSeen,
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    final lastSeen = json['lastSeen'] as String?;

    return UserModel(
      id: json['id'],
      username: json['username'],
      email: json['email'],
      profileImage: json['profileImage'],
      isOnline: json['isOnline'] ?? false,
      lastSeen: lastSeen == null ? null : parseServerDate(lastSeen),
    );
  }

  UserModel copyWith({bool? isOnline, DateTime? lastSeen}) {
    return UserModel(
      id: id,
      username: username,
      email: email,
      profileImage: profileImage,
      isOnline: isOnline ?? this.isOnline,
      lastSeen: lastSeen ?? this.lastSeen,
    );
  }
}
