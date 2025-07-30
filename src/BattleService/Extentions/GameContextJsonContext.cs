using BattleService.GameLogic.Engine;
using System.Text.Json.Serialization;

namespace BattleService.Extentions
{
    [JsonSerializable(typeof(GameContext))]
    internal partial class GameContextJsonContext : JsonSerializerContext
    { }
}
