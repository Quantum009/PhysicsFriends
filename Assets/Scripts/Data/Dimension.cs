// ============================================================
// Dimension.cs — 量纲系统：物理量的量纲表示与运算
// 用于合成卡牌时的量纲匹配检查
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhysicsFriends.Data
{
    /// <summary>
    /// 量纲结构体，表示一个物理量的量纲组合
    /// 格式为 [m^a · s^b · kg^c · K^d · A^e · cd^f]
    /// </summary>
    [Serializable]
    public struct Dimension : IEquatable<Dimension>
    {
        public int m;   // 长度(米)的指数
        public int sec; // 时间(秒)的指数
        public int kg;  // 质量(千克)的指数
        public int K;   // 温度(开尔文)的指数
        public int A;   // 电流(安培)的指数
        public int cd;  // 光照强度(坎德拉)的指数

        // 构造函数：传入六个量纲指数
        public Dimension(int m, int sec, int kg, int K, int A, int cd)
        {
            this.m = m;
            this.sec = sec;
            this.kg = kg;
            this.K = K;
            this.A = A;
            this.cd = cd;
        }

        // 量纲相加（对应合成时将多张牌的量纲指数加在一起）
        public static Dimension operator +(Dimension a, Dimension b)
        {
            return new Dimension(
                a.m + b.m,      // 长度指数相加
                a.sec + b.sec,  // 时间指数相加
                a.kg + b.kg,    // 质量指数相加
                a.K + b.K,      // 温度指数相加
                a.A + b.A,      // 电流指数相加
                a.cd + b.cd     // 光照强度指数相加
            );
        }

        // 量纲取反（用于某些特殊运算）
        public static Dimension operator -(Dimension d)
        {
            return new Dimension(-d.m, -d.sec, -d.kg, -d.K, -d.A, -d.cd);
        }

        // 量纲相减
        public static Dimension operator -(Dimension a, Dimension b)
        {
            return a + (-b);
        }

        // 量纲标量乘法
        public Dimension Multiply(int scalar)
        {
            return new Dimension(
                m * scalar,
                sec * scalar,
                kg * scalar,
                K * scalar,
                A * scalar,
                cd * scalar
            );
        }

        // 量纲开根号（所有指数除以2，必须都能整除）
        public bool CanSqrt()
        {
            return m % 2 == 0 && sec % 2 == 0 && kg % 2 == 0 &&
                   K % 2 == 0 && A % 2 == 0 && cd % 2 == 0;
        }

        // 执行开根号运算
        public Dimension Sqrt()
        {
            return new Dimension(m / 2, sec / 2, kg / 2, K / 2, A / 2, cd / 2);
        }

        // 执行平方运算
        public Dimension Square()
        {
            return Multiply(2);
        }

        // 判断是否为零量纲（无量纲量）
        public bool IsZero()
        {
            return m == 0 && sec == 0 && kg == 0 && K == 0 && A == 0 && cd == 0;
        }

        // 相等判断
        public bool Equals(Dimension other)
        {
            return m == other.m && sec == other.sec && kg == other.kg &&
                   K == other.K && A == other.A && cd == other.cd;
        }

        public override bool Equals(object obj)
        {
            return obj is Dimension d && Equals(d);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m, sec, kg, K, A, cd);
        }

        public static bool operator ==(Dimension a, Dimension b) => a.Equals(b);
        public static bool operator !=(Dimension a, Dimension b) => !a.Equals(b);

        // 转为可读字符串，如 [m^2·s^-2·kg^1]
        public override string ToString()
        {
            var parts = new List<string>(); // 收集非零指数
            if (m != 0) parts.Add($"m^{m}");
            if (sec != 0) parts.Add($"s^{sec}");
            if (kg != 0) parts.Add($"kg^{kg}");
            if (K != 0) parts.Add($"K^{K}");
            if (A != 0) parts.Add($"A^{A}");
            if (cd != 0) parts.Add($"cd^{cd}");
            return parts.Count > 0 ? $"[{string.Join("·", parts)}]" : "[无量纲]";
        }
    }
}
