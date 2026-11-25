using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameHomework.Core
{
    /// <summary>
    /// 위젯 색 테마. 배경 이미지의 밝기에 맞춰 고른다.
    /// 색은 "#RRGGBB" 또는 "#AARRGGBB" 문자열 (WPF 쪽에서 브러시로 바꿈).
    /// </summary>
    public class WidgetTheme
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";

        /// <summary>배경(이미지 위에 덮는 색). 투명도는 OverlayOpacity로 따로 조절</summary>
        public string Overlay { get; init; } = "";
        /// <summary>이미지 없을 때 / 있을 때 기본 가림 정도 (0~1)</summary>
        public double DefaultOverlayNoImage { get; init; }
        public double DefaultOverlayWithImage { get; init; }

        public string WindowBorder { get; init; } = "";
        public string Title { get; init; } = "";
        public string Text { get; init; } = "";
        public string Muted { get; init; } = "";
        /// <summary>완료한 숙제 (일부러 흐리게)</summary>
        public string Dim { get; init; } = "";
        public string CardBg { get; init; } = "";
        public string CardBorder { get; init; } = "";
        /// <summary>RPG 캐릭터 이름 등 강조</summary>
        public string Accent { get; init; } = "";

        public string CheckFill { get; init; } = "";
        public string CheckBoxBorder { get; init; } = "";
        public string CheckBoxBg { get; init; } = "";
        public string CheckHover { get; init; } = "";

        public string Icon { get; init; } = "";
        public string IconHover { get; init; } = "";
        public string IconHoverBg { get; init; } = "";
        public string Scroll { get; init; } = "";

        public string WarnBorder { get; init; } = "";
        public string WarnBg { get; init; } = "";
        public string CriticalBorder { get; init; } = "";
        public string CriticalBg { get; init; } = "";
        public string WarnText { get; init; } = "";
        public string EventBorder { get; init; } = "";
        public string MissedBorder { get; init; } = "";
        public string MissedBg { get; init; } = "";
        public string MissedText { get; init; } = "";
        /// <summary>초기화 전날 경고 (주황)</summary>
        public string DueBorder { get; init; } = "";
        public string DueBg { get; init; } = "";
        public string DueText { get; init; } = "";
        public string ButtonBg { get; init; } = "";
        public string ButtonHoverBg { get; init; } = "";

        /// <summary>배지 글자색: 일일, 요일, 주간, 월간</summary>
        public string[] Badge { get; init; } = Array.Empty<string>();
        /// <summary>배지 배경 불투명도 (0~255)</summary>
        public byte BadgeBgAlpha { get; init; } = 0x2E;

        /// <summary>글씨에 그림자 (화려한 이미지 위에서 읽기 쉽게)</summary>
        public bool TextShadow { get; init; }

        public double DefaultOverlay(bool hasImage) => hasImage ? DefaultOverlayWithImage : DefaultOverlayNoImage;

        // ===================== 테마 3종 =====================

        public static readonly WidgetTheme Dark = new()
        {
            Id = "Dark",
            Name = "다크",
            Description = "어두운 배경 이미지용 (기본)",
            Overlay = "#17191E",
            DefaultOverlayNoImage = 0.92,
            DefaultOverlayWithImage = 0.70,
            WindowBorder = "#2EFFFFFF",
            Title = "#F2F3F5",
            Text = "#E6E8EC",
            Muted = "#9AA0AA",
            Dim = "#6B717B",
            CardBg = "#10FFFFFF",
            CardBorder = "#16FFFFFF",
            Accent = "#8FA6FF",
            CheckFill = "#6C8CFF",
            CheckBoxBorder = "#7A808C",
            CheckBoxBg = "#14FFFFFF",
            CheckHover = "#AFC0FF",
            Icon = "#B8BDC7",
            IconHover = "#FFFFFF",
            IconHoverBg = "#22FFFFFF",
            Scroll = "#44FFFFFF",
            WarnBorder = "#E8625C",
            WarnBg = "#16FF5A5A",
            CriticalBorder = "#FF4040",
            CriticalBg = "#2EFF4040",
            WarnText = "#FF8F87",
            EventBorder = "#26FFFFFF",
            MissedBorder = "#33B07A7A",
            MissedBg = "#0DFFFFFF",
            MissedText = "#C9A3A3",
            DueBorder = "#F59F00",
            DueBg = "#1FF59F00",
            DueText = "#FFC078",
            ButtonBg = "#1AFFFFFF",
            ButtonHoverBg = "#33FFFFFF",
            Badge = new[] { "#8FA6FF", "#7FD1A8", "#FFB86B", "#C79BFF" },
            BadgeBgAlpha = 0x2E
        };

        public static readonly WidgetTheme Light = new()
        {
            Id = "Light",
            Name = "라이트",
            Description = "밝은 배경 이미지용 (어두운 글씨)",
            Overlay = "#F7F8FA",
            DefaultOverlayNoImage = 0.94,
            DefaultOverlayWithImage = 0.75,
            WindowBorder = "#26000000",
            Title = "#111418",
            Text = "#1F2328",
            Muted = "#4A5160",
            Dim = "#8A909A",
            CardBg = "#B3FFFFFF",
            CardBorder = "#1A000000",
            Accent = "#3B5BDB",
            CheckFill = "#3B5BDB",
            CheckBoxBorder = "#8A909A",
            CheckBoxBg = "#FFFFFF",
            CheckHover = "#3B5BDB",
            Icon = "#4A5160",
            IconHover = "#111418",
            IconHoverBg = "#14000000",
            Scroll = "#40000000",
            WarnBorder = "#E03131",
            WarnBg = "#14E03131",
            CriticalBorder = "#C92A2A",
            CriticalBg = "#26E03131",
            WarnText = "#C92A2A",
            EventBorder = "#26000000",
            MissedBorder = "#4DC92A2A",
            MissedBg = "#0A000000",
            MissedText = "#8F3B3B",
            DueBorder = "#E8590C",
            DueBg = "#14E8590C",
            DueText = "#B33D07",
            ButtonBg = "#14000000",
            ButtonHoverBg = "#26000000",
            Badge = new[] { "#3B5BDB", "#2B8A3E", "#C2410C", "#7048E8" },
            BadgeBgAlpha = 0x1F
        };

        public static readonly WidgetTheme Glass = new()
        {
            Id = "Glass",
            Name = "글래스",
            Description = "사진·화려한 배경 이미지용 (흰 글씨 + 그림자, 이미지가 잘 보임)",
            Overlay = "#000000",
            DefaultOverlayNoImage = 0.50,
            DefaultOverlayWithImage = 0.45,
            WindowBorder = "#40FFFFFF",
            Title = "#FFFFFF",
            Text = "#FFFFFF",
            Muted = "#E3E6EB",
            Dim = "#B8BDC7",
            CardBg = "#40000000",
            CardBorder = "#33FFFFFF",
            Accent = "#C5D0FF",
            CheckFill = "#6C8CFF",
            CheckBoxBorder = "#E3E6EB",
            CheckBoxBg = "#33000000",
            CheckHover = "#FFFFFF",
            Icon = "#E3E6EB",
            IconHover = "#FFFFFF",
            IconHoverBg = "#33FFFFFF",
            Scroll = "#66FFFFFF",
            WarnBorder = "#FF6B6B",
            WarnBg = "#40C92A2A",
            CriticalBorder = "#FF4040",
            CriticalBg = "#66C92A2A",
            WarnText = "#FFC9C9",
            EventBorder = "#4DFFFFFF",
            MissedBorder = "#66FFB3B3",
            MissedBg = "#26000000",
            MissedText = "#FFD8D8",
            DueBorder = "#FFC078",
            DueBg = "#40E8590C",
            DueText = "#FFE8CC",
            ButtonBg = "#33FFFFFF",
            ButtonHoverBg = "#55FFFFFF",
            Badge = new[] { "#C5D0FF", "#B2F2BB", "#FFD8A8", "#E5DBFF" },
            BadgeBgAlpha = 0x4D,
            TextShadow = true
        };

        public static readonly IReadOnlyList<WidgetTheme> All = new[] { Dark, Light, Glass };

        public static WidgetTheme Get(string? id)
        {
            foreach (var t in All)
                if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;
            return Dark;
        }

        // ===================== 색 계산 (테스트용) =====================

        public static (byte A, byte R, byte G, byte B) Parse(string hex)
        {
            var s = hex.TrimStart('#');
            if (s.Length == 6) s = "FF" + s;
            if (s.Length != 8) throw new FormatException(hex);
            byte P(int i) => byte.Parse(s.Substring(i, 2), NumberStyles.HexNumber);
            return (P(0), P(2), P(4), P(6));
        }

        /// <summary>WCAG 대비 비율</summary>
        public static double Contrast(string fg, string bg)
        {
            static double L(string hex)
            {
                var (_, r, g, b) = Parse(hex);
                static double C(byte v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
                return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
            }
            double a = L(fg), b2 = L(bg);
            return (Math.Max(a, b2) + 0.05) / (Math.Min(a, b2) + 0.05);
        }

        /// <summary>overlay 색을 opacity만큼 아래 색 위에 덮었을 때의 색</summary>
        public static string Blend(string overlay, double opacity, string under)
        {
            var (_, r1, g1, b1) = Parse(overlay);
            var (_, r2, g2, b2) = Parse(under);
            byte M(byte a, byte b) => (byte)Math.Round(a * opacity + b * (1 - opacity));
            return $"#{M(r1, r2):X2}{M(g1, g2):X2}{M(b1, b2):X2}";
        }
    }
}
