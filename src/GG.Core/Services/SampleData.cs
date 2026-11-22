using GG.Core.Models;

namespace GG.Core.Services;

/// <summary>
/// Made-up games used on the very first run (step 1 of development) so the grid has something to show.
/// Their ids start with <see cref="IdPrefix"/> so they can be removed automatically once real games are imported.
/// </summary>
public static class SampleData
{
    public const string IdPrefix = "sample-";

    public static bool IsSample(Game game) => game.Id.StartsWith(IdPrefix, StringComparison.Ordinal);

    public static List<Game> Create()
    {
        var entries = new (string Name, GameSource Source)[]
        {
            ("Starfall Odyssey", GameSource.Steam),
            ("Neon Drift", GameSource.Steam),
            ("Hollow Crown", GameSource.Steam),
            ("Iron Tide Tactics", GameSource.Steam),
            ("Pixel Harvest", GameSource.Steam),
            ("Cavern Echoes", GameSource.Steam),
            ("Skyline Racers", GameSource.Epic),
            ("Frostbound", GameSource.Epic),
            ("Quantum Heist", GameSource.Epic),
            ("Ember Knights Saga", GameSource.Xbox),
            ("Deep Blue Colony", GameSource.Xbox),
            ("Wasteland Couriers", GameSource.GOG),
            ("The Clockwork Archive", GameSource.GOG),
            ("Velocity League", GameSource.EA),
            ("Shadow Protocol", GameSource.Ubisoft),
            ("Arcane Frontier", GameSource.BattleNet),
            ("Retro Rally 64", GameSource.Shortcut),
            ("Moonlit Garden", GameSource.Shortcut),
            ("Thunder Brigade", GameSource.Shortcut),
            ("Lantern Hollow", GameSource.Shortcut),
            ("Drift King Underground", GameSource.FolderScan),
            ("Ancient Relic Hunters", GameSource.FolderScan),
            ("Orbital Mechanic", GameSource.Manual),
            ("Paper Planes", GameSource.Shortcut),
        };

        var rng = new Random(42); // Deterministic so screenshots/tests are repeatable.
        var now = DateTime.Now;
        var games = new List<Game>();

        for (var i = 0; i < entries.Length; i++)
        {
            var (name, source) = entries[i];
            var played = rng.NextDouble() < 0.6;
            games.Add(new Game
            {
                Id = $"{IdPrefix}{i:D3}",
                Name = name,
                Source = source,
                ExePath = $@"C:\Games\{name}\{name.Replace(" ", "")}.exe",
                InstallPath = $@"C:\Games\{name}",
                IsFavorite = i % 7 == 0,
                IsHidden = i == entries.Length - 1,
                NeedsReview = source == GameSource.FolderScan && i % 2 == 0,
                LastPlayed = played ? now.AddHours(-rng.Next(1, 24 * 60)) : null,
                TotalPlaytime = played ? TimeSpan.FromMinutes(rng.Next(15, 60 * 200)) : TimeSpan.Zero,
                DateAdded = now.AddDays(-rng.Next(0, 365)),
            });
        }

        return games;
    }
}
