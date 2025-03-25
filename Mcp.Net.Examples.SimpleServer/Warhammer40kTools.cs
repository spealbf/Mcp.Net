using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.SimpleServer
{
    /// <summary>
    /// Provides simple Warhammer 40k themed example tools for the MCP protocol.
    /// </summary>
    public class Warhammer40kTools
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _names =
        {
            "Artemis",
            "Eisenhorn",
            "Ravenor",
            "Kryptman",
            "Coteaz",
            "Karamazov",
        };
        private static readonly string[] _ordos = { "Hereticus", "Xenos", "Malleus", "Scriptorum" };
        private static readonly string[] _factions =
        {
            "Space Marines",
            "Imperial Guard",
            "Adeptus Mechanicus",
        };
        private static readonly string[] _enemies =
        {
            "Tyranids",
            "Chaos",
            "Orks",
            "Necrons",
            "T'au",
        };
        private static readonly string[] _battlefields =
        {
            "Hive City",
            "Forge World",
            "Death World",
            "Space Hulk",
        };

        /// <summary>
        /// Generates a name and title for a Warhammer 40k Inquisitor character.
        /// </summary>
        /// <param name="includeTitle">Whether to include a title prefix.</param>
        /// <returns>Information about the generated Inquisitor.</returns>
        [McpTool("wh40k_inquisitor_name", "Generate a name for a Warhammer 40k Inquisitor")]
        public static InquisitorInfo GenerateInquisitorName(
            [McpParameter(required: false, description: "Include title")] bool includeTitle = true
        )
        {
            string name = _names[_random.Next(_names.Length)];
            string ordo = _ordos[_random.Next(_ordos.Length)];
            string title = includeTitle ? "Lord " : "";

            return new InquisitorInfo
            {
                Name = name,
                FullTitle = $"{title}Inquisitor {name}",
                Ordo = $"Ordo {ordo}",
                Description = $"A servant of the Emperor and member of the Ordo {ordo}.",
            };
        }

        /// <summary>
        /// Rolls dice with Warhammer 40k flavor text.
        /// </summary>
        /// <param name="diceCount">Number of dice to roll.</param>
        /// <param name="diceSides">Number of sides on each die.</param>
        /// <param name="flavor">Flavor of the roll (hit, wound, save).</param>
        /// <returns>Results of the dice roll.</returns>
        [McpTool("wh40k_roll_dice", "Roll dice with Warhammer 40k flavor")]
        public static DiceRollResult RollDice(
            [McpParameter(required: true, description: "Number of dice to roll")] int diceCount,
            [McpParameter(required: true, description: "Number of sides on each die")]
                int diceSides,
            [McpParameter(required: false, description: "Flavor of the roll (hit, wound, save)")]
                string flavor = "hit"
        )
        {
            if (diceCount <= 0 || diceCount > 100)
                throw new ArgumentException("Dice count must be between 1 and 100");

            if (diceSides <= 0 || diceSides > 20)
                throw new ArgumentException("Dice sides must be between 1 and 20");

            // Roll the dice
            List<int> rolls = new List<int>();
            for (int i = 0; i < diceCount; i++)
            {
                rolls.Add(_random.Next(1, diceSides + 1));
            }

            // Simple threshold based on flavor
            int threshold = flavor == "wound" ? 4 : 3; // 4+ for wounds, 3+ for others
            int successes = rolls.Count(r => r > threshold);

            string flavorText = flavor switch
            {
                "hit" => $"Your attack hits {successes} times!",
                "wound" => $"You wound your target {successes} times!",
                "save" => $"You successfully save {successes} wounds!",
                _ => $"You rolled {successes} successes.",
            };

            return new DiceRollResult
            {
                Rolls = rolls,
                Total = rolls.Sum(),
                FlavorText = flavorText,
            };
        }

        /// <summary>
        /// Simulates a simple battle between Imperial forces and enemies in the Warhammer 40k universe.
        /// This method is async to demonstrate how to implement async tools in MCP.
        /// </summary>
        /// <param name="imperialForce">The Imperial faction for the battle.</param>
        /// <param name="enemyForce">The enemy faction for the battle.</param>
        /// <returns>Results of the simulated battle.</returns>
        [McpTool("wh40k_battle_simulation", "Simulate a battle in the Warhammer 40k universe")]
        public static async Task<BattleResult> SimulateBattleAsync(
            [McpParameter(required: false, description: "Imperial force")]
                string imperialForce = "",
            [McpParameter(required: false, description: "Enemy force")] string enemyForce = ""
        )
        {
            // This is intentionally async to demonstrate async tool pattern
            await Task.Delay(500);

            string imperial = string.IsNullOrEmpty(imperialForce)
                ? _factions[_random.Next(_factions.Length)]
                : imperialForce;

            string enemy = string.IsNullOrEmpty(enemyForce)
                ? _enemies[_random.Next(_enemies.Length)]
                : enemyForce;

            string battlefield = _battlefields[_random.Next(_battlefields.Length)];

            // Simple 50/50 battle outcome
            bool imperialVictory = _random.Next(2) == 0;

            return new BattleResult
            {
                ImperialForce = imperial,
                EnemyForce = enemy,
                Battlefield = battlefield,
                IsImperialVictory = imperialVictory,
                BattleReport =
                    $"The {imperial} {(imperialVictory ? "defeated" : "were defeated by")} the {enemy} at {battlefield}. The Emperor protects!",
            };
        }
    }

    /// <summary>
    /// Information about a generated Inquisitor character.
    /// </summary>
    public class InquisitorInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullTitle { get; set; } = string.Empty;
        public string Ordo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Results of a dice roll with Warhammer 40k flavor.
    /// </summary>
    public class DiceRollResult
    {
        public List<int> Rolls { get; set; } = new List<int>();
        public int Total { get; set; }
        public string FlavorText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Results of a simulated Warhammer 40k battle.
    /// </summary>
    public class BattleResult
    {
        public string ImperialForce { get; set; } = string.Empty;
        public string EnemyForce { get; set; } = string.Empty;
        public string Battlefield { get; set; } = string.Empty;
        public bool IsImperialVictory { get; set; }
        public string BattleReport { get; set; } = string.Empty;
    }
}
