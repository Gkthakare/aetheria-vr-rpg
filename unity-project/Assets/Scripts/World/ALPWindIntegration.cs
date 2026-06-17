using UnityEngine;
using System.Reflection;

namespace Aetheria.World
{
    /// <summary>
    /// Ensures ALP8310 Controller Global is present and synced with WindZone.
    /// Required for ALP Big Oak Tree wind sway. Add to IslandWorldManager.
    /// </summary>
    public class ALPWindIntegration : MonoBehaviour
    {
        [SerializeField] private WindZone windZone;

        void Awake()
        {
            if (windZone == null)
                windZone = GetComponent<WindZone>() ?? FindObjectOfType<WindZone>();
            if (windZone == null) return;

            var alpType = FindALP8310Type();
            if (alpType == null) return;

            var controller = GetComponent(alpType);
            if (controller == null)
                controller = gameObject.AddComponent(alpType);

            var windZoneField = alpType.GetField("windZone", BindingFlags.Public | BindingFlags.Instance);
            windZoneField?.SetValue(controller, windZone);

            var synchField = alpType.GetField("SynchWindZone", BindingFlags.Public | BindingFlags.Instance);
            synchField?.SetValue(controller, true);
        }

        private static System.Type FindALP8310Type()
        {
            var t = System.Type.GetType("ALP8310Controller, ALP8310_ControllerGlobal");
            if (t != null) return t;
            t = System.Type.GetType("ALP8310Controller");
            if (t != null) return t;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("ALP8310Controller");
                if (t != null) return t;
            }
            return null;
        }
    }
}
