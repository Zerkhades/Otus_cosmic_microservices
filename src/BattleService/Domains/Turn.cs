namespace BattleService.Domains;

public record Turn(string PlayerId, int Tick, byte[] Payload);