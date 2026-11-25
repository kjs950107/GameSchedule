using System;
using System.IO;

namespace GameHomework
{
    /// <summary>
    /// 위젯 배경 이미지. 고른 이미지를 %AppData%\GameHomework\backgrounds 로 복사해 두어서
    /// 원본 파일을 옮기거나 지워도 배경이 유지된다.
    /// </summary>
    public static class BackgroundImages
    {
        public static string FolderPath => Path.Combine(AppState.Store.FolderPath, "backgrounds");

        public const string FileFilter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp";

        /// <summary>원본을 복사하고, 저장할 파일 이름을 돌려준다</summary>
        public static string Import(string sourcePath)
        {
            Directory.CreateDirectory(FolderPath);
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            var name = $"bg_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
            File.Copy(sourcePath, Path.Combine(FolderPath, name), false);
            return name;
        }

        /// <summary>저장된 이름 → 실제 경로 (없으면 null)</summary>
        public static string? Resolve(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            return Path.IsPathRooted(stored) ? stored : Path.Combine(FolderPath, stored);
        }

        /// <summary>어느 위젯도 쓰지 않는 복사본 정리</summary>
        public static void CleanupUnused()
        {
            try
            {
                if (!Directory.Exists(FolderPath)) return;
                var used = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in AppState.Data.Widget.Placements.Values)
                    if (!string.IsNullOrEmpty(p?.BackgroundImage)) used.Add(Path.GetFileName(p.BackgroundImage));
                foreach (var file in Directory.GetFiles(FolderPath))
                    if (!used.Contains(Path.GetFileName(file)))
                        try { File.Delete(file); } catch { }
            }
            catch (Exception ex)
            {
                App.LogError("배경 이미지 정리 실패", ex);
            }
        }
    }
}
