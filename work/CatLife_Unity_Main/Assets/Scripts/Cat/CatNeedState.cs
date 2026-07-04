using System;
using UnityEngine;

namespace CatLife.Cat
{
    [Serializable]
    public struct CatNeedState
    {
        [Range(0f, 1f)] public float curiosity01;
        [Range(0f, 1f)] public float sleepiness01;
        [Range(0f, 1f)] public float affection01;
        [Range(0f, 1f)] public float safety01;
        [Range(0f, 1f)] public float interruptionSensitivity01;
        [Range(0f, 1f)] public float focusCompanionship01;

        public static CatNeedState CreateDefault()
        {
            return new CatNeedState
            {
                curiosity01 = 0.52f,
                sleepiness01 = 0.2f,
                affection01 = 0.35f,
                safety01 = 0.78f,
                interruptionSensitivity01 = 0.28f,
                focusCompanionship01 = 0.35f
            };
        }

        public CatNeedState Clamp01()
        {
            curiosity01 = Mathf.Clamp01(curiosity01);
            sleepiness01 = Mathf.Clamp01(sleepiness01);
            affection01 = Mathf.Clamp01(affection01);
            safety01 = Mathf.Clamp01(safety01);
            interruptionSensitivity01 = Mathf.Clamp01(interruptionSensitivity01);
            focusCompanionship01 = Mathf.Clamp01(focusCompanionship01);
            return this;
        }
    }
}
