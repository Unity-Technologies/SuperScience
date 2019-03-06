using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class ColorContributor : MonoBehaviour
    {
        [SerializeField]
        Color m_Color;

        public Color color { get { return m_Color; } }
    }
}
