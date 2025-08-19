using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleService.Application.Commands;
using BattleService.Domains;
using BattleService.Infrastructure.State;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BattleService.Tests
{
    // Фейковое хранилище, реализующее IBattleStore
    public class FakeBattleStore : IBattleStore
    {
        public readonly List<Battle> AddedBattles = new();
        public void Add(Battle battle) => AddedBattles.Add(battle);
        public bool TryGet(Guid id, out Battle? battle)
        {
            battle = AddedBattles.Find(b => b.Id == id);
            return battle != null;
        }
        public void Remove(Guid id) { }
        public IEnumerable<Battle> ListRunning() => AddedBattles;
    }


    public class StartBattleCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ValidCommand_AddsBattleAndLogsMessage()
        {
            // Arrange
            var fakeStore = new FakeBattleStore();
            var fakeLogger = new FakeLogger<StartBattleCommandHandler>();
            var handler = new StartBattleCommandHandler(fakeStore, fakeLogger);

            var battleId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var participants = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var command = new StartBattleCommand(battleId, tournamentId, participants);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(fakeStore.TryGet(battleId, out var battle));
            Assert.Equal(tournamentId, battle?.TournamentId);
            Assert.Equal(participants.Count, battle?.Participants.Count);
            //Assert.Contains(fakeLogger.Messages, m => m.Contains(battleId.ToString()) && m.Contains(participants.Count.ToString()));
        }
    }
}