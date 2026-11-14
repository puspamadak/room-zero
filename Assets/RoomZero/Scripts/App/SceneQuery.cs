// Version-proof scene search.
// Unity 6.5+ made FindObjectsSortMode and FindFirstObjectByType obsolete (EntityId migration),
// while the replacement overloads don't exist in Unity 6000.0. Walking the loaded scenes'
// roots compiles and behaves the same on every Unity 6 version.
// Owner: #1 Tech Lead.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoomZero
{
    public static class SceneQuery
    {
        public static List<T> FindAll<T>(bool includeInactive = false) where T : Component
        {
            var result = new List<T>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    result.AddRange(root.GetComponentsInChildren<T>(includeInactive));
            }
            return result;
        }

        public static T FindFirst<T>(bool includeInactive = false) where T : Component
        {
            var all = FindAll<T>(includeInactive);
            return all.Count > 0 ? all[0] : null;
        }
    }
}
