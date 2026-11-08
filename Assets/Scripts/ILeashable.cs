using UnityEngine;

// Anything you can catch with the leash
public interface ILeashable
{
    void Catch(Transform holder);
    void Release();
}
