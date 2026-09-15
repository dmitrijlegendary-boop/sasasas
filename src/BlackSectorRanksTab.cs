using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using RanksApi;

namespace BlackSectorRanksTab;

[MinimumApiVersion(374)]
public sealed class BlackSectorRanksTab : BasePlugin
{
    public override string ModuleName => "BLACKSECTOR Ranks TAB Icons";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleAuthor => "BLACKSECTOR";
    public override string ModuleDescription => "Synchronizes Ranks Core levels with custom TAB rank icons";

    private readonly PluginCapability<IRanksApi> _ranksCapability = new("ranks-core:api");
    private IRanksApi? _ranksApi;
    private Dictionary<int, int> _icons = new();
    private int _rankType = 12;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _ranksApi = _ranksCapability.Get();
        if (_ranksApi is null)
        {
            Server.PrintToConsole("[BLACKSECTOR Ranks TAB] Ranks Core API unavailable.");
            return;
        }

        LoadConfig();
        AddTimer(1.0f, UpdateRanks, TimerFlags.REPEAT);
        Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Loaded {_icons.Count} icon mappings.");
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
        if (_ranksApi is null)
            return;

        var recipients = new RecipientFilter();

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot)
                continue;

            var level = _ranksApi.GetPlayerRank(player);
            var icon = level > 0 && _icons.TryGetValue(level, out var configuredIcon)
                ? configuredIcon
                : 0;

            if (player.CompetitiveRankType == (sbyte)_rankType &&
                player.CompetitiveRanking == icon)
                continue;

            player.CompetitiveRankType = (sbyte)_rankType;
            player.CompetitiveRanking = icon;
            player.CompetitiveWins = 777;
            recipients.Add(player);
        }

        if (recipients.Count > 0)
            UserMessage.FromId(350).Send(recipients);
    }
}
