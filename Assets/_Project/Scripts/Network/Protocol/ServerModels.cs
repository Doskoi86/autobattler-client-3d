using System;
using System.Collections.Generic;

namespace AutoBattler.Client.Network.Protocol
{
    /// <summary>
    /// Modèles de données miroir du serveur SignalR.
    /// Ces classes correspondent aux payloads envoyés/reçus via le protocole.
    /// </summary>

    [Serializable]
    public class PlayerInfo
    {
        public string Id;
        public string Name;
    }

    [Serializable]
    public class HeroOffer
    {
        public string Id;
        public string Name;
        public string Description;
        public int HeroPowerCost;        // 0 = gratuit, N = coût en or
        public string HeroPowerType;     // "PassiveAlways", "ActiveFree", "ActiveCost", "Triggered"
    }

    [Serializable]
    public class MinionState
    {
        public string InstanceId;
        public string Name;
        public int Attack;
        public int Health;
        public int Tier;
        public string Keywords;      // "Taunt, DivineShield" (flags enum sérialisé)
        public string Description;
        public string Tribes;         // "Beast" ou "Mech, Beast" (comma-separated)
        public bool IsGolden;
    }

    [Serializable]
    public class ShopOfferState
    {
        public int Index;
        public string InstanceId;
        public string Name;
        public int Attack;
        public int Health;
        public int Tier;
        public bool Frozen;
        public string Keywords;
        public string Description;
        public string Tribes;
    }

    [Serializable]
    public class CombatEventData
    {
        public string Type;           // Attack, DeathrattleTrigger, RebornTrigger, CleaveHit
        public string AttackerId;
        public string TargetId;
        public int Damage;
        public bool DivineShieldPopped;
        public bool TargetDied;
        public bool AttackerDied;
        public string AttackerName;
        public string TargetName;
        public int AttackerAttack;
        public int TargetAttack;
        public int AttackerHealthAfter;
        public int TargetHealthAfter;
        public string TokenName;      // Pour Deathrattle/Reborn : nom du token invoqué
    }

    [Serializable]
    public class BoardSnapshot
    {
        public string InstanceId;
        public string Name;
        public int Attack;
        public int Health;
        public int Tier;
    }

    [Serializable]
    public class PlayerPublicState
    {
        public string PlayerId;
        public int Health;
        public int Armor;
        public int TavernTier;
        public bool IsEliminated;
    }

    [Serializable]
    public class RankingEntry
    {
        public string PlayerId;
        public string PlayerName;
        public int FinalRank;
    }
}
