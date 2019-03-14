using UnityEngine;

namespace Unity.Labs.SuperScience
{
    /// <summary>
    /// This component contains a Color property that is used by the Modification Response Example Window.
    /// Modification of this property triggers a delayed response where the window displays the average of
    /// the colors of all ColorContributors in the Scene.
    /// </summary>
    public class ColorContributor : MonoBehaviour
    {
        [SerializeField]
        Color m_Color;

        public Color color { get { return m_Color; } }
    }
}
