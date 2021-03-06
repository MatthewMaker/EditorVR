using UnityEngine;

namespace Unity.EditorXR
{
    /// <summary>
    /// Provides an icon/sprite to display on a menu item which represents this class
    /// </summary>
    public interface IMenuIcon
    {
        /// <summary>
        /// The icon representing this class that can be displayed in menus
        /// </summary>
        Sprite icon { get; }
    }
}
