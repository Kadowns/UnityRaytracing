using UnityEngine;

namespace HawkTracer {
    public class Singleton<T> : MonoBehaviour where T : Singleton<T> {
        private static T m_instance;
        public static T Instance => m_instance != null ? m_instance : (m_instance = FindObjectOfType<T>());
    }
}