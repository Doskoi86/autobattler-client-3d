using UnityEngine;
using System.Collections.Generic;

namespace AutoBattler.Client.Cards
{
    /// <summary>
    /// ScriptableObject qui mappe les noms de minions à leurs sprites d'artwork.
    /// Éditable dans l'Inspector : glisser-déposer les sprites pour chaque minion.
    ///
    /// 📋 CRÉER LA BASE D'ARTWORK :
    /// 1. Project → clic droit → Create → AutoBattler → Minion Art Database
    /// 2. Renommer "MinionArtDatabase"
    /// 3. Placer dans Assets/_Project/Data/
    /// 4. Dans l'Inspector, ajouter une entrée par minion :
    ///    - Taper le nom exact du minion (ex: "Recrue Humaine")
    ///    - Glisser le sprite depuis Assets/_Project/Sprites/Cards/Art/
    /// 5. Assigner ce SO dans CardFactory → "Minion Art Database"
    /// </summary>
    [CreateAssetMenu(fileName = "MinionArtDatabase", menuName = "AutoBattler/Minion Art Database")]
    public class MinionArtDatabase : ScriptableObject
    {
        [System.Serializable]
        public struct MinionArtEntry
        {
            [Tooltip("Nom exact du minion (doit correspondre au JSON)")]
            public string minionName;

            [Tooltip("Sprite d'artwork du minion")]
            public Sprite artwork;
        }

        [Header("Artwork par minion")]
        [SerializeField] private MinionArtEntry[] entries;

        [Header("Fallback")]
        [Tooltip("Sprite utilisé si le minion n'a pas d'artwork dédié")]
        [SerializeField] private Sprite defaultArtwork;

        // Cache pour lookup rapide (construit au premier accès)
        private Dictionary<string, Sprite> _cache;

        /// <summary>
        /// Retourne le sprite d'artwork pour un minion donné.
        /// Retourne le defaultArtwork si le minion n'est pas dans la base.
        /// </summary>
        public Sprite GetArtwork(string minionName)
        {
            if (_cache == null) BuildCache();

            if (!string.IsNullOrEmpty(minionName) && _cache.TryGetValue(minionName, out var sprite))
                return sprite;

            return defaultArtwork;
        }

        private void BuildCache()
        {
            _cache = new Dictionary<string, Sprite>();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.minionName) && entry.artwork != null)
                    _cache[entry.minionName] = entry.artwork;
            }
        }

        // Reset le cache si le SO est modifié dans l'éditeur
        private void OnValidate()
        {
            _cache = null;
        }
    }
}
