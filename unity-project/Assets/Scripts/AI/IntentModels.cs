using System;
using UnityEngine;

namespace Aetheria.AI
{
    /// <summary>
    /// Matches POST /api/v1/intent/parse response (flat spawn payload for JsonUtility).
    /// See Docs/schemas/intent_response_v1.schema.json
    /// </summary>
    [Serializable]
    public class IntentParseHttpRequest
    {
        public string text;
        public string locale;
    }

    [Serializable]
    public class IntentParseResponse
    {
        public string version;
        public string intentType;
        public float confidence;
        public SpawnPayload spawn;
        public string[] errors;
    }

    [Serializable]
    public class SpawnPayload
    {
        public string catalogId;
        public string displayName;
        public float scale;
        public float tintR;
        public float tintG;
        public float tintB;
        public string difficultyTier;
        public string[] weaknessTags;
        public string placementMode;
        public float minDistanceM;
        public float maxDistanceM;
        public bool requireNavMesh;
    }
}
