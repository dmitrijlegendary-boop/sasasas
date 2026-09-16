using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using RanksApi;

namespace BlackSectorRanksTab;

[MinimumApiVersion(374)]
public sealed class BlackSectorRanksTab : BasePlugin
{
    public override string ModuleName => "BLACKSECTOR Ranks TAB Icons";
    public override string ModuleVersion => "2.5.0";
    public override string ModuleAuthor => "BLACKSECTOR";
    public override string ModuleDescription => "Synchronizes Ranks Core levels with custom TAB rank icons";

    private IRanksApi? _ranksApi;
    private Dictionary<int, int> _icons = new();
    private int _maxLevel = 1;
    private int _testIcon = -1;
    private int _tick;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _ranksApi = IRanksApi.Capability.Get();
        if (_ranksApi is null)
        {
            Server.PrintToConsole("[BLACKSECTOR Ranks TAB] Ranks Core API unavailable.");
            return;
        }

        LoadConfig();

        AddCommand("css_bsrank_test", "Force a TAB icon; -1 restores automatic mode", (player, info) =>
        {
            if (player is not null)
                return;

            if (info.ArgCount < 2 || !int.TryParse(info.GetArg(1), out _testIcon))
            {
                Server.PrintToConsole("[BLACKSECTOR Ranks TAB] Usage: css_bsrank_test <icon>; use -1 for automatic mode.");
                return;
            }

            ApplyAndReveal(true);
            Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Test icon set to {_testIcon}; reveal sent.");
        });

        RegisterListener<Listeners.OnTick>(() =>
        {
            _tick++;
            // Reapply and reveal twice per second. This deliberately does not depend on
            // the MetaMod FakeRanks connection-time message.
            ApplyAndReveal(_tick % 32 == 0);
        });

        Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] v2.5.0 loaded {_icons.Count} real VPK icon mappings.");
    }

    private void LoadConfig()
    {
        var path = Path.Combine(Application.RootDirectory,
            "configs/plugins/RanksCore/Modules/ranks_fakerank.json");

        if (!File.Exists(path))
        {
            Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Config not found: {path}");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var mappings = new Dictionary<int, int>();
            foreach (var item in document.RootElement.GetProperty("FakeRank").EnumerateObject())
            {
                if (int.TryParse(item.Name, out var level))
                    mappings[level] = item.Value.ValueKind == JsonValueKind.Number
                        ? item.Value.GetInt32()
                        : int.Parse(item.Value.GetString()!);
            }

            _icons = mappings;
            _maxLevel = _icons.Count > 0 ? _icons.Keys.Max() : 1;
        }
        catch (Exception exception)
        {
            Server.PrintToConsole($"[BLACKSECTOR Ranks TAB] Config error: {exception.Message}");
        }
    }

    private void ApplyAndReveal(bool reveal)
    {
        if (_ranksApi is null || _icons.Count == 0)
            return;

        var recipients = new RecipientFilter();

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot || player.IsHLTV)
                continue;

            var level = Math.Clamp(_ranksApi.GetPlayerRank(player), 1, _maxLevel);
            var icon = _testIcon >= 0
                ? _testIcon
                : _icons.TryGetValue(level, out var mapped) ? mapped : _icons[1];

            player.CompetitiveWins = 111;
            player.CompetitiveRankType = 12;
            player.CompetitiveRanking = icon;

            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveWins");
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");

            if (reveal)
                recipients.Add(player);
        }

        if (reveal && recipients.Count > 0)
        {
            using var message = UserMessage.FromPartialName("CCSUsrMsg_ServerRankRevealAll");
            message.Send(recipients);
        }
    }
}
