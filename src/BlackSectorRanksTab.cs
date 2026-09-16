using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;\nusing CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using RanksApi;

namespace BlackSectorRanksTab;

[MinimumApiVersion(374)]
public sealed class BlackSectorRanksTab : BasePlugin
{
    public override string ModuleName => "BLACKSECTOR Ranks TAB Icons";
    public override string ModuleVersion => "2.2.0";
    public override string ModuleAuthor => "BLACKSECTOR";
    public override string ModuleDescription => "Synchronizes Ranks Core levels with custom TAB rank icons";

    private IRanksApi? _ranksApi;
    private Dictionary<int, int> _icons = new();
    private int _rankType = 12;
    private int _maxLevel = 1;\n    private int _testIcon = -1;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _ranksApi = IRanksApi.Capability.Get();
        if (_ranksApi is null)
        {
            Server.PrintToConsole("[BLACKSECTOR Ranks TAB] Ranks Core API unavailable.");
            return;
        }

        LoadConfig();
        RegisterListener<Listeners.OnTick>(UpdateRanks);
        Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] v2.1.0 loaded {_icons.Count} icon mappings.");
    }

    private void LoadConfig()
    {
        var path = Path.Combine(
            Application.RootDirectory,
            "configs/plugins/RanksCore/Modules/ranks_fakerank.json");

        if (!File.Exists(path))
        {
            Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Config not found: {path}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            var type = ReadInt(root.GetProperty("Type"));
            _rankType = type switch
            {
                0 => 11,
                2 => 7,
                3 => 11,
                _ => 12
            };

            var mappings = new Dictionary<int, int>();
            foreach (var item in root.GetProperty("FakeRank").EnumerateObject())
            {
                if (int.TryParse(item.Name, out var level))
                    mappings[level] = ReadInt(item.Value);
            }

            _icons = mappings;
            _maxLevel = _icons.Count > 0 ? _icons.Keys.Max() : 1;
        }
        catch (Exception exception)
        {
            Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Config error: {exception.Message}");
        }
    }

    private static int ReadInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();

        return int.TryParse(value.GetString(), out var result) ? result : 0;
    }

    private void UpdateRanks()
    {
        if (_ranksApi is null || _icons.Count == 0)
            return;

        var recipients = new RecipientFilter();

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid ||
                player.IsBot ||
                player.IsHLTV)
                continue;

            var level = Math.Clamp(_ranksApi.GetPlayerRank(player), 1, _maxLevel);
            var icon = _testIcon >= 0
                ? _testIcon
                : _icons.TryGetValue(level, out var configuredIcon)
                    ? configuredIcon
                    : _icons[1];

            player.CompetitiveWins = 777;
            player.CompetitiveRankType = (sbyte)_rankType;
            player.CompetitiveRanking = icon;

            recipients.Add(player);
        }

        if (recipients.Count > 0)
            UserMessage.FromPartialName("CCSUsrMsg_ServerRankRevealAll").Send(recipients);
    }
}
