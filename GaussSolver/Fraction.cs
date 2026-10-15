using System;
using System.Globalization;
using System.Text;

namespace GaussSolver
{
    public class Fraction : IEquatable<Fraction>, IComparable<Fraction>
    {
        public long Numerator { get; }
        public long Denominator { get; }

        public static readonly Fraction Zero = new Fraction(0, 1);
        public static readonly Fraction One = new Fraction(1, 1);
        public static readonly Fraction MinusOne = new Fraction(-1, 1);

        public Fraction(long numerator = 0, long denominator = 1)
        {
            if (denominator == 0)
                throw new DivideByZeroException("Denominator cannot be zero");

            if (numerator == 0)
            {
                Numerator = 0;
                Denominator = 1;
                return;
            }

            // توحيد الإشارة ليكون المقام موجباً دائماً
            if (denominator < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            long gcd = GCD(Math.Abs(numerator), denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        // GCD ( خوارزمية اقليدس )
        private static long GCD(long a, long b)
        {
            while (b != 0)
            {
                long temp = b;
                b = a % b;
                a = temp;
            }
            return a == 0 ? 1 : a;
        }

        // خصائص أساسية لخوارزميات REF و RREF 
        public bool IsZero => Numerator == 0;
        public bool IsOne => Numerator == 1 && Denominator == 1;
        public bool IsMinusOne => Numerator == -1 && Denominator == 1;
        public bool IsNegative => Numerator < 0;
        public bool IsPositive => Numerator > 0;
        public bool IsInteger => Denominator == 1;

        public Fraction Abs() => new Fraction(Math.Abs(Numerator), Denominator);

        public Fraction Reciprocal()
        {
            if (Numerator == 0)
                throw new DivideByZeroException("Cannot divide by zero");
            return new Fraction(Denominator, Numerator);
        }

        // التحويل من أرقام صحيحة إلى كسر
        public static implicit operator Fraction(long value) => new Fraction(value, 1);
        public static implicit operator Fraction(int value) => new Fraction(value, 1);

        // منع ال overflow
        public static Fraction operator +(Fraction a) => a;
        public static Fraction operator -(Fraction a) => new Fraction(-a.Numerator, a.Denominator);

        public static Fraction operator +(Fraction a, Fraction b)
        {
            long gcd = GCD(a.Denominator, b.Denominator);
            long den = (a.Denominator / gcd) * b.Denominator;
            long num = a.Numerator * (b.Denominator / gcd) + b.Numerator * (a.Denominator / gcd);
            return new Fraction(num, den);
        }

        public static Fraction operator -(Fraction a, Fraction b) => a + (-b);

        public static Fraction operator *(Fraction a, Fraction b)
        {
            long gcd1 = GCD(Math.Abs(a.Numerator), b.Denominator);
            long gcd2 = GCD(Math.Abs(b.Numerator), a.Denominator);
            return new Fraction(
                (a.Numerator / gcd1) * (b.Numerator / gcd2),
                (a.Denominator / gcd2) * (b.Denominator / gcd1)
            );
        }

        public static Fraction operator /(Fraction a, Fraction b)
        {
            if (b.Numerator == 0)
                throw new DivideByZeroException("cannot divide by zero");
            return a * b.Reciprocal();
        }

        // عمليات المقارنة ضد الـ null
        public static bool operator ==(Fraction? a, Fraction? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Numerator == b.Numerator && a.Denominator == b.Denominator;
        }

        public static bool operator !=(Fraction? a, Fraction? b) => !(a == b);
        public static bool operator <(Fraction a, Fraction b) => a.CompareTo(b) < 0;
        public static bool operator >(Fraction a, Fraction b) => a.CompareTo(b) > 0;
        public static bool operator <=(Fraction a, Fraction b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Fraction a, Fraction b) => a.CompareTo(b) >= 0;

        public bool Equals(Fraction? other)
        {
            if (other is null) return false;
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object? obj) => obj is Fraction other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

        public int CompareTo(Fraction? other)
        {
            if (other is null) return 1;
            long gcd = GCD(Denominator, other.Denominator);
            long left = Numerator * (other.Denominator / gcd);
            long right = other.Numerator * (Denominator / gcd);
            return left.CompareTo(right);
        }

        // النصوص المستخرجة من PDF أو OCR (توحيد السالب، القسمة، والأرقام العربية)
        private static string NormalizeMathText(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= '\u0660' && c <= '\u0669')
                    sb.Append((char)('0' + (c - '\u0660')));
                else if (c >= '\u06F0' && c <= '\u06F9')
                    sb.Append((char)('0' + (c - '\u06F0')));
                
                else if (c == '−' || c == '–' || c == '—' || c == '﹣')
                    sb.Append('-');
               
                else if (c == '÷' || c == '∕' || c == '\\')
                    sb.Append('/');
                
                else if (c == '٫' || c == ',')
                    sb.Append('.');
                else if (!char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        
        public static bool TryParseCoefficient(string? input, out Fraction result)
        {
            result = Zero;
            if (input is null) return false;

            string cleaned = NormalizeMathText(input);
            if (cleaned == "" || cleaned == "+")
            {
                result = One;
                return true;
            }
            if (cleaned == "-")
            {
                result = MinusOne;
                return true;
            }
            return TryParse(cleaned, out result);
        }

        // قراءة المدخلات النصية (تدعم الكسور "3/4"، الأرقام الصحيحة "-5"، والعشرية "0.25")
        public static bool TryParse(string? input, out Fraction result)
        {
            result = Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string cleaned = NormalizeMathText(input);
            if (string.IsNullOrEmpty(cleaned)) return false;

           
            if (cleaned.Contains('/'))
            {
                string[] parts = cleaned.Split('/');
                if (parts.Length != 2) return false;

                if (long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long num) &&
                    long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long den) &&
                    den != 0)
                {
                    result = new Fraction(num, den);
                    return true;
                }
                return false;
            }

            
            if (cleaned.Contains('.'))
            {
                if (!decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal dec))
                    return false;

                int[] bits = decimal.GetBits(dec);
                int scale = (bits[3] >> 16) & 0x7F;
                if (scale > 12) return false;

                long factor = (long)Math.Pow(10, scale);
                long num = (long)Math.Round(dec * factor);
                result = new Fraction(num, factor);
                return true;
            }

            
            if (long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intVal))
            {
                result = new Fraction(intVal, 1);
                return true;
            }

            return false;
        }

        public static Fraction Parse(string input)
        {
            if (TryParse(input, out Fraction result))
                return result;
            throw new FormatException($"The input value is not a valid number of fraction");
        }

        // دوال العرض والـ GUI والـ Step History
        public double ToDouble() => (double)Numerator / Denominator;

        public string ToDecimalString(int decimals = 2) =>
            Math.Round(ToDouble(), decimals).ToString(CultureInfo.InvariantCulture);

        
        public string ToStepString() =>
            (IsNegative || !IsInteger) ? $"({ToString()})" : ToString();

        
        public string FormatRowAddition(string targetRow, string pivotRow)
        {
            if (IsZero) return $"{targetRow}";
            string sign = IsNegative ? "-" : "+";
            Fraction absVal = Abs();
            string factorStr = absVal.IsOne ? "" : (absVal.IsInteger ? $"{absVal} * " : $"({absVal}) * ");
            return $"{targetRow} = {targetRow} {sign} {factorStr}{pivotRow}";
        }

        public override string ToString() =>
            Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";
    }
}
