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
            return JsonUtility.FromJson<CatLifeAppData>(json);
        }
    }
}
