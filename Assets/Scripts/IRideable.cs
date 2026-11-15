using UnityEngine;

// Anything you can climb onto and ride - unicorns and dragons. While you
// ride, you do the moving (FirstPersonPlayer) and it goes along underneath you.
public interface IRideable
{
    void StartRiding();
    void StopRiding();

    // Called every frame you're riding, with where you are and which way you face
    void Carry(Vector3 riderFeet, float yaw, float speed, bool inTheAir);

    float SpeedBoost { get; }   // how many times faster than walking
    float SeatHeight { get; }   // how much higher your eyes are, sitting on its back
    float GlideSpeed { get; }   // the fastest you drift down when you let go of Space
}
