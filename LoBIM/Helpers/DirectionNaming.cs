using System;
using Autodesk.Revit.DB;

public static class DirectionNaming
{
    /// <summary>
    /// Trả về nhãn hướng (N, NE, E, …) từ vector normal. 
    /// winds: 4, 8, hoặc 16 (4 hướng, 8 hướng, 16 hướng).
    /// northOffsetDeg: cộng bù nếu muốn quy chiếu theo True North (ví dụ lấy góc chênh PN↔TN).
    /// </summary>
    public static string GetCompassLabel(XYZ normal, int winds = 8, double northOffsetDeg = 0.0)
    {
        if (normal == null) throw new ArgumentNullException(nameof(normal));
        // Bỏ thành phần Z, chỉ xét XY
        var vx = normal.X;
        var vy = normal.Y;
        var len = Math.Sqrt(vx * vx + vy * vy);
        if (len < 1e-9) throw new ArgumentException("Normal quá nhỏ (gần 0) trên mặt phẳng XY.");

        // Chuẩn hoá
        vx /= len; vy /= len;

        // Góc từ Bắc (Y+) theo chiều kim đồng hồ:
        // dùng atan2(X, Y) thay vì atan2(Y, X)
        double deg = Rad2Deg(Math.Atan2(vx, vy));

        // Cộng bù nếu cần (ví dụ hiệu chỉnh True North)
        deg = NormalizeDegrees(deg + northOffsetDeg);

        string[] rose4 = { "N", "E", "S", "W" };
        string[] rose8 = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        string[] rose16 = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        string[] table = winds switch
        {
            4 => rose4,
            8 => rose8,
            16 => rose16,
            _ => throw new ArgumentOutOfRangeException(nameof(winds), "winds chỉ hỗ trợ 4, 8 hoặc 16.")
        };

        double sector = 360.0 / table.Length;
        // Quy về chỉ số sector gần nhất (round để dính vào hướng “đẹp”)
        int idx = (int)Math.Round(deg / sector) % table.Length;
        if (idx < 0) idx += table.Length;

        return table[idx];
    }

    /// <summary>
    /// Trả về tên view dạng "{prefix} – {label}" (ví dụ "Section – NE").
    /// </summary>
    public static string BuildViewNameFromNormal(XYZ normal, string prefix = "View", int winds = 8, double northOffsetDeg = 0.0)
    {
        var label = GetCompassLabel(normal, winds, northOffsetDeg);
        return $"{prefix} – {label}";
    }

    private static double Rad2Deg(double rad) => rad * 180.0 / Math.PI;

    private static double NormalizeDegrees(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }
}
