namespace BattleService.Application.Common.Exceptions
{
    public class AlreadyExistsException(string name, object key) : Exception($"Entity \"{name}\" ({key}) is already exists in DB.");

}
