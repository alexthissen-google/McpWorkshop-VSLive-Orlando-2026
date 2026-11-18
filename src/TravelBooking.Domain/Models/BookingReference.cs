namespace TravelBooking.Domain.Models;

public static class BookingReference
{
    public const string Prefix = "BKG-";

    // Excludes I, L, O, 0, 1 to avoid visually ambiguous booking references.
    private static readonly char[] Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray();

    public static string Generate()
    {
        Span<char> chars = stackalloc char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }
        return $"{Prefix}{new string(chars)}";
    }
}
