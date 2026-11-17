/// The API returns UTC timestamps (usually with a trailing "Z").
/// This makes sure they are always parsed as UTC and returned in local time.
DateTime parseServerDate(String value) {
  final hasZone =
      value.endsWith('Z') || RegExp(r'[+-]\d{2}:\d{2}$').hasMatch(value);
  return DateTime.parse(hasZone ? value : '${value}Z').toLocal();
}

bool isSameDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

/// 3:05 PM
String formatClock(DateTime time) {
  final t = time.toLocal();
  final hour = t.hour % 12 == 0 ? 12 : t.hour % 12;
  final minute = t.minute.toString().padLeft(2, '0');
  final suffix = t.hour >= 12 ? 'PM' : 'AM';
  return '$hour:$minute $suffix';
}

/// Today / Yesterday / 17/11/2026
String dayLabel(DateTime time) {
  final t = time.toLocal();
  final now = DateTime.now();
  if (isSameDay(t, now)) return 'Today';
  if (isSameDay(t, now.subtract(const Duration(days: 1)))) return 'Yesterday';
  return '${t.day}/${t.month}/${t.year}';
}

String lastSeenText(DateTime? time) {
  if (time == null) return 'offline';
  return 'last seen ${dayLabel(time).toLowerCase()} at ${formatClock(time)}';
}
