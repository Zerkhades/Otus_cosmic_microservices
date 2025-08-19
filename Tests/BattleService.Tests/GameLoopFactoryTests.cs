using System;
using BattleService.Application.Worlds;
using BattleService.GameLogic.Engine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BattleService.Tests
{
    public class GameLoopFactoryTests
    {
        [Fact]
        public void Create_ShouldReturnNewGameLoopInstance()
        {
            // Arrange: регистрируем GameLoop в DI-контейнере.
            var services = new ServiceCollection();
            services.AddTransient<GameLoop>();
            var provider = services.BuildServiceProvider();

            var factory = new GameLoopFactory(provider);

            // Act: создаём GameLoop через фабрику.
            var gameLoop = factory.Create(Guid.NewGuid());

            // Assert: проверяем, что экземпляр не null и имеет тип GameLoop.
            Assert.NotNull(gameLoop);
            Assert.IsType<GameLoop>(gameLoop);
        }
    }
}