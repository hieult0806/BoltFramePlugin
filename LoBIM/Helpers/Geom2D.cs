using Autodesk.Revit.DB;
using System;

public static class Geom2D
{
    const double EPS = 1e-9;

    // == API chính ==
    // Trả về: (khoảng cách feet, điểm trên ref line, điểm trên wall), tất cả ở XY (Z=0).
    public static (double distance, XYZ pointOnRefLine, XYZ pointOnWall)
        FindClosestDistance2D(Curve wallCurve2D, Curve refLine2D)
    {
        // Lấy 2 đầu mỗi đoạn, "flatten" về XY
        XYZ A = Flat(wallCurve2D.GetEndPoint(0)), B = Flat(wallCurve2D.GetEndPoint(1));
        XYZ C = Flat(refLine2D.GetEndPoint(0)), D = Flat(refLine2D.GetEndPoint(1));

        // 1) Nếu 2 đoạn cắt nhau → khoảng cách = 0 tại điểm giao
        if (SegmentIntersection2D(A, B, C, D, out XYZ P))
            return (0.0, P, P);

        // 2) Không cắt → min của bốn ứng viên
        //   (C→AB), (D→AB), (A→CD), (B→CD)
        var q1 = ClosestPointOnSegment2D(A, B, C);
        var d1 = Dist2D(C, q1);

        var q2 = ClosestPointOnSegment2D(A, B, D);
        var d2 = Dist2D(D, q2);

        var q3 = ClosestPointOnSegment2D(C, D, A);
        var d3 = Dist2D(A, q3);

        var q4 = ClosestPointOnSegment2D(C, D, B);
        var d4 = Dist2D(B, q4);

        // Chọn nhỏ nhất và trả đúng cặp (điểm trên ref, điểm trên wall)
        double min = d1; XYZ pr = C, pw = q1;
        if (d2 < min) { min = d2; pr = D; pw = q2; }
        if (d3 < min) { min = d3; pr = q3; pw = A; }
        if (d4 < min) { min = d4; pr = q4; pw = B; }

        return (min, pr, pw);
    }

    // == Helpers 2D ==
    static XYZ Flat(XYZ p) => new XYZ(p.X, p.Y, 0);

    static double Dist2D(XYZ p, XYZ q)
    {
        double dx = p.X - q.X, dy = p.Y - q.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static double Dot2(XYZ u, XYZ v) => u.X * v.X + u.Y * v.Y;
    static double Cross2(XYZ u, XYZ v) => u.X * v.Y - u.Y * v.X; // z-component

    // Giao hai đoạn 2D AB và CD; trả P nếu có
    static bool SegmentIntersection2D(XYZ A, XYZ B, XYZ C, XYZ D, out XYZ P)
    {
        P = XYZ.Zero;
        XYZ r = new XYZ(B.X - A.X, B.Y - A.Y, 0);
        XYZ s = new XYZ(D.X - C.X, D.Y - C.Y, 0);

        double rxs = Cross2(r, s);
        double qpxr = Cross2(new XYZ(C.X - A.X, C.Y - A.Y, 0), r);

        // Collinear?
        if (Math.Abs(rxs) < EPS && Math.Abs(qpxr) < EPS)
        {
            double rr = Dot2(r, r); if (rr < EPS) return false; // AB suy biến
            // Tham số chiếu C và D lên AB
            double tC = Dot2(new XYZ(C.X - A.X, C.Y - A.Y, 0), r) / rr;
            double tD = Dot2(new XYZ(D.X - A.X, D.Y - A.Y, 0), r) / rr;

            double tMin = Math.Max(0, Math.Min(tC, tD));
            double tMax = Math.Min(1, Math.Max(tC, tD));
            if (tMin <= tMax + EPS)
            {
                double tMid = 0.5 * (Math.Max(0, tMin) + Math.Min(1, tMax));
                P = new XYZ(A.X + tMid * r.X, A.Y + tMid * r.Y, 0);
                return true; // chồng lấn → khoảng cách 0
            }
            return false; // collinear nhưng rời nhau
        }

        // Song song không giao
        if (Math.Abs(rxs) < EPS && Math.Abs(qpxr) >= EPS) return false;

        // Giao duy nhất
        double t = Cross2(new XYZ(C.X - A.X, C.Y - A.Y, 0), s) / rxs;
        double u = Cross2(new XYZ(C.X - A.X, C.Y - A.Y, 0), r) / rxs;

        if (t >= -EPS && t <= 1 + EPS && u >= -EPS && u <= 1 + EPS)
        {
            P = new XYZ(A.X + t * r.X, A.Y + t * r.Y, 0);
            return true;
        }
        return false;
    }

    // Điểm gần nhất trên đoạn AB tới điểm P (2D)
    static XYZ ClosestPointOnSegment2D(XYZ A, XYZ B, XYZ P)
    {
        XYZ AB = new XYZ(B.X - A.X, B.Y - A.Y, 0);
        double ab2 = Dot2(AB, AB);
        if (ab2 < EPS) return A; // đoạn suy biến
        double t = Dot2(new XYZ(P.X - A.X, P.Y - A.Y, 0), AB) / ab2;
        t = Math.Max(0, Math.Min(1, t));
        return new XYZ(A.X + t * AB.X, A.Y + t * AB.Y, 0);
    }
}
