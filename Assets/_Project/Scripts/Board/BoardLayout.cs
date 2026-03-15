using UnityEngine;

namespace AutoBattler.Client.Board
{
    /// <summary>
    /// Calcule les positions des slots en arc sur le plateau.
    /// Hearthstone utilise un arc très léger (pas une ligne droite)
    /// qui donne un aspect plus naturel et centré.
    /// Les positions se recentrent dynamiquement selon le nombre d'unités.
    /// </summary>
    public static class BoardLayout
    {
        /// <summary>
        /// Calcule les positions en arc pour un nombre donné de slots.
        /// </summary>
        /// <param name="count">Nombre de slots à positionner (1-7)</param>
        /// <param name="center">Centre de l'arc (position world du board)</param>
        /// <param name="spacing">Espacement entre chaque slot</param>
        /// <param name="arcAmount">Courbure de l'arc (0 = ligne droite, 1 = arc prononcé)</param>
        /// <returns>Tableau de positions world</returns>
        public static Vector3[] CalculatePositions(int count, Vector3 center, float spacing, float arcAmount)
        {
            if (count <= 0) return new Vector3[0];

            var positions = new Vector3[count];

            // Largeur totale occupée, centrée sur 0
            float totalWidth = (count - 1) * spacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * spacing;

                // Arc parabolique : le centre est plus avancé (Z+), les bords reculent
                // Normaliser x entre -1 et 1
                float normalized = count > 1 ? (2f * i / (count - 1) - 1f) : 0f;
                float z = -normalized * normalized * arcAmount;

                positions[i] = center + new Vector3(x, 0f, z);
            }

            return positions;
        }
    }
}
