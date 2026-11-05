using System;
using UnityEngine;

namespace CatLife.Mobile
{
    public static class CatLifeDataJson
    {
        public static string Serialize(CatLifeAppData data)
        {
            return JsonUtility.ToJson(data);
        }

        public static CatLifeAppData Deserialize(string json)
        {
            CatLifeAppData data = JsonUtility.FromJson<CatLifeAppData>(json);
            if (data == null) throw new ArgumentException("Missing CatLife save data.", nameof(json));
            // Unity recreates a null serializable class as an empty Normal object.
            // Only Transition and Focus represent a persisted active session.
            if (data.activeSession != null && data.activeSession.phase != CatLifeSessionPhase.Transition &&
                data.activeSession.phase != CatLifeSessionPhase.Focus) data.activeSession = null;
            return data;
        }

        public static bool TryDeserialize(string json, out CatLifeAppData data)
        {
            data = null;
            try { data = Deserialize(json); }
            catch (ArgumentException) { return false; }
            return data != null && data.settings != null && data.records != null;
        }
    }
}
