namespace NFSMWSave.Core;

public sealed record CarInfo(string Key, string Name, string Make, int[] MaxLevels, bool LevelsConfirmed);

public sealed record RivalInfo(int Rank, string Name, string Car, int RequiredBounty, int RequiredChallenges, int RequiredWins);

public static class GameData
{
    private static readonly int[] Tier1 = [3, 4, 3, 4, 4, 3, 3];
    private static readonly int[] Tier2 = [2, 3, 2, 3, 3, 2, 3];
    private static readonly int[] Tier3 = [2, 2, 1, 2, 2, 1, 3];
    private static readonly int[] Tier4 = [1, 1, 1, 1, 1, 1, 3];
    private static readonly int[] Hero = [0, 0, 0, 0, 0, 0, 3];
    private static readonly int[] A4Seen = [3, 4, 3, 4, 3, 2, 3];
    private static readonly int[] Unconfirmed = [1, 1, 1, 1, 1, 1, 3];

    public static readonly string[] PerformanceNames = ["Tires", "Brakes", "Suspension", "Transmission", "Engine", "Turbo / Supercharger", "Nitrous"];

    public static readonly IReadOnlyList<CarInfo> Cars =
    [
        Known("911gt2", "Porsche 911 GT2", "PORSCHE"),
        Known("911turbo", "Porsche 911 Turbo S", "PORSCHE"),
        new("997s", "Porsche 911 Carrera S", "PORSCHE", Tier2, true),
        new("a3_20t", "Audi A3 3.2 quattro", "AUDI", Tier1, true),
        new("a4_32", "Audi A4 3.2 FSI quattro", "AUDI", A4Seen, true),
        Known("camaro", "Chevrolet Camaro SS", "CHEVROLET"),
        new("carrera_gt", "Porsche Carrera GT", "PORSCHE", Tier4, true),
        new("caymans", "Porsche Cayman S", "PORSCHE", Tier2, true),
        new("clio", "Renault Clio V6", "RENAULT", Tier1, true),
        new("clk500", "Mercedes-Benz CLK 500", "MERCEDES", Tier2, true),
        new("cobaltss", "Chevrolet Cobalt SS", "CHEVROLET", Tier1, true),
        new("corvette_car", "Chevrolet Corvette C6", "CORVETTE", Tier3, true),
        Known("corvettec6r", "Chevrolet Corvette C6.R", "CORVETTE"),
        Known("cts", "Cadillac CTS", "CADILLAC"),
        new("db9", "Aston Martin DB9", "ASTONMARTIN", Tier2, true),
        new("eclipsegt", "Mitsubishi Eclipse GT", "MITSUBISHI", Tier1, true),
        Known("elise", "Lotus Elise", "LOTUS"),
        new("fordgt", "Ford GT", "FORD", Tier4, true),
        new("gallardo", "Lamborghini Gallardo", "LAMBORGHINI", Tier3, true),
        new("gti", "Volkswagen Golf GTI", "VOLKSWAGEN", Tier1, true),
        Known("gto", "Pontiac GTO", "PONTIAC"),
        new("imprezzawrx", "Subaru Impreza WRX STi", "SUBARU", Tier2, true),
        new("is300", "Lexus IS 300", "LEXUS", Tier1, true),
        new("lancerevo8", "Mitsubishi Lancer Evolution VIII", "MITSUBISHI", Tier2, true),
        new("m3_gtr", "BMW M3 GTR (Street)", "BMW", Hero, false),
        new("m3_gtre46", "BMW M3 GTR", "BMW", Hero, true),
        new("monaro", "Vauxhall Monaro VXR", "VAUXHALL", Tier2, true),
        new("murcielago", "Lamborghini Murcielago", "LAMBORGHINI", Tier4, true),
        new("mustanggt", "Ford Mustang GT", "FORD", Tier2, true),
        new("punto", "Fiat Punto", "FIAT", Tier1, true),
        new("rx7", "Mazda RX-7", "MAZDA", Tier2, true),
        new("rx8", "Mazda RX-8", "MAZDA", Tier1, true),
        Known("sl500", "Mercedes-Benz SL 500", "MERCEDES"),
        Known("sl65", "Mercedes-Benz SL 65 AMG", "MERCEDES"),
        new("slr", "Mercedes-Benz SLR McLaren", "MCLAREN", Tier4, true),
        new("supra", "Toyota Supra", "TOYOTA", Tier1, true),
        Known("tt", "Audi TT 3.2 quattro", "AUDI"),
        new("viper", "Dodge Viper SRT-10", "DODGE", Tier3, true),
    ];

    public static readonly IReadOnlyList<RivalInfo> Rivals =
    [
        new(1, "Razor", "BMW M3 GTR", 10_000_000, 5, 9),
        new(2, "Ronnie", "Aston Martin DB9", 7_550_000, 5, 8),
        new(3, "Bull", "Mercedes-Benz SLR McLaren", 5_550_000, 5, 8),
        new(4, "Ming", "Lamborghini Gallardo", 4_050_000, 5, 7),
        new(5, "Webster", "Chevrolet Corvette C6", 3_050_000, 4, 7),
        new(6, "JV", "Dodge Viper SRT-10", 2_300_000, 4, 7),
        new(7, "Kaze", "Mercedes-Benz CLK 500", 1_680_000, 4, 7),
        new(8, "Jewels", "Ford Mustang GT", 1_180_000, 4, 5),
        new(9, "Earl", "Mitsubishi Lancer Evolution VIII", 790_000, 4, 5),
        new(10, "Baron", "Porsche Cayman S", 500_000, 4, 5),
        new(11, "Big Lou", "Mitsubishi Eclipse GT", 300_000, 3, 5),
        new(12, "Izzy", "Mazda RX-8", 180_000, 3, 4),
        new(13, "Vic", "Toyota Supra", 100_000, 3, 4),
        new(14, "Taz", "Lexus IS 300", 50_000, 3, 4),
        new(15, "Sonny", "Volkswagen Golf GTI", 20_000, 3, 3),
    ];

    private static readonly Dictionary<uint, CarInfo> ByHash = Cars.ToDictionary(c => AttribHash.Of(c.Key));

    public static CarInfo? FindCar(uint feKey) => ByHash.GetValueOrDefault(feKey);

    public static int[] DefaultMaxLevels => (int[])Unconfirmed.Clone();

    private static CarInfo Known(string key, string name, string make) => new(key, name, make, Unconfirmed, false);
}
