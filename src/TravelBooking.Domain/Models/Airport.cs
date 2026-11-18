namespace TravelBooking.Domain.Models;

public class Airport
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string Country { get; private set; } = default!;

    private Airport() { }

    public static Airport Create(string code, string city, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));
        ArgumentException.ThrowIfNullOrWhiteSpace(country, nameof(country));

        var normalisedCode = code.Trim().ToUpperInvariant();
        if (normalisedCode.Length != 3 || !normalisedCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Airport code must be exactly three letters.", nameof(code));
        }

        return new Airport
        {
            Id = Guid.NewGuid(),
            Code = normalisedCode,
            City = city,
            Country = country,
        };
    }
}
