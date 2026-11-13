using System.Text;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Services;
using Discord;

namespace BF2WebAdmin.Server.Modules.DiscordBot;

/// <summary>The text the Discord bot writes: names for weapons and teams, the score table, the help, safe player text.</summary>
public static class DiscordText
{
    public const string Help = @"```# Show player scores
/score

# Use custom commands
.<command>

# Admin commands
" + ServerAdmin.Help + "```";

    // TODO: some different structure
    private static readonly IDictionary<string, string> WeaponNames = new Dictionary<string, string>
    {
        // Vehicles: https://github.com/chrisw1229/bf2-stats/blob/master/webapp/models/vehicles.py
        {"aav_tunguska_gun", "Tunguska"},
        {"aav_tunguska_sa19launcher", "Tunguska"},

        {"aav_type95guns", "Type 95"},
        {"aav_type95_qw2launcher", "Type 95"},

        {"ahe_ah1z_hydralauncher", "AH-1Z Hydra"},
        {"ahe_ah1z_cogunner_hellfirelaunchertv", "AH-1Z TV"},
        {"ahe_ah1z_gun", "AH-1Z MG"},

        {"ahe_z10_s8launcher", "Z-10 Hydra"},
        {"ahe_z10_hj8launcher_tv", "Z-10 TV"},
        {"ahe_z10_gun", "Z-10 MG"},

        {"ahe_havoc_s8launcher", "Mi-28 Hydra"},
        {"ahe_havoc_atakalauncher_tv", "Mi-28 Hydra"},
        {"ahe_havoc_gun", "Mi-28 Hydra"},

        {"air_a10_us_bomblauncher", "A-10"},

        {"air_f35b_autocannon", "F-35B"},
        {"air_f35b_sidewinderlauncher", "F-35B"},
        {"air_f35b_bomblauncher", "F-35B"},

        {"air_j10_cannon", "J-10"},
        {"air_j10_archerlauncher", "J-10"},

        {"air_su30mkk_30mmcannon", "Su-30MKK"},
        {"air_su30mkk_archerlauncher", "Su-30MKK"},
        {"air_su30mkk_kedgelauncher_laser", "Su-30MKK"},

        {"air_su39_canon", "Su-39"},

        {"apc_btr90__barrel", "BTR-90"},
        {"apc_btr90_hj8launcher", "BTR-90"},
        //{"firingport_ak", "BTR-90"}, // duplicate key

        {"apc_wz551_barrel", "WZ551"},
        {"apc_wz551_hj8launcher", "WZ551"},
        //{"firingport_ak", "WZ551"}, // duplicate key

        {"ars_d30_barrel", "D-30"},

        {"ats_hj8_launcher", "HJ-8"},

        {"ats_tow_launcher", "BGM-71"},

        //{"uslmg_m249saw_stationary", "RIB"}, // duplicate key

        {"che_wz11_canons", "WZ-11"},

        //{"chlmg_type95_stationary", "Bipod"}, // duplicate key

        //{"chhmg_type85", "Z-8"}, // duplicate key

        //{"hmg_m2hb", "M2"}, // duplicate key

        {"igla_djigit_launcher", "Igla"},

        //{"hmg_m2hb", "FAAV"}, // duplicate key
        //{"uslmg_m249saw_stationary", "FAAV"}, // duplicate key

        //{"chhmg_kord", "Paratrooper"},
        //{"rulmg_rpk74_stationary", "Paratrooper"},

        //{"chhmg_type85", "Nanjing"}, // duplicate key

        //{"chhmg_type85", "Paratrooper"}, // duplicate key
        //{"chlmg_type95_stationary", "Paratrooper"}, // duplicate key

        //{"chhmg_kord", "GAZ-3937"},

        //{"rulmg_rpk74_stationary", "Bipod"},

        {"ruair_mig29_30mmcannon", "MiG-29"},
        {"ruair_archerlauncher", "MiG-29"},
        {"ruair_mig29_bomblauncher_1", "MiG-29"},

        {"ruair_su34_30mmcannon", "Su-34"},
        {"ruair_su34_archerlauncher", "Su-34"},
        {"ruair_su34_250kgbomblauncher", "Su-34"},
        {"ruair_su34_kedgelaunchertv", "Su-34"},

        {"rutnk_t90_barrel", "T-90"},
        {"coaxial_mg_mec", "T-90"},
        //{"chhmg_kord", "T-90"},

        {"she_ec635_cannons", "EC635"},

        {"she_littlebird_miniguns", "MH-6"},

        {"tnk_type98_barrel", "Type 98"},
        {"coaxial_mg_china", "Type 98"},
        //{"chhmg_type85", "Type 98"}, // duplicate key

        //{"uslmg_m249saw_stationary", "Bipod"}, // duplicate key

        {"usaas_stinger_launcher", "FIM-92"},

        {"usaav_m6_barrel", "M6"},
        {"usaav_m6_stinger_launcher", "M6"},

        {"f18_autocannon", "F/A-18"},
        {"f18_sidewinderlauncher", "F/A-18"},
        {"usair_f18_bomblauncher", "F/A-18"},

        {"usair_f15_autocannon", "F-15"},
        {"usair_f15_sidewinderlauncher", "F-15"},
        {"usair_f15_mavericklauncherlaser", "F-15"},
        {"usair_f15_250kgbomblauncher", "F-15"},

        {"usapc_lav25_barrel", "LAV-25"},
        {"usapc_lav25_towlauncher", "LAV-25"},
        {"firingport_m16", "LAV-25"},

        {"usart_lw155_barrel", "M777"},

        //{"hmg_m2hb", "HMMWV"}, // duplicate key

        {"hmg_m134_gun", "UH-60"},

        {"ustnk_m1a2_barrel", "M1A2"},
        {"coaxial_browning", "M1A2"},
        //{"hmg_m2hb", "M1A2"}, // duplicate key

        {"eurofighter_autocannon", "Eurofighter"},
        {"eurofighter_missiles", "Eurofighter"},
        {"eurofighter_bomb_launcher", "Eurofighter"},

        {"fantan_autocannon", "Q-5"},
        {"xpak2_fantan_bomblauncher", "Q-5"},

        {"xpak2_tiger_missiles", "EC665"},
        {"tnk_c2_barrel", "C2"}
    };

    public static string WeaponName(string weapon)
    {
        var key = weapon?.ToLower() ?? string.Empty;
        return WeaponNames.TryGetValue(key, out var name) ? name : weapon;
    }

    public static string TeamFlag(string teamName) => teamName switch
    {
        "US" => ":flag_us:",
        "CH" => ":flag_cn:",
        "MEC" => ":orange_square:",
        "EU" => ":flag_eu:",
        _ => ":black_medium_square:",
    };

    /// <summary>Player text without Discord markdown and mentions (a zero-width space after @, # and &lt;).</summary>
    public static string Sanitize(string text)
    {
        if (text == null)
            return string.Empty;

        var noMarkdown = Format.Sanitize(text);
        var noMentions = noMarkdown.Replace("@", "@​").Replace("#", "#​").Replace("<", "<​");
        return noMentions;
    }

    /// <summary>The /score table: each team's players by score, with country, score, team score, kills, deaths and ping.</summary>
    public static string ScoreTable(IGameServer game)
    {
        const int maxLength = 19;
        var sb = new StringBuilder(100);
        foreach (var team in game.Teams.OrderByDescending(t => t.Id))
        {
            sb.Append("#  ");
            sb.Append(team.Name?.PadRight(3));
            sb.Append(string.Empty.PadRight(maxLength));
            sb.Append("🏆".PadLeft(5));
            sb.Append("🛠".PadLeft(5));
            sb.Append("🎯".PadLeft(5));
            sb.Append("💀".PadLeft(5));
            sb.Append("🌐".PadLeft(5));
            sb.AppendLine();

            foreach (var player in game.Players.OrderByDescending(p => p.Score.Total).Where(p => p.Team.Id == team.Id))
            {
                var name = player.DisplayName.Length <= maxLength ? player.DisplayName : player.DisplayName.Substring(0, maxLength);
                sb.Append(player.Index.ToString().PadRight(3));
                sb.Append((player.Country.Code ?? string.Empty).PadRight(3));
                sb.Append(name.PadRight(maxLength));
                sb.Append(player.Score.Total.ToString().PadLeft(5));
                sb.Append(player.Score.Team.ToString().PadLeft(5));
                sb.Append(player.Score.Kills.ToString().PadLeft(5));
                sb.Append(player.Score.Deaths.ToString().PadLeft(5));
                sb.Append(player.Score.Ping.ToString().PadLeft(5));
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return $"```{sb}```";
    }
}
