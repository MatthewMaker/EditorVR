using System;
using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEngine;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Gives decorated class the ability to add raycast sources to the system
    /// </summary>
    interface IUsesGetPointerEventData : IFunctionalitySubscriber<IProvidesGetPointerEventData>
    {
    }

    static class UsesGetPointerEventData
    {
        public static RayEventData GetPointerEventData(this IUsesGetPointerEventData user, Transform rayOrigin)
        {
#if FI_AUTOFILL
            return default(RayEventData);
#else
            return user.provider.GetPointerEventData(rayOrigin);
#endif
        }
    }
}
