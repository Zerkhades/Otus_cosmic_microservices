using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Abstractions
{
    /// <summary>
    /// Поведение для объектов, способных перемещаться.
    /// </summary>
    public interface IMovable
    {
        Vector2 Position { get; set; }
        Vector2 Velocity { get; }
        float RotationDeg { get; }      // курс в градусах 0‑360
        void ApplyThrust(float delta);  // ускорение вперёд (‑1..1)
        void Rotate(float degDelta);    // поворот вокруг оси Z
        //void Move(Vector2 delta);
    }
}
