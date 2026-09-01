using UnityEngine;

public enum CatLifeLandmarkAction
{
    Records,
    Growth
}

public sealed class CatLifeLandmark : MonoBehaviour
{
    public CatLifeLandmarkAction action;
}
