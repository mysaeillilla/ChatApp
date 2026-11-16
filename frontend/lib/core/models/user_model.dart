class UserModel {
  final String id;
  final String username;
  final String email;
  final String? profileImage;

  UserModel({
    required this.id,
    required this.username,
    required this.email,
    this.profileImage,
  });

  factory UserModel.fromJson(Map<String, dynamic> json) {
    return UserModel(
      id: json['id'],
      username: json['username'],
      email: json['email'],
      profileImage: json['profileImage'],
    );
  }
}
