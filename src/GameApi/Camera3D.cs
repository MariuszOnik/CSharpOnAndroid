using System;
using Microsoft.Xna.Framework;

namespace GameApi;

/// <summary>
/// Kamera sceny 3D. Oś Y to góra, jednostki dowolne (wygodnie: 1 = 1 metr).
/// Dostępna w grze jako Camera; używają jej DrawModel i WorldToScreen.
/// </summary>
public class Camera3D
{
    /// <summary>Gdzie stoi kamera.</summary>
    public Vector3 Position { get; set; } = new(0, 3, 10);

    /// <summary>Punkt, na który patrzy.</summary>
    public Vector3 Target { get; set; } = Vector3.Zero;

    public Vector3 Up { get; set; } = Vector3.Up;

    /// <summary>Kąt widzenia w pionie, w stopniach.</summary>
    public float FieldOfView { get; set; } = 60f;

    /// <summary>Najbliższa i najdalsza rysowana odległość.</summary>
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;

    /// <summary>Ustawia kamerę w position, patrzącą na target.</summary>
    public void LookAt(Vector3 position, Vector3 target)
    {
        Position = position;
        Target = target;
    }

    /// <summary>
    /// Kamera krążąca wokół punktu: yaw – obrót w poziomie, pitch – uniesienie nad poziom (radiany).
    /// Przykład: Camera.Orbit(Vector3.Zero, 12, czas * 0.5f, 0.4f);
    /// </summary>
    public void Orbit(Vector3 target, float distance, float yaw, float pitch)
    {
        // Dokładnie nad celem kierunek patrzenia pokrywa się z Up i widok się rozsypuje.
        pitch = MathHelper.Clamp(pitch, -1.55f, 1.55f);
        float flat = MathF.Cos(pitch) * distance;
        Position = target + new Vector3(MathF.Sin(yaw) * flat, MathF.Sin(pitch) * distance, MathF.Cos(yaw) * flat);
        Target = target;
    }

    public Matrix View => Matrix.CreateLookAt(Position, Target, Up);

    public Matrix Projection(float aspectRatio) =>
        Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(MathHelper.Clamp(FieldOfView, 1f, 179f)), aspectRatio, Near, Far);
}
