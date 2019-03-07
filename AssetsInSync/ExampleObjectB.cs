using UnityEngine;

namespace Unity.Labs.SuperScience
{
    [CreateAssetMenu(menuName = "ExampleObjectB", fileName = "ExampleObjectB")]
    public class ExampleObjectB : ScriptableObject
    {
        [SerializeField]
        int m_ExampleField;

        public void UpdateFromObjectA(ExampleObjectA objectA)
        {
            m_ExampleField = objectA.exampleField * 10;
        }
    }
}
