// Convai 4.x Configuration UI uses Unity 6 UI Toolkit attributes. Unity 2022.3 does not ship these
// types. Define only the *Attribute* classes: [UxmlElement] -> UxmlElementAttribute, [UxmlAttribute] -> UxmlAttributeAttribute.
// Do not add UxmlElement/UxmlAttribute subclasses (Unity 6 / some 2022 patches expose a sealed base and cause CS0509).

#if UNITY_EDITOR && !UNITY_6000_0_OR_NEWER

namespace UnityEngine.UIElements
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
    public sealed class UxmlElementAttribute : System.Attribute
    {
        public UxmlElementAttribute()
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class UxmlAttributeAttribute : System.Attribute
    {
        public string name { get; set; }

        public UxmlAttributeAttribute()
        {
        }

        public UxmlAttributeAttribute(string uxmlAttributeName)
        {
            name = uxmlAttributeName;
        }
    }
}

#endif
