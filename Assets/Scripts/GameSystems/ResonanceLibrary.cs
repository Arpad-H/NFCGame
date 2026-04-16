// using UnityEngine;
//
// public class ResonanceLibrary : MonoBehaviour
// {
//     private static ResonanceLibrary _instance;
//     public static ResonanceLibrary Instance
//     {
//         get
//         {
//             if (_instance == null)
//             {
//                 _instance = FindFirstObjectByType<ResonanceLibrary>();
//                 if (_instance == null)
//                 {
//                     Debug.LogError("ResonanceLibrary is missing from the scene!");
//                 }
//             }
//             return _instance;
//         }
//     }
//     public ResonanceLibraryObject resonanceLibrary;
//
//     void Awake()
//     {
//         if (_instance == null)
//         {
//             _instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//         else if (_instance != this)
//         {
//             Destroy(gameObject);
//         }
//     }
//     
//     public Resonance GetResonance(ResonanceType type)
//     {
//         return resonanceLibrary.allResonances.Find(r => r.ResonanceType == type);
//     }
// }