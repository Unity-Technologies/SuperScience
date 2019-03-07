using UnityEngine;

namespace Unity.Labs.SuperScience
{
    [CreateAssetMenu(menuName = "ExampleObjectA", fileName = "ExampleObjectA")]
    public class ExampleObjectA : ScriptableObject
    {
        [SerializeField]
        ExampleObjectB m_OtherObject;

        [SerializeField]
        int m_ExampleField;

        public ExampleObjectB otherObject { get { return m_OtherObject; } }

        public int exampleField { get { return m_ExampleField; } }
    }
}
