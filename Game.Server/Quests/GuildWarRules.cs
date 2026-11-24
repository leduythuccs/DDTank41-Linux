using Bussiness;
using Game.Logic;

namespace Game.Server.Quests
{
    /// <summary>Which games count as a guild war for quests and guild tasks.</summary>
    public static class GuildWarRules
    {
        /// <summary>
        /// A real guild war (2+ players of one guild on a team), or, with the FreeMatchCountsAsGuildWar option,
        /// any free PvP match: playing alone or with players of other guilds then counts too.
        /// </summary>
        public static bool Counts(AbstractGame game)
        {
            return game.GameType == eGameType.Guild
                || (GameProperties.FreeMatchCountsAsGuildWar && game.RoomType == eRoomType.Match && game.GameType == eGameType.Free);
        }
    }
}
