using System;
using Microsoft.Win32;

namespace GameHomework
{
    /// <summary>윈도우 시작 시 자동 실행 (HKCU\...\Run)</summary>
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "GameHomework";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                    return key?.GetValue(ValueName) is string s && s.Length > 0;
                }
                catch { return false; }
            }
        }

        public static void Set(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (enable)
                {
                    var exe = Environment.ProcessPath ?? "";
                    if (exe.Length > 0) key.SetValue(ValueName, "\"" + exe + "\"");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex)
            {
                App.LogError("자동 실행 설정 실패", ex);
            }
        }

        /// <summary>exe 위치를 옮겼을 때 경로 갱신</summary>
        public static void RefreshPathIfEnabled()
        {
            if (IsEnabled) Set(true);
        }
    }
}
