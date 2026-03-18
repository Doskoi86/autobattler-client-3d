using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Network
{
    /// <summary>
    /// Interface d'abstraction réseau. Le jeu ne sait jamais s'il parle
    /// au vrai serveur (SignalR) ou au MockServer (simulation locale).
    /// Toute communication passe par cette interface, jamais directement par le réseau.
    /// </summary>
    public interface IServerBridge
    {
        // --- État ---
        bool IsConnected { get; }
        string PlayerId { get; }
        string GameId { get; }

        // --- Connexion ---
        Task ConnectAsync(string playerName);
        Task DisconnectAsync();

        // --- Lobby ---
        Task JoinLobbyAsync();
        Task LobbyReadyAsync();
        Task LeaveLobbyAsync();

        // --- Sélection héros ---
        Task SelectHeroAsync(string heroId);

        // --- Actions shop (phase recrutement) ---
        Task BuyMinionAsync(int offerIndex);
        Task SellMinionAsync(string instanceId);
        Task PlayMinionAsync(string instanceId, int slotIndex);
        Task MoveMinionAsync(string instanceId, int toIndex);
        Task RefreshShopAsync();
        Task FreezeShopAsync();
        Task UpgradeTavernAsync();
        Task ReadyForCombatAsync();

        // --- Hero Power ---
        Task UseHeroPowerAsync();

        // --- Résultats ---
        Task AnimationDoneAsync();

        // --- Events : Connexion & Lobby ---
        event Action<string, string> OnConnected;                           // playerId, playerName
        event Action<List<PlayerInfo>, List<string>> OnLobbyJoined;         // players, readyIds
        event Action<string, string> OnLobbyPlayerJoined;                   // playerId, name
        event Action<string> OnLobbyPlayerReady;                            // playerId
        event Action<string> OnLobbyPlayerLeft;                             // playerId

        // --- Events : Initialisation partie ---
        event Action<string, List<PlayerInfo>> OnGameFound;                 // gameId, players
        event Action<List<HeroOffer>> OnHeroOffered;                        // offers
        event Action OnAllHeroesSelected;

        // --- Events : Phases ---
        event Action<string, int, int> OnPhaseStarted;                      // phase, turnNumber, durationSeconds
        event Action<List<PlayerPublicState>> OnPlayersUpdated;             // players

        // --- Events : Shop & Board ---
        event Action<List<ShopOfferState>, int, int> OnShopRefreshed;       // offers, gold, upgradeCost
        event Action<string, string, int> OnMinionBought;                   // instanceId, name, gold
        event Action<string, int> OnMinionSold;                             // instanceId, gold
        event Action OnShopFrozen;
        event Action<int, int, int> OnTavernUpgraded;                       // tier, gold, upgradeCost
        event Action<string, int> OnMinionPlayed;                           // instanceId, slotIndex
        event Action<List<MinionState>> OnBoardUpdated;                     // minions
        event Action<List<MinionState>> OnHandUpdated;                      // minions
        event Action<int, int> OnHeroHealthUpdated;                         // health, armor
        event Action<string, string> OnTripleFormed;                        // goldenId, goldenName

        // --- Events : Hero Power ---
        event Action<int, bool> OnHeroPowerUsed;                                  // gold, canUseAgain

        // --- Events : Combat ---
        event Action<List<CombatEventData>, List<BoardSnapshot>, List<BoardSnapshot>> OnCombatReplay;
        event Action<string, bool, int, string> OnCombatResult;             // opponentId, didWin, damage, log

        // --- Events : Fin de partie ---
        event Action<int> OnEliminated;                                     // finalRank
        event Action<string, int> OnPlayerEliminated;                       // playerId, finalRank
        event Action OnGameWon;
        event Action<List<RankingEntry>> OnGameOver;                        // rankings

        // --- Events : Erreurs ---
        event Action<string> OnError;                                       // message
    }
}
