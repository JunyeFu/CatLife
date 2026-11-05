using UnityEngine;

public enum CatLifeLandmarkAction
{
    Records,
    Growth,
    FishShop,
    RewardTree,
    Plaza
}

public sealed class CatLifeLandmark : MonoBehaviour
{
    public CatLifeLandmarkAction action;
}
