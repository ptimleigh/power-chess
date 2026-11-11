using ChessFighter.Core;

namespace ChessFighter.Web.Game;

public enum PuzzleResult { None, Solved, Failed }

public enum InputMode { Normal, Armed, FireAim, LeapAim, BombPreview, Promotion }

/// <summary>What a debug tap does: give a piece a buff, drop a powerup square pair, drop teleporters, or clean up.</summary>
public enum DebugTool { None, Buff, Square, Portal, Clear, Unbuff, Stun }

/// <summary>A piece with a stable identity so CSS transitions can animate it as the engine state changes.</summary>
public sealed class PieceView
{
    public int Id;
    public PieceType Type;
    public Side Side;
    public int Square;
    public Buff Buffs;
    public int Stun;
    public int Armor;
    public int Ghost;
    /// <summary>Tier of the banked move on this piece (0 none, 1 = I, 2 = II).</summary>
    public int Tier;
    /// <summary>Briefly true after a teleport so the piece blinks across.</summary>
    public bool Warping;
    public bool Dying;
    /// <summary>Destroyed by the engine but kept on the board until a fireball reaches it.</summary>
    public bool PendingDeath;
    /// <summary>Dies in flames (hit by a fireball) rather than just fading.</summary>
    public bool Burning;
    public int AnimSeq;
}

public sealed class FxItem
{
    public int Id;
    public string Kind = "";
    public int Sq;
    public int To = -1;
    public int Radius;
    /// <summary>Flight time for projectile effects (fireball), in milliseconds.</summary>
    public int DurMs;
    public string Css = "";
    /// <summary>Words for a callout effect.</summary>
    public string Text = "";
}

public sealed class Toast
{
    public int Id;
    public string Title = "";
    public string Text = "";
    public string Css = "";
    public string Icon = "";
    /// <summary>Who this is about, with the label shown on the toast ("YOU", "RIVAL", "WHITE", "BLACK"). Null for general notes.</summary>
    public Side? Who;
    public string WhoLabel = "";
    /// <summary>How long it stays on screen, in milliseconds.</summary>
    public int Ms = 2600;
}

/// <summary>
/// The card-draw moment. The player sees a fan of face-down cards and taps one; the card inside was decided by the random draw
/// already, so the pick is for show. A rival's draw is a quick face-up flip.
/// </summary>
public sealed class DrawShow
{
    public CardKind Card;
    public Side Side;
    public string WhoLabel = "";
    /// <summary>True while the player is being asked to pick (the board is blocked and the rival waits).</summary>
    public bool Pick;
    /// <summary>0 waiting for a pick, 1 flipped and showing the card.</summary>
    public int Phase;
    public int Chosen = 1;
    public int Serial;
}

/// <summary>One line of the event log: what happened and to whom.</summary>
public sealed class LogEntry
{
    public Side? Who;
    public string WhoLabel = "";
    public string Text = "";
    public int Ply;
}

/// <summary>
/// Owns the engine state and turns engine events into animation state. The engine stays the single source of
/// truth; after every action the view pieces are reconciled against it.
/// </summary>
public sealed class GameSession
{
    readonly GameOptions opt;
    readonly Random rnd = new();
    Ai ai;
    int nextId = 1;
    int lastFireMs;

    // Position history for the move viewer and the Rewind perk. viewHistory[i] holds the piece views at position i
    // (the position before action i), so stepping through the past animates with the same piece identities.
    readonly GameHistory history = new();
    readonly List<List<PieceView>> viewHistory = new();
    int reviewIndex = -1; // -1 = live game

    public GameState State { get; private set; } = null!;
    public List<PieceView> Pieces { get; } = new();
    public List<FxItem> Effects { get; } = new();
    public List<Toast> Toasts { get; } = new();
    public List<GameAction> Legal { get; private set; } = new();
    public GameResult Result { get; private set; }
    public string ResultReason { get; private set; } = "";

    public bool Busy { get; private set; }
    public bool Thinking { get; private set; }
    public bool Shaking { get; private set; }
    /// <summary>1 = light, 2 = heavy. Scales with the value of what was just destroyed.</summary>
    public int ShakeLevel { get; private set; } = 1;
    /// <summary>Hit-stop: animations hold for a beat on a heavy strike.</summary>
    public bool Frozen { get; private set; }
    /// <summary>A brief zoom punch on the biggest strikes.</summary>
    public bool Punch { get; private set; }
    public InputMode Mode { get; private set; }
    public ActionKind ArmedKind { get; private set; }
    public int Selected { get; private set; } = -1;
    public HashSet<int> Targets { get; } = new();
    /// <summary>Extra pieces a queen's fireball would also destroy along the aimed lines (preview only).</summary>
    public HashSet<int> Doomed { get; } = new();
    /// <summary>Bomb preview: pieces inside the blast that survive (kings, armored pieces).</summary>
    public HashSet<int> Spared { get; } = new();
    /// <summary>Bomb preview: every square inside the blast.</summary>
    public HashSet<int> BlastArea { get; } = new();
    public int BombOwnLosses { get; private set; }
    public int BombEnemyLosses { get; private set; }
    /// <summary>Other bomb pieces the previewed blast would set off.</summary>
    public int BombChain { get; private set; }
    /// <summary>Square of the powered-up piece whose info card is open, or -1.</summary>
    public int Inspect { get; private set; } = -1;
    /// <summary>Square of the powerup square whose info card is open, or -1.</summary>
    public int InspectSquare { get; private set; } = -1;
    public List<GameAction> PromoChoices { get; private set; } = new();
    public int LastFrom { get; private set; } = -1;
    public int LastTo { get; private set; } = -1;
    public bool InCheck { get; private set; }
    public int Moves { get; private set; }

    public bool Reviewing => reviewIndex >= 0 && reviewIndex <= history.Count;
    public int HistoryCount => history.Count;
    public GameHistory History => history;
    /// <summary>Position being shown while reviewing (0 = start, <see cref="HistoryCount"/> = the final position). Equals <see cref="HistoryCount"/> when live.</summary>
    public int ReviewPosition => Reviewing ? reviewIndex : history.Count;
    /// <summary>What the board shows: the live game, or a past position while reviewing.</summary>
    public GameState View => Reviewing && reviewIndex < history.Count ? history[reviewIndex].Before : State;
    public List<PieceView> ViewPieces => Reviewing && reviewIndex < history.Count ? viewHistory[reviewIndex] : Pieces;
    public int ViewLastFrom => Reviewing ? LastOf(reviewIndex).from : LastFrom;
    public int ViewLastTo => Reviewing ? LastOf(reviewIndex).to : LastTo;
    public bool CanReview => history.Count > 0 && !Busy && !PickupPending;
    public bool CanRewind => HumanTurn && !Reviewing && history.CanRewind(State, Me);
    /// <summary>The side the player controls: White vs the AI, otherwise whoever is to move.</summary>
    public Side Me => VsAi ? HumanSide : State.ToMove;
    /// <summary>The side the player controls against the AI (White unless they chose Black or Random). Hot-seat games and puzzles are always White.</summary>
    public Side HumanSide { get; private set; } = Side.White;
    public Side RivalSide => HumanSide.Opponent();

    public Action? Changed;
    public Action<string>? Sfx;
    /// <summary>Called with a save string after every completed action, or null when there is nothing to resume (game over).</summary>
    public Action<string?>? Persist;

    // ---------- progression ----------
    MatchTracker? tracker;
    bool endReported;
    bool rewardable = true;
    public bool DebugUsed { get; private set; }

    // ---------- daily puzzle ----------
    public PuzzleDef? Puzzle { get; private set; }
    /// <summary>White actions spent so far in the puzzle.</summary>
    public int PuzzleMoves { get; private set; }
    public PuzzleResult PuzzleOutcome { get; private set; }
    /// <summary>Called once when a puzzle is solved.</summary>
    public Action<PuzzleDef, int>? PuzzleSolved;
    /// <summary>Called once when the game ends, with what happened, so the page can award XP.</summary>
    public Action<MatchSummary>? GameEnded;
    /// <summary>Whether finishing this game should pay XP: not a scenario, training board or debug-assisted game.</summary>
    public bool Rewardable => rewardable && !DebugUsed && Puzzle == null;

    // ---------- hints ----------

    public int HintsUsed { get; private set; }
    /// <summary>Extra hints earned this game (a rewarded ad in the native app).</summary>
    public int HintBonus { get; private set; }
    public int HintFrom { get; private set; } = -1;
    public int HintTo { get; private set; } = -1;
    public string HintText { get; private set; } = "";
    public bool HintThinking { get; private set; }
    public int HintsLeft => HintPolicy.Remaining(HintsUsed, HintBonus);
    /// <summary>A hint is offered on your turn against the AI, never in a puzzle or while reviewing.</summary>
    public bool HintAvailable => HumanTurn && VsAi && Puzzle == null && !HintThinking && !Thinking;

    public void ClearHint()
    {
        HintFrom = HintTo = -1;
        HintText = "";
    }

    public void AddHintBonus() { HintBonus++; }

    /// <summary>Finds the best move for you and highlights it. Costs one of the free hints.</summary>
    public async Task Hint()
    {
        if (!HintAvailable || HintsLeft == 0) return;
        await ShowHint("", true);
    }

    /// <summary>Summon Mars: the best move is shown automatically at the start of each of the owner's turns. Free, and no XP penalty.</summary>
    async Task MaybeMars()
    {
        if (GameOver || Reviewing || Puzzle != null || !HumanTurn) return;
        if (State.Mars[(int)State.Decider] == 0 || State.RespCard != CardKind.None || PickupPending) return;
        await ShowHint("Mars: ", false);
    }

    async Task ShowHint(string prefix, bool counts)
    {
        HintThinking = true;
        ClearSelection();
        Changed?.Invoke();
        await Task.Delay(60); // let the UI paint before the (synchronous) search
        GameAction act = Hints.Suggest(State);
        if (counts) HintsUsed++;
        switch (act.Kind)
        {
            case ActionKind.Move:
            case ActionKind.Fireball:
            case ActionKind.Leap:
            case ActionKind.StunBeam:
            case ActionKind.StealPick:
            case ActionKind.Steal:
            case ActionKind.Swap:
                HintFrom = act.From; HintTo = act.To; break;
            case ActionKind.Detonate:
                HintFrom = act.From; HintTo = -1; break;
            case ActionKind.Stun:
            case ActionKind.PlaceBoulder:
            case ActionKind.ClearBoulder:
                HintFrom = -1; HintTo = act.To; break;
            default:
                HintFrom = HintTo = -1; break;
        }
        HintText = prefix + Hints.Describe(State, act);
        HintThinking = false;
        AutoSelectForced(); // showing a hint clears the selection; mid-combo the forced piece is the only one that can be picked, so pick it again
        Sfx?.Invoke("pickup");
        Changed?.Invoke();
    }

    void ResetTracker(bool eligible)
    {
        HintsUsed = 0;
        HintBonus = 0;
        ClearHint();
        tracker = new MatchTracker(HumanSide, opt.Ai);
        endReported = false;
        rewardable = eligible;
        DebugUsed = false;
    }

    public GameSession(GameOptions options, TutorialState? tutorials = null)
    {
        opt = options;
        Tut = tutorials ?? new TutorialState();
        ai = Ai.ForLevel(opt.Ai);
        NewGame();
        if (opt.Scramble > 0) DebugScramble(opt.Scramble);
    }

    // ---------- powerup tutorials ----------


    public TutorialState Tut { get; }
    /// <summary>The powerup whose tutorial sheet is open, if any.</summary>
    public PowerupKind? Tutorial { get; private set; }
    /// <summary>Bound to the "don't show tutorials again" checkbox.</summary>
    public bool DontShowAgain { get; set; }
    /// <summary>Called with the serialised <see cref="TutorialState"/> whenever it changes.</summary>
    public Action<string>? TutorialsChanged;

    void MaybeTutorial(PowerupKind kind)
    {
        if (!opt.Tutorials || !Tut.ShouldShow(kind)) return;
        Tutorial = Tutorials.Normalize(kind);
        DontShowAgain = false;
    }

    /// <summary>Open a tutorial on request ("Watch demo"), whether or not it has been seen.</summary>
    public void ShowTutorial(PowerupKind kind)
    {
        if (!Tutorials.Has(kind)) return;
        Tutorial = Tutorials.Normalize(kind);
        DontShowAgain = false;
        Changed?.Invoke();
    }

    public void DismissTutorial()
    {
        if (Tutorial is PowerupKind k) Tut.MarkSeen(k);
        if (DontShowAgain) Tut.Off = true;
        Tutorial = null;
        DontShowAgain = false;
        TutorialsChanged?.Invoke(Tut.Serialize());
        Changed?.Invoke();
    }

    public void ResetTutorials()
    {
        Tut.Reset();
        Tutorial = null;
        TutorialsChanged?.Invoke(Tut.Serialize());
        Changed?.Invoke();
    }

    /// <summary>The pickup modal embeds the demo when this is true.</summary>
    public bool PickupTutorial => PickupPending && opt.Tutorials && Tut.ShouldShow(State.PendingKind);

    void AcknowledgePickupTutorial()
    {
        if (!PickupTutorial) return;
        Tut.MarkSeen(State.PendingKind);
        if (DontShowAgain) Tut.Off = true;
        DontShowAgain = false;
        TutorialsChanged?.Invoke(Tut.Serialize());
    }

    /// <summary>The Rewind button: explain it the first time, then use it.</summary>
    public void RewindClick()
    {
        if (opt.Tutorials && Tut.ShouldShow(PowerupKind.Rewind))
        {
            MaybeTutorial(PowerupKind.Rewind);
            Changed?.Invoke();
            return;
        }
        UseRewind();
    }

    public bool VsAi => opt.Ai > 0;
    public bool GameOver => Result != GameResult.Ongoing;
    bool AiToMove => VsAi && !GameOver && State.Decider == RivalSide;
    public bool HumanTurn => !GameOver && !Busy && !Reviewing && PuzzleOutcome == PuzzleResult.None && (!VsAi || State.Decider == HumanSide);
    public bool PickupPending => State.PendingSquare >= 0;

    /// <summary>A Stun or Steal square was just landed on and its target must be chosen now.</summary>
    public bool EffectPending => State.EffectKind != PowerupKind.None && !GameOver;

    /// <summary>The squares the pending Stun or Steal can hit.</summary>
    public IEnumerable<int> EffectTargets => Legal.Where(a => a.Kind == ActionKind.StunBeam || a.Kind == ActionKind.StealPick || a.Kind == ActionKind.Leap).Select(a => a.To);
    public Side Turn => State.ToMove;

    // ---------- setup ----------

    GameConfig BuildConfig()
    {
        // The player's level decides which powers, cards and upgrades exist (a level below zero opens everything).
        var cfg = new GameConfig { StartRerolls = opt.Rerolls, Pool = opt.Level >= 0 ? FighterUnlocks.PoolFor(opt.Level) : GameConfig.FighterPool(), UnlockLevel = opt.Level };
        if (opt.Pairs >= 0) cfg.InitialPairs = opt.Pairs;
        if (opt.Spawn >= 0) cfg.SpawnInterval = opt.Spawn;
        if (opt.Only.Count > 0) cfg.Pool = opt.Only.Select(k => new WeightedKind(k, 1)).ToList();
        cfg.RewindAllowed[(int)RivalSide] = !VsAi; // the AI cannot use Rewind, so it never draws it
        cfg.SwapAllowed[(int)RivalSide] = !VsAi; // nor Swap
        cfg.RewindAllowed[(int)HumanSide] = true;
        cfg.SwapAllowed[(int)HumanSide] = true;
        bool tele = opt.Level < 0 || opt.Level >= Progression.TeleporterLevel;
        cfg.PortalChance = opt.Portals && tele ? 0.12 : 0;
        cfg.Squares = opt.Mode;
        cfg.MysteryChance = opt.HiddenChance;
        if (opt.Only.Count == 0 && opt.Puzzle == null && !opt.Training)
        {
            int spawn = opt.Spawn;
            Presets.Apply(cfg, opt.Preset, opt.Freq, opt.Custom, opt.Level);
            if (spawn >= 0) cfg.SpawnInterval = spawn;
            if (!tele && cfg.PortalChance > 0) cfg.PortalChance = 0;
            if (!opt.Portals) cfg.PortalChance = 0;
        }
        return cfg;
    }

    void SetupPuzzle()
    {
        var def = opt.Puzzle!;
        State = def.NewState(PuzzleDef.Config());
        State.Rng = new Rng(opt.Seed);
        ai = Ai.ForLevel(2);
        Puzzle = def;
        PuzzleMoves = 0;
        PuzzleOutcome = PuzzleResult.None;
    }

    void SetupNormal(GameConfig cfg)
    {
        Puzzle = null;
        PuzzleOutcome = PuzzleResult.None;
        string? fen = opt.Fen; // the training game is the normal full board with two flame squares in the middle
        if (opt.Training)
        {
            // A first game on the full board against the Easy opponent: no random squares, just two Fireball squares in the middle.
            cfg.InitialPairs = 0;
            cfg.PortalChance = 0;
            cfg.Pool = new[] { PowerupKind.Fireball, PowerupKind.SpikedArmor, PowerupKind.Poison, PowerupKind.StealGrab, PowerupKind.SmallBomb }
                .Select(k => new WeightedKind(k, 1)).ToList();
        }

        if (fen == null)
        {
            State = GameState.NewGame(cfg, opt.Seed);
        }
        else
        {
            State = GameState.FromFen(fen, cfg);
            State.Rng = new Rng(opt.Seed);
            State.AddPerk(Side.White, PowerupKind.Reroll, cfg.StartRerolls);
            State.AddPerk(Side.Black, PowerupKind.Reroll, cfg.StartRerolls);
            for (int i = 0; i < cfg.InitialPairs; i++) Rules.TrySpawnPair(State, null, false);
        }

        foreach (var (side, kind, n) in opt.Perks) State.AddPerk(side, kind, n);
        foreach (var (side, card, n) in opt.Cards) State.AddCard(side, card, n);
        if (opt.PendingCard != CardKind.None) State.PendingCard = opt.PendingCard;
        foreach (var (sq, kind) in opt.Buffs)
        {
            int i = Sq.Parse(sq);
            if (!State.Cells[i].IsEmpty) Rules.ApplyPickup(State, State.Cells[i].Side, i, kind, null);
        }
        foreach (var sq in opt.Mystery) State.Mystery[Sq.Parse(sq)] = true;
        foreach (var sq in opt.PortalAt)
        {
            State.Portal[Sq.Parse(sq)] = true;
            State.Portal[Sq.Mirror(Sq.Parse(sq))] = true;
        }
        foreach (var (sq, kind) in opt.Reveal)
        {
            State.Mystery[Sq.Parse(sq)] = true;
            State.Content[Sq.Parse(sq)] = (byte)kind;
        }
        if (opt.Training)
        {
            // Two revealed fireball squares in the middle: the flagship powerup, visible from move one.
            foreach (var sq in new[] { "d4", "e5" })
            {
                State.Mystery[Sq.Parse(sq)] = true;
                State.Content[Sq.Parse(sq)] = (byte)PowerupKind.Fireball;
            }
            ai = Ai.ForLevel(1);
            CoachStep = 1;
        }

    }

    static int roundCounter;

    // ---------- coach: a short guided first game on the training board ----------

    /// <summary>0 = off. 1 move a piece, 2 grab a power square, 3 fire it, 4 well done.</summary>
    public int CoachStep { get; private set; }

    public string CoachText => CoachStep switch
    {
        1 => "Welcome! Tap one of your blue pieces, then tap a square to move it. Checkmate the red king to win.",
        2 => "The gold squares are power squares: land a piece on one to collect it. Your d-pawn can reach the flame square on d4 in one move!",
        3 => Legal.Any(a => a.Kind == ActionKind.Fireball)
            ? "That piece now holds a Fireball. Tap it, press the Fireball button, then tap a red piece to blast it."
            : "That piece now holds a Fireball! It fires like a rook. Line it up with a red piece on the same row or column, then tap it and press Fireball.",
        4 => "Nice shot! Capture pieces and checkmate the king to win. Tap ? any time for help.",
        _ => "",
    };

    public int CoachCount => 4;

    /// <summary>Squares to pulse for the current step.</summary>
    public HashSet<int> CoachHighlights
    {
        get
        {
            var set = new HashSet<int>();
            if (CoachStep == 2)
            {
                for (int sq = 0; sq < 64; sq++) if (State.Mystery[sq] && State.Content[sq] != 0) set.Add(sq);
            }
            else if (CoachStep == 3)
            {
                for (int sq = 0; sq < 64; sq++) { var c = State.Cells[sq]; if (!c.IsEmpty && c.Side == Side.White && c.Has(Buff.Fireball)) set.Add(sq); }
            }
            return set;
        }
    }

    public void EndCoach()
    {
        CoachStep = 0;
        Changed?.Invoke();
    }

    /// <summary>Moves the coach on when the player has done what the current step asked.</summary>
    void AdvanceCoach(GameEvent e)
    {
        if (CoachStep == 0 || e.Side != Side.White) return;
        if (CoachStep == 1 && e.Kind == EventKind.Moved) CoachStep = 2;
        if (CoachStep == 2 && e.Kind == EventKind.PickupFound && e.Powerup == PowerupKind.Fireball) CoachStep = 3;
        else if (CoachStep == 3 && e.Kind == EventKind.FireballFired) CoachStep = 4;
        else if (CoachStep == 4 && e.Kind == EventKind.Moved) CoachStep = 0;
    }

    /// <summary>Counts up across the page's lifetime, each time any game starts. Drives the ROUND n banner (and replays it).</summary>
    public int GameSerial { get; private set; }

    /// <summary>True for a freshly started game, false when a saved game was restored (no intro when resuming).</summary>
    public bool RoundIntro { get; private set; }

    public void NewGame()
    {
        GameSerial = ++roundCounter;
        RoundIntro = true;
        // Who the player is: White unless they chose Black (or Random). Puzzles, the guided first game and two-player games are always White.
        HumanSide = Side.White;
        if (opt.Ai > 0 && opt.Puzzle == null && !opt.Training)
            HumanSide = opt.PlayAs == "black" ? Side.Black : opt.PlayAs == "random" ? ((new Random().Next(2) == 0) ? Side.White : Side.Black) : Side.White;
        var cfg = BuildConfig();

        if (opt.Puzzle != null) SetupPuzzle();
        else SetupNormal(cfg);

        ResetTracker(opt.Fen == null || opt.Rewards);
        Pieces.Clear();
        Effects.Clear();
        Draw = null;
        Toasts.Clear();
        toastQueue.Clear();
        Log.Clear();
        history.Clear();
        viewHistory.Clear();
        reviewIndex = -1;
        for (int sq = 0; sq < 64; sq++)
        {
            var c = State.Cells[sq];
            if (!c.IsEmpty) Pieces.Add(new PieceView { Id = nextId++, Square = sq });
        }
        SyncViews();
        LastFrom = LastTo = -1;
        Moves = 0;
        Busy = false;
        Thinking = false;
        ClearSelection();
        Refresh();
        Changed?.Invoke();
        Save();
    }

    public void Restart() => NewGame();

    /// <summary>
    /// Debug: jump to a random position that looks like the middle of a game. It starts a fresh game with the current options, plays
    /// random legal actions (moves, powers, cards, Nuh-uh answers) until about <paramref name="plies"/> plies have passed, and stops on
    /// the human's turn with nothing pending. Captured pieces, powers, powerup squares, card bags and Take back history all come from
    /// real play, so the position is always a legal one. Games that end early are discarded and replayed.
    /// </summary>
    public void DebugScramble(int plies)
    {
        if (Busy || Puzzle != null || opt.Puzzle != null) return;
        var rnd = new Random();
        int budget = Math.Max(40, plies * 4);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            NewGame();                                           // a clean setup that honours the options
            State.Rng = new Rng((ulong)rnd.NextInt64() | 1UL);   // so powerup squares and draws differ every time
            int steps = 0;
            bool landed = false;
            while (steps++ < budget)
            {
                if (Rules.GetResult(State) != GameResult.Ongoing) break;
                var legal = MoveGen.LegalActions(State);
                if (legal.Count == 0) break;
                bool settled = State.PendingSquare < 0 && State.PendingCard == CardKind.None && State.EffectKind == PowerupKind.None
                    && State.RespCard == CardKind.None && State.ForcedMover < 0;
                if (State.Ply >= plies && settled && (!VsAi || State.ToMove == Side.White)) { landed = true; break; }
                Rules.Apply(State, legal[rnd.Next(legal.Count)]);
            }
            if (!landed) continue;

            Pieces.Clear();
            for (int sq = 0; sq < 64; sq++)
                if (!State.Cells[sq].IsEmpty) Pieces.Add(new PieceView { Id = nextId++, Square = sq });
            SyncViews();
            history.Clear();
            viewHistory.Clear();
            reviewIndex = -1;
            LastFrom = LastTo = -1;
            Moves = State.Ply;
            RoundIntro = false;
            DebugUsed = true;
            ClearSelection();
            Refresh();
            AddToast("SCRAMBLED", $"A random position {State.Ply} plies in", "gold", "spark");
            Changed?.Invoke();
            Save();
            return;
        }
        AddToast("Scramble failed", "Every random game ended too early. Try again.", "gold", "skull");
        Changed?.Invoke();
    }

    // ---------- save / resume ----------

    /// <summary>Serialise the game (engine state, history and piece identities) so a reload can pick up where it left off.</summary>
    public string SaveString()
    {
        var ids = new List<byte>();
        void AddIds(List<PieceView> views)
        {
            var map = new ushort[64];
            foreach (var v in views) if (!v.Dying) map[v.Square] = (ushort)v.Id;
            foreach (var id in map) { ids.Add((byte)(id & 255)); ids.Add((byte)(id >> 8)); }
        }
        foreach (var views in viewHistory) AddIds(views);
        AddIds(Pieces);
        return string.Join("|", GameStateCodec.EncodeGame(State, history), nextId, opt.Ai, Convert.ToBase64String(ids.ToArray()), opt.Mode == SquareMode.Revealed ? "r" : "m", HumanSide == Side.Black ? "b" : "w");
    }

    /// <summary>Restore a saved game. Returns false (leaving the current game untouched) if the save is not usable.</summary>
    public bool Restore(string saved)
    {
        try
        {
            var parts = saved.Split('|');
            if (parts.Length < 4 || parts.Length > 6) return false;
            int savedNextId = int.Parse(parts[1]);
            int savedAi = int.Parse(parts[2]);
            var idBytes = Convert.FromBase64String(parts[3]);

            var oldSide = HumanSide;
            HumanSide = parts.Length == 6 && parts[5] == "b" ? Side.Black : Side.White;   // older saves are all White
            int oldAi = opt.Ai;
            var oldMode = opt.Mode;
            opt.Ai = Math.Clamp(savedAi, 0, 6);
            opt.Mode = parts.Length >= 5 && parts[4] == "r" ? SquareMode.Revealed : SquareMode.Mystery; // older saves were all mystery
            GameState state;
            GameHistory hist;
            if (!GameStateCodec.TryDecodeGame(parts[0], BuildConfig(), out state, out hist) || idBytes.Length != (hist.Count + 1) * 128)
            {
                opt.Ai = oldAi;
                opt.Mode = oldMode;
                HumanSide = oldSide;
                return false;
            }

            List<PieceView> ViewsFor(GameState at, int index)
            {
                var list = new List<PieceView>();
                for (int sq = 0; sq < 64; sq++)
                {
                    int id = idBytes[index * 128 + sq * 2] | (idBytes[index * 128 + sq * 2 + 1] << 8);
                    var c = at.Cells[sq];
                    if (c.IsEmpty) continue;
                    if (id == 0) throw new InvalidOperationException("missing piece id");
                    list.Add(new PieceView { Id = id, Type = c.Type, Side = c.Side, Square = sq, Buffs = c.Buffs, Stun = c.StunTurns, Armor = c.ArmorTurns, Ghost = c.GhostTurns, Tier = c.Tier });
                }
                return list;
            }

            var newViews = new List<List<PieceView>>();
            for (int i = 0; i < hist.Count; i++) newViews.Add(ViewsFor(hist[i].Before, i));
            var live = ViewsFor(state, hist.Count);

            history.Clear();
            for (int i = 0; i < hist.Count; i++) history.Record(hist[i].Before, hist[i].Action);
            viewHistory.Clear();
            viewHistory.AddRange(newViews);
            Pieces.Clear();
            Pieces.AddRange(live);
            State = state;
            ResetTracker(true);
            nextId = Math.Max(savedNextId, 1);
            ai = Ai.ForLevel(opt.Ai);
            Effects.Clear();
            Toasts.Clear();
            toastQueue.Clear();
            reviewIndex = -1;
            Busy = false;
            Thinking = false;
            var (lf, lt) = LastOf(history.Count);
            LastFrom = lf;
            LastTo = lt;
            ClearSelection();
            Inspect = -1;
            Refresh();
            AddToast("Game restored", "Picked up where you left off", "gold", "history");
            RoundIntro = false;
            Changed?.Invoke();
            Save();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>After a restore, let the AI move if it is its turn.</summary>
    public async Task ResumeAi()
    {
        await AiLoop();
    }

    void Save()
    {
        if (Persist == null) return;
        Persist(GameOver ? null : SaveString());
    }

    // ---------- input ----------

    public int Perk(Side side, PowerupKind kind) => State.Perks(side, kind);

    public bool CanUse(ActionKind kind) => HumanTurn && !PickupPending && State.ForcedMover < 0 && Legal.Any(a => a.Kind == kind);

    public bool HasDetonate => Selected >= 0 && Mode == InputMode.Normal && Legal.Any(a => a.Kind == ActionKind.Detonate && a.From == Selected);

    /// <summary>Show exactly what the selected bomber would destroy, then wait for a confirm.</summary>
    public void PreviewBomb()
    {
        if (!HasDetonate) return;
        int bomber = Selected;
        var piece = State.Cells[bomber];
        Mode = InputMode.BombPreview;
        Targets.Clear();
        Doomed.Clear();
        Spared.Clear();
        BlastArea.Clear();
        BombOwnLosses = BombEnemyLosses = 0;
        // The whole blast, chain reactions included: every bomb piece it reaches goes off too, and the bomber is destroyed with the rest.
        var zone = new bool[64];
        var order = new List<int>();
        Geometry.BlastChain(State, bomber, zone, order);
        BombChain = order.Count - 1;
        for (int sq = 0; sq < 64; sq++)
        {
            if (!zone[sq]) continue;
            BlastArea.Add(sq);
            var c = State.Cells[sq];
            if (Geometry.IsVulnerableToBomb(c))
            {
                Doomed.Add(sq);
                if (c.Side == piece.Side) BombOwnLosses++; else BombEnemyLosses++;
            }
            else if (State.Boulders[sq]) Doomed.Add(sq);
            else if (!c.IsEmpty && sq != bomber) Spared.Add(sq);
        }
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    /// <summary>The selected piece of the player on the move holds a Fireball (whether or not anything is in line yet).</summary>
    public bool HoldsFireball => Selected >= 0 && Mode == InputMode.Normal && HumanTurn && State.Cells[Selected].Has(Buff.Fireball);
    public bool HoldsLeap => Selected >= 0 && Mode == InputMode.Normal && HumanTurn && State.Cells[Selected].Has(Buff.Leap);

    /// <summary>The selected piece earned a fireball and has at least one enemy in line.</summary>
    public bool HasFireball => Selected >= 0 && Mode == InputMode.Normal && Legal.Any(a => a.Kind == ActionKind.Fireball && a.From == Selected);

    /// <summary>Step 2 of firing: show the directions this piece can hit and let the player tap one.</summary>
    public void StartFire()
    {
        if (!HasFireball) return;
        Mode = InputMode.FireAim;
        Targets.Clear();
        Doomed.Clear();
        foreach (var c in Legal.Where(a => a.Kind == ActionKind.Fireball && a.From == Selected))
        {
            Targets.Add(c.To);
            var line = new List<int>();
            Geometry.FireballVictims(State, c.From, c.Aux, line);
            foreach (int v in line) if (v != c.To) Doomed.Add(v);
        }
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    // ---------- cards ----------

    /// <summary>Whose decision the game is waiting for (the rival while a card awaits a Nuh-uh answer).</summary>
    public Side Decider => State.Decider;

    /// <summary>A card was just activated and the rival may cancel it with Nuh-uh.</summary>
    /// <summary>True for a moment after the board flips, for the CSS rotation.</summary>
    public bool Flipping { get; private set; }
    bool rebuildViews;

    public bool Responding => State.RespCard != CardKind.None && !GameOver;

    /// <summary>The bag card the player picked to play, or None.</summary>
    public CardKind SelectedCard { get; private set; }

    /// <summary>The card being aimed: an immediate card just drawn, else the one picked from the bag.</summary>
    public CardKind ActiveCard => State.PendingCard != CardKind.None ? State.PendingCard : SelectedCard;

    int cardFrom = -1, cardTo = -1, cardExtra = -1, cardInputs;

    public enum CardStep { None, Square, Extra, Confirm }

    List<GameAction> CardRemaining() => Legal.Where(a => a.Kind == ActionKind.PlayCard && Cards.KindOf(a.Aux) == ActiveCard
        && (cardFrom < 0 || a.From == cardFrom) && (cardTo < 0 || a.To == cardTo) && (cardExtra < 0 || Cards.ExtraOf(a.Aux) == cardExtra)).ToList();

    /// <summary>
    /// What the active card needs next, worked out from the legal actions that are still possible: tap a square, choose a piece
    /// type, or (for a bag card that needed no input at all) confirm. Caissa asks for the piece first, then the square.
    /// </summary>
    public CardStep NextCardStep(out List<int> squares, out List<int> extras)
    {
        squares = new List<int>();
        extras = new List<int>();
        if (ActiveCard == CardKind.None || !HumanTurn || Responding) return CardStep.None;
        var acts = CardRemaining();
        if (acts.Count == 0) return CardStep.None;
        extras = acts.Select(a => Cards.ExtraOf(a.Aux)).Distinct().OrderBy(x => x).ToList();
        if (ActiveCard == CardKind.SummonCaissa && cardExtra < 0 && extras.Count > 1) return CardStep.Extra;
        bool samePos = acts.All(a => a.From == a.To);
        if (samePos)
        {
            var pos = acts.Select(a => a.From).Distinct().ToList();
            if (pos.Count > 1 && cardFrom < 0) { squares = pos; return CardStep.Square; }
        }
        else
        {
            var froms = acts.Select(a => a.From).Distinct().ToList();
            if (froms.Count > 1 && cardFrom < 0) { squares = froms; return CardStep.Square; }
            var tos = acts.Select(a => a.To).Distinct().ToList();
            if (tos.Count > 1 && cardTo < 0) { squares = tos; return CardStep.Square; }
        }
        if (extras.Count > 1 && cardExtra < 0) return CardStep.Extra;
        if (cardInputs == 0) return CardStep.Confirm;
        return CardStep.None;
    }

    /// <summary>The squares to highlight for the current step. A row or quadrant card lights the whole row or quadrant.</summary>
    public IEnumerable<int> CardTargets
    {
        get
        {
            var step = NextCardStep(out var squares, out _);
            if (step != CardStep.Square) return Enumerable.Empty<int>();
            if (CardPreview >= 0) squares = new List<int> { CardPreview };
            if (ActiveCard == CardKind.DragonStrike)
                return squares.SelectMany(a => Enumerable.Range(0, 8).Select(f => Sq.Make(f, Sq.Rank(a))));
            if (ActiveCard == CardKind.MissileStrike)
                return squares.SelectMany(a => Enumerable.Range(0, 16).Select(i => Sq.Make(Sq.File(a) + i % 4, Sq.Rank(a) + i / 4)));
            return squares;
        }
    }

    /// <summary>A tap anywhere in a row or quadrant selects its anchor square, which is what the engine's action carries.</summary>
    static int CardAnchor(CardKind card, int sq)
    {
        if (card == CardKind.DragonStrike) return Sq.Make(0, Sq.Rank(sq));
        if (card == CardKind.MissileStrike) return Sq.Make(Sq.File(sq) / 4 * 4, Sq.Rank(sq) / 4 * 4);
        return sq;
    }

    void ResetCardChoice()
    {
        SelectedCard = CardKind.None;
        cardFrom = cardTo = cardExtra = -1;
        cardInputs = 0;
        CardPreview = -1;
        CardPreviewNote = "";
        Doomed.Clear();
    }

    public async Task SelectCard(CardKind kind)
    {
        if (!HumanTurn || PickupPending || EffectPending || Responding || State.ForcedMover >= 0) return;
        if (State.PendingCard != CardKind.None) return;
        if (State.CardCount(Turn, kind) == 0) return;
        ClearSelection();
        ResetCardChoice();
        SelectedCard = kind;
        Sfx?.Invoke("click");
        await AdvanceCard();
        if (SelectedCard == CardKind.None)
        {
            // The card had no legal use: say why instead of silently doing nothing.
            AddToast("Can't play " + Names.CardName(kind), WhyCardBlocked(kind), "stun", Names.CardIcon(kind));
            Sfx?.Invoke("thud");
            Changed?.Invoke();
        }
    }

    /// <summary>A short reason a card has no legal use right now.</summary>
    string WhyCardBlocked(CardKind kind)
    {
        var pseudo = new List<GameAction>();
        Cards.Generate(State, Turn, kind, pseudo);
        if (pseudo.Count > 0) return "It would leave your king in check.";
        switch (kind)
        {
            case CardKind.NuhUh: return "Nuh-uh answers a card your rival plays.";
            case CardKind.MissileStrike: return "A missile is already on its way.";
            case CardKind.SummonCaissa: return State.Captured.Skip((int)Turn * 8).Take(8).Sum(x => (int)x) == 0 ? "You haven't lost a piece yet." : "There is no free square to place it.";
            case CardKind.SummonShiva: return "No rival piece can be struck. Kings and armored pieces are safe.";
            case CardKind.DragonStrike: return "No row from 3 to 6 has a piece to burn without a king in it.";
            case CardKind.TakeBack: return "There isn't enough history to rewind yet.";
            case CardKind.SmashBoulder: return "There is no boulder on the board.";
            case CardKind.HorseSacrifice: return "You have no knight to sacrifice.";
            case CardKind.NoMorePowers: return "There are no powers or powerup squares to clear.";
            default: return "It has no legal use right now.";
        }
    }

    public void CancelCard()
    {
        if (State.PendingCard != CardKind.None) return; // an immediate card has to be used
        ResetCardChoice();
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    /// <summary>Plays the card as soon as nothing more is needed from the player.</summary>
    async Task AdvanceCard()
    {
        var step = NextCardStep(out _, out _);
        if (step == CardStep.None)
        {
            var acts = CardRemaining();
            if (acts.Count == 0) { ResetCardChoice(); Changed?.Invoke(); return; }
            await Human(acts[0]);
            return;
        }
        Changed?.Invoke();
    }

    /// <summary>The row or quadrant the player has aimed at but not yet fired.</summary>
    public int CardPreview { get; private set; } = -1;
    public string CardPreviewNote { get; private set; } = "";

    void ShowStrikePreview(int anchor)
    {
        CardPreview = anchor;
        Doomed.Clear();
        int mine = 0, theirs = 0;
        if (ActiveCard == CardKind.DragonStrike)
        {
            int rank = Sq.Rank(anchor);
            for (int f = 0; f < 8; f++)
            {
                int sq = Sq.Make(f, rank);
                var c = State.Cells[sq];
                if (!Geometry.IsVulnerableToBomb(c)) continue;
                Doomed.Add(sq);
                if (c.Side == Turn) mine++; else theirs++;
            }
            CardPreviewNote = $"The dragon burns {theirs} rival and {mine} of your own pieces.";
        }
        else
        {
            for (int i = 0; i < 16; i++)
            {
                var c = State.Cells[Sq.Make(Sq.File(anchor) + i % 4, Sq.Rank(anchor) + i / 4)];
                if (c.IsEmpty || c.Type == PieceType.King) continue;
                if (c.Side == Turn) mine++; else theirs++;
            }
            CardPreviewNote = $"Lands in 10 moves and destroys everything there (a king loses the game). Right now: {theirs} rival, {mine} yours.";
        }
    }

    public async Task ConfirmPreview()
    {
        if (CardPreview < 0) return;
        cardFrom = cardTo = CardPreview;
        cardInputs++;
        await AdvanceCard();
    }

    public async Task ConfirmCard()
    {
        if (NextCardStep(out _, out _) != CardStep.Confirm) return;
        cardInputs++;
        await AdvanceCard();
    }

    public async Task ChooseCardExtra(int extra)
    {
        cardExtra = extra;
        cardInputs++;
        await AdvanceCard();
    }

    async Task CardTap(int sq)
    {
        var step = NextCardStep(out var squares, out _);
        sq = CardAnchor(ActiveCard, sq);
        if (step != CardStep.Square || !squares.Contains(sq)) { Sfx?.Invoke("click"); return; }
        if ((ActiveCard == CardKind.DragonStrike || ActiveCard == CardKind.MissileStrike) && CardPreview != sq)
        {
            // First tap shows what the strike will hit; a second tap (or Play) fires it.
            ShowStrikePreview(sq);
            Sfx?.Invoke("click");
            Changed?.Invoke();
            return;
        }
        var acts = CardRemaining();
        if (acts.All(a => a.From == a.To)) { cardFrom = sq; cardTo = sq; }
        else if (cardFrom < 0 && acts.Select(a => a.From).Distinct().Count() > 1) cardFrom = sq;
        else cardTo = sq;
        cardInputs++;
        await AdvanceCard();
    }

    /// <summary>Answer a card the rival just played: cancel it with Nuh-uh or let it through.</summary>
    public async Task Respond(bool nuhUh)
    {
        if (!Responding || !HumanTurn) return;
        var act = Legal.FirstOrDefault(a => a.Kind == (nuhUh ? ActionKind.UseNuhUh : ActionKind.AllowCard));
        if (act.Kind != (nuhUh ? ActionKind.UseNuhUh : ActionKind.AllowCard)) return;
        await Human(act);
    }

    public bool CanRespondNuhUh => Responding && State.CardCount(State.Decider, CardKind.NuhUh) > 0;

    string CardPrompt(CardStep step)
    {
        string name = Names.CardName(ActiveCard);
        if (step == CardStep.Confirm) return ActiveCard == CardKind.SummonMars ? $"Play {name}? It doesn't cost your turn." : $"Play {name}? It costs your turn.";
        if (step == CardStep.Extra) return ActiveCard == CardKind.SummonCaissa ? "Caïssa: which piece comes back?" : $"{name}: choose what it becomes";
        bool second = cardFrom >= 0;
        switch (ActiveCard)
        {
            case CardKind.PlaceBoulder: return "Boulder: tap an empty square";
            case CardKind.SmashBoulder: return "Smash: tap the boulder to remove";
            case CardKind.Promotion: return "Promotion: tap the piece to promote";
            case CardKind.RoleReversal: return second ? "Role reversal: tap the piece to swap with" : "Role reversal: tap the first piece";
            case CardKind.HorseSacrifice: return second ? "Horse sacrifice: tap the piece that becomes a rook" : "Horse sacrifice: tap the knight to sacrifice";
            case CardKind.SummonCaissa: return "Caïssa: tap an empty square";
            case CardKind.DragonStrike: return "Dragon: tap a row to burn";
            case CardKind.MissileStrike: return "Missile: tap the quadrant to strike";
            default: return name;
        }
    }

    /// <summary>The selected piece holds a Leap with a rival to jump.</summary>
    public bool HasLeap => Selected >= 0 && Mode == InputMode.Normal && Legal.Any(a => a.Kind == ActionKind.Leap && a.From == Selected);

    /// <summary>Show where the Leap can land; the jumped rivals are marked, and tapping a landing square makes the jump.</summary>
    public async Task StartLeap()
    {
        if (!HasLeap) return;
        var cands = Legal.Where(a => a.Kind == ActionKind.Leap && a.From == Selected).ToList();
        Mode = InputMode.LeapAim;
        Targets.Clear();
        Doomed.Clear();
        Spared.Clear();
        foreach (var c in cands) { Targets.Add(c.To); Doomed.Add(Geometry.LeapOver(c.From, c.To)); }
        Sfx?.Invoke("click");
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    /// <summary>A Leap chain is waiting: stop jumping and keep the piece where it is.</summary>
    public async Task LeapStop()
    {
        var stop = Legal.FirstOrDefault(a => a.Kind == ActionKind.LeapStop);
        if (stop.Kind == ActionKind.LeapStop && HumanTurn) await Human(stop);
    }

    public void ClearSelection()
    {
        Selected = -1;
        Targets.Clear();
        Doomed.Clear();
        Spared.Clear();
        BlastArea.Clear();
        Mode = InputMode.Normal;
        PromoChoices = new List<GameAction>();
    }

    public void CloseInspect()
    {
        Inspect = -1;
        InspectSquare = -1;
        Changed?.Invoke();
    }

    public bool IsPowered(int sq)
    {
        var c = State.Cells[sq];
        return !c.IsEmpty && (c.Buffs != Buff.None || c.IsStunned);
    }

    public void Cancel()
    {
        Inspect = -1;
        InspectSquare = -1;
        ClearSelection();
        AutoSelectForced();
        Changed?.Invoke();
    }

    void AutoSelectForced()
    {
        if (State.ForcedMover >= 0 && HumanTurn) SelectPiece(State.ForcedMover);
    }

    public void Arm(ActionKind kind)
    {
        if (!CanUse(kind) && !(Mode == InputMode.Armed && ArmedKind == kind)) return;
        if (Mode == InputMode.Armed && ArmedKind == kind)
        {
            Cancel();
            return;
        }
        ClearSelection();
        Mode = InputMode.Armed;
        ArmedKind = kind;
        MaybeTutorial(kind switch
        {
            ActionKind.Steal => PowerupKind.Steal,
            ActionKind.Stun => PowerupKind.Stun,
            ActionKind.PlaceBoulder => PowerupKind.Boulder,
            ActionKind.Swap => PowerupKind.Swap,
            _ => PowerupKind.Reroll,
        });
        foreach (var a in Legal.Where(a => a.Kind == kind))
        {
            Targets.Add(a.To);
            if (kind == ActionKind.Swap) Targets.Add(a.From); // swap is two taps: any piece with a partner first
        }
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    void SelectPiece(int sq)
    {
        ClearSelection();
        bool mine = !State.Cells[sq].IsEmpty && State.Cells[sq].Side == State.ToMove;
        if (!mine) return;
        var acts = Legal.Where(a => a.From == sq && a.Kind == ActionKind.Move).ToList();
        bool special = Legal.Any(a => (a.Kind == ActionKind.Detonate || a.Kind == ActionKind.Fireball || a.Kind == ActionKind.Leap) && a.From == sq)
            || State.Cells[sq].Has(Buff.Fireball) || State.Cells[sq].Has(Buff.Leap);
        if (acts.Count == 0 && !special) return;
        Selected = sq;
        foreach (var a in acts) Targets.Add(a.To);
        Sfx?.Invoke("click");
        var held = State.Cells[sq];
        foreach (var flag in new[] { Buff.Fireball, Buff.Leap, Buff.DoubleMove, Buff.TripleMove, Buff.Ghost, Buff.Poison, Buff.Armor, Buff.SmallBomb, Buff.BigBomb })
        {
            if (!held.Has(flag)) continue;
            var kind = flag == Buff.Armor ? PowerupKind.SpikedArmor : Names.ForBuff(flag);
            if (opt.Tutorials && Tut.ShouldShow(kind)) { MaybeTutorial(kind); break; }
        }
    }

    // ---------- debug tools (for trying powerups on demand) ----------

    public DebugTool DebugArmed { get; private set; }
    public PowerupKind DebugKind { get; private set; }
    /// <summary>How many times a Buff tap applies its power: 2 turns Fireball I into II, Bomb I into II, Leap I into II.</summary>
    public int DebugTimes { get; private set; } = 1;
    /// <summary>Cards from the debug panel go to the rival's bag instead of yours.</summary>
    public bool DebugCardsToRival { get; private set; }

    /// <summary>Give a card to your bag (or the rival's).</summary>
    public void DebugGiveCard(CardKind kind)
    {
        if (Busy) return;
        DebugUsed = true;
        State.AddCard(DebugCardsToRival ? Me.Opponent() : Me, kind, 1);
        Sfx?.Invoke("pickup");
        Refresh();
        Save();
        Changed?.Invoke();
    }

    public void DebugToggleCardSide()
    {
        DebugCardsToRival = !DebugCardsToRival;
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    public void DebugClearBags()
    {
        if (Busy) return;
        DebugUsed = true;
        Array.Clear(State.Bag, 0, State.Bag.Length);
        Refresh();
        Save();
        Changed?.Invoke();
    }

    /// <summary>Wipe every power from every piece and every powerup square, like No More Powers.</summary>
    public void DebugClearBoard()
    {
        if (Busy) return;
        DebugUsed = true;
        for (int i = 0; i < 64; i++)
        {
            var p = State.Cells[i];
            if (!p.IsEmpty) { p.Buffs = Buff.None; p.Tier = 0; p.GhostTurns = 0; p.PoisonTurns = 0; p.ArmorTurns = 0; p.StunTurns = 0; State.Cells[i] = p; }
            State.Mystery[i] = false;
            State.Content[i] = 0;
        }
        State.MissileLeft = 0;
        State.MissileAnchor = -1;
        State.Mars[0] = State.Mars[1] = 0;
        SyncViews();
        Refresh();
        Save();
        Changed?.Invoke();
    }

    /// <summary>Drop one more mirrored pair of powerup squares, as the game would.</summary>
    public void DebugSpawn()
    {
        if (Busy) return;
        DebugUsed = true;
        var ev = new List<GameEvent>();
        Rules.TrySpawnPair(State, ev, false);
        Process(ev);
        Refresh();
        Save();
        Changed?.Invoke();
    }

    public void DebugArm(DebugTool tool, PowerupKind kind = PowerupKind.None, int times = 1)
    {
        ClearSelection();
        Inspect = -1;
        InspectSquare = -1;
        bool same = DebugArmed == tool && DebugKind == kind && DebugTimes == times;
        DebugArmed = same ? DebugTool.None : tool;
        DebugKind = same ? PowerupKind.None : kind;
        DebugTimes = same ? 1 : times;
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    public void DebugDisarm()
    {
        DebugArmed = DebugTool.None;
        DebugKind = PowerupKind.None;
        DebugTimes = 1;
        Changed?.Invoke();
    }

    public string DebugHint => DebugArmed switch
    {
        DebugTool.Buff => $"Debug: tap a piece to give it {(DebugKind == PowerupKind.Fireball || DebugKind == PowerupKind.Leap || DebugKind == PowerupKind.SmallBomb ? Names.TierName(DebugKind, DebugTimes) : Names.Name(DebugKind))}",
        DebugTool.Square => DebugKind == PowerupKind.None ? "Debug: tap an empty square for a mystery pair" : $"Debug: tap an empty square for a {Names.Name(DebugKind)} pair",
        DebugTool.Portal => "Debug: tap an empty square for a teleporter pair",
        DebugTool.Clear => "Debug: tap a powerup square, teleporter or boulder to remove it",
        DebugTool.Unbuff => "Debug: tap a piece to strip its powers",
        DebugTool.Stun => "Debug: tap a piece to stun it for 2 turns",
        _ => "",
    };

    void DebugTap(int sq)
    {
        var cell = State.Cells[sq];
        int mirror = Sq.Mirror(sq);
        bool free = cell.IsEmpty && !State.Boulders[sq] && !State.Mystery[sq] && !State.Portal[sq];
        bool mirrorFree = State.Cells[mirror].IsEmpty && !State.Boulders[mirror] && !State.Mystery[mirror] && !State.Portal[mirror];
        switch (DebugArmed)
        {
            case DebugTool.Buff:
            {
                if (cell.IsEmpty) return;
                if (Rules.KingIncompatible(cell, DebugKind)) { AddToast("King can't use " + Names.Name(DebugKind), "Kings can't carry poison or bombs. Pick another piece.", "gold", "crown"); break; }
                var ev = new List<GameEvent>();
                DebugUsed = true;
                for (int i = 0; i < DebugTimes; i++) Rules.ApplyPickup(State, cell.Side, sq, DebugKind, ev);
                Process(ev);
                SyncViews();
                break;
            }
            case DebugTool.Unbuff:
            {
                if (cell.IsEmpty) return;
                var p = cell;
                p.Buffs = Buff.None; p.Tier = 0; p.GhostTurns = 0; p.PoisonTurns = 0; p.ArmorTurns = 0;
                State.Cells[sq] = p;
                DebugUsed = true;
                SyncViews();
                break;
            }
            case DebugTool.Stun:
            {
                if (cell.IsEmpty) return;
                var p = cell;
                p.StunTurns = Rules.StunBoltTurns;
                State.Cells[sq] = p;
                DebugUsed = true;
                AddFx("stars", sq, css: "stun");
                SyncViews();
                break;
            }
            case DebugTool.Square:
                if (!free) return;
                DebugUsed = true;
                State.Mystery[sq] = true;
                State.Content[sq] = (byte)DebugKind;
                if (mirrorFree && mirror != sq)
                {
                    State.Mystery[mirror] = true;
                    State.Content[mirror] = (byte)DebugKind;
                }
                AddFx("ping", sq, css: "gold");
                break;
            case DebugTool.Portal:
                if (!free || !mirrorFree || mirror == sq) return;
                DebugUsed = true;
                State.Portal[sq] = true;
                State.Portal[mirror] = true;
                AddFx("ping", sq, css: "portal");
                AddFx("ping", mirror, css: "portal");
                break;
            case DebugTool.Clear:
                if (State.Mystery[sq]) { State.Mystery[sq] = false; State.Content[sq] = 0; }
                else if (State.Portal[sq]) { State.Portal[sq] = false; State.Portal[mirror] = false; }
                else if (State.Boulders[sq]) State.Boulders[sq] = false;
                else return;
                break;
        }
        Refresh();
        Save();
        Changed?.Invoke();
    }

    public async Task TapSquare(int sq)
    {
        if (DebugArmed != DebugTool.None && !Busy && !Reviewing)
        {
            DebugTap(sq);
            return;
        }
        if (!HumanTurn || PickupPending || Mode == InputMode.Promotion) return;

        if (ActiveCard != CardKind.None && !EffectPending)
        {
            await CardTap(sq);
            return;
        }

        if (EffectPending)
        {
            // Aim the Stun bolt or pick the piece to steal. Nothing else can be done until it is resolved.
            var pick = Legal.FirstOrDefault(a => (a.Kind == ActionKind.StunBeam || a.Kind == ActionKind.StealPick || a.Kind == ActionKind.Leap) && a.To == sq);
            if (pick.Kind == ActionKind.StunBeam || pick.Kind == ActionKind.StealPick || pick.Kind == ActionKind.Leap) await Human(pick);
            else Sfx?.Invoke("click");
            return;
        }

        if (Mode == InputMode.BombPreview)
        {
            Cancel(); // tapping the board backs out; the Confirm button is the only way to detonate
            return;
        }

        if (Mode == InputMode.Armed)
        {
            if (!Targets.Contains(sq)) { Cancel(); return; }
            if (ArmedKind == ActionKind.Swap)
            {
                if (Selected == sq) { Arm(ActionKind.Swap); return; } // tapping the first piece again backs out
                if (Selected < 0)
                {
                    Selected = sq;
                    Targets.Clear();
                    foreach (var a in Legal.Where(a => a.Kind == ActionKind.Swap))
                    {
                        if (a.From == sq) Targets.Add(a.To);
                        else if (a.To == sq) Targets.Add(a.From);
                    }
                    Sfx?.Invoke("click");
                    Changed?.Invoke();
                    return;
                }
                var pair = Legal.First(a => a.Kind == ActionKind.Swap && ((a.From == Selected && a.To == sq) || (a.From == sq && a.To == Selected)));
                await Human(pair);
                return;
            }
            var cands = Legal.Where(a => a.Kind == ArmedKind && a.To == sq).ToList();
            await Human(cands[0]);
            return;
        }

        if (Mode == InputMode.LeapAim)
        {
            if (!Targets.Contains(sq)) { Cancel(); return; }
            await Human(Legal.First(a => a.Kind == ActionKind.Leap && a.From == Selected && a.To == sq));
            return;
        }

        if (Mode == InputMode.FireAim)
        {
            if (!Targets.Contains(sq)) { Cancel(); return; }
            await Human(Legal.First(a => a.Kind == ActionKind.Fireball && a.From == Selected && a.To == sq));
            return;
        }

        // A king that cannot use a teleporter (its exit would leave it in check) gets an explanation instead of silence.
        if (Selected >= 0 && State.Portal[sq] && !Targets.Contains(sq) && State.Cells[Selected].Type == PieceType.King
            && MoveGen.HasPseudoMove(State, Selected, sq))
        {
            int exit = Sq.Mirror(sq);
            AddToast("Teleporter blocked", $"Your king would land on {Sq.Name(exit)} and be in check there.", "portal", "portal");
            Sfx?.Invoke("click");
            Changed?.Invoke();
            return;
        }

        if (Selected >= 0 && Targets.Contains(sq))
        {
            var moves = Legal.Where(a => a.Kind == ActionKind.Move && a.From == Selected && a.To == sq).ToList();
            if (moves.Count > 1)
            {
                Mode = InputMode.Promotion;
                PromoChoices = moves;
                Changed?.Invoke();
                return;
            }
            await Human(moves[0]);
            return;
        }

        // Tapping a powered-up piece (either side) opens its info card; tapping it again closes it.
        bool wasInspected = Inspect == sq;
        bool wasSquareInspected = InspectSquare == sq;
        Inspect = -1;
        InspectSquare = -1;
        if (Selected == sq && State.ForcedMover < 0)
        {
            ClearSelection();
        }
        else
        {
            if (State.ForcedMover < 0 || sq == State.ForcedMover) SelectPiece(sq);
            if (IsPowered(sq) && !wasInspected) Inspect = sq;
            // An empty powerup square: tap it to read what it is (and what it holds, when revealed).
            if ((State.Mystery[sq] || State.Portal[sq]) && State.Cells[sq].IsEmpty && !wasSquareInspected && Selected < 0) InspectSquare = sq;
        }
        Changed?.Invoke();
    }

    public async Task Choose(GameAction a) => await Human(a);

    /// <summary>Confirm step of the bomb preview: actually detonate the selected bomber.</summary>
    public async Task Detonate()
    {
        if (Mode != InputMode.BombPreview || Selected < 0) return;
        var action = Legal.FirstOrDefault(a => a.Kind == ActionKind.Detonate && a.From == Selected);
        if (action.Kind != ActionKind.Detonate) return;
        await Human(action);
    }

    public async Task KeepPickup()
    {
        if (!HumanTurn || !PickupPending) return;
        AcknowledgePickupTutorial();
        await Human(Legal.First(a => a.Kind == ActionKind.KeepPickup));
    }

    public async Task RerollPickup()
    {
        if (!HumanTurn || !PickupPending) return;
        AcknowledgePickupTutorial();
        var a = Legal.FirstOrDefault(x => x.Kind == ActionKind.RerollPickup);
        if (a.Kind == ActionKind.RerollPickup) await Human(a);
    }

    async Task Human(GameAction a)
    {
        // A piece about to land on a powerup square: remember what it holds, so the demo opens the first time it is found.
        var landed = PowerupKind.None;
        if (a.Kind == ActionKind.Move && a.To >= 0 && a.To < 64 && State.Mystery[a.To]) landed = (PowerupKind)State.Content[a.To];
        await Play(a);
        if (landed != PowerupKind.None && !GameOver) MaybeTutorial(landed);
        await AiLoop();
    }

    // ---------- turn flow ----------

    /// <summary>Global animation pace: 1.0 was the original Power Chess feel. Keep in step with the CSS durations in fighter.css (.piece, .piece-body.land-*, .piece.dying).</summary>
    const double Pace = 0.75;
    static int T(int ms) => (int)(ms * Pace);

    async Task AiLoop()
    {
        while (AiToMove)
        {
            Thinking = true;
            Busy = true;
            Changed?.Invoke();
            await Task.Delay(T(450)); // let the UI paint before the (synchronous) search
            var sw = System.Diagnostics.Stopwatch.StartNew();
            GameAction act = ai.Choose(State);
            Console.WriteLine($"[ai] {Ai.LevelName(opt.Ai)} chose {act} in {sw.ElapsedMilliseconds} ms");
            Thinking = false;
            await Play(act);
        }
        Busy = false;
        AutoSelectForced();
        Changed?.Invoke();
        await MaybeMars();
    }

    async Task Play(GameAction a)
    {
        ResetCardChoice();
        Busy = true;
        Inspect = -1;
        InspectSquare = -1;
        reviewIndex = -1;
        ClearHint();
        ClearSelection();
        Side mover = State.ToMove;
        history.Record(State, a);
        viewHistory.Add(SnapshotViews());
        var ev = new List<GameEvent>();
        lastFireMs = 0;
        Rules.Apply(State, a, ev);
        Moves++;
        tracker?.Observe(ev);
        Process(ev);
        if (rebuildViews)
        {
            // Everything moved at once (restart, flip, take back): draw the board afresh instead of animating each piece away.
            rebuildViews = false;
            Pieces.Clear();
            for (int sq = 0; sq < 64; sq++)
                if (!State.Cells[sq].IsEmpty) Pieces.Add(new PieceView { Id = nextId++, Square = sq });
        }
        SyncViews();
        Refresh();
        Changed?.Invoke();

        bool big = ev.Any(e => e.Kind == EventKind.Detonated || e.Kind == EventKind.FireballFired);
        bool hit = ev.Any(e => e.Kind == EventKind.Captured || e.Kind == EventKind.Destroyed || e.Kind == EventKind.Converted);
        int wait = big ? 1000 : hit ? 700 : 450;
        wait += 350 * Math.Max(0, ev.Count(e => e.Kind == EventKind.Detonated) - 1); // each extra bomb in a chain gets its own beat
        if (opt.Drama && ev.Any(e => e.Kind == EventKind.Captured)) wait += 330; // room for the strike beat
        if (opt.Drama && !GameOver && InCheck && !Reviewing)
        {
            bool struck = ev.Any(e => e.Kind == EventKind.Captured);
            Later(struck ? 700 : 150, () => AddFx("callout", State.ToMove == Side.White ? 3 : 59, css: "check", text: "CHECK!"));
        }
        bool finale = opt.Drama && GameOver && Result != GameResult.Draw && ResultReason != "Resigned" && ResultReason != "Missile strike";
        if (opt.Drama && GameOver && Result != GameResult.Draw && !finale)
        {
            Later(ev.Any(e => e.Kind == EventKind.Captured) ? 650 : 150, () => { Freeze(420, true); AddFx("callout", 28, css: "heavy mate", text: "K.O.!"); Shake(2); });
            wait += 500;
        }
        if (lastFireMs > 0) wait = Math.Max(wait, lastFireMs + 800); // let the fireball land and the pieces burn
        await Task.Delay(T(wait));
        if (finale) await RunFinale(a.Kind == ActionKind.Move ? a.To : -1);
        while (Draw is { Pick: true }) await Task.Delay(60); // the rival waits for the player's card pick

        if (Puzzle != null && PuzzleOutcome == PuzzleResult.None)
        {
            if (mover == Side.White) PuzzleMoves++;
            if (GameOver)
            {
                PuzzleOutcome = Result == GameResult.WhiteWins && PuzzleMoves <= Puzzle.MaxMoves ? PuzzleResult.Solved : PuzzleResult.Failed;
                if (PuzzleOutcome == PuzzleResult.Solved) PuzzleSolved?.Invoke(Puzzle, PuzzleMoves);
            }
            else if (PuzzleMoves >= Puzzle.MaxMoves)
            {
                PuzzleOutcome = PuzzleResult.Failed; // out of moves without a mate
            }
        }

        if (GameOver)
        {
            if (!endReported && tracker != null && Puzzle == null)
            {
                endReported = true;
                var summary = tracker.Finish(Result, ResultReason);
                summary.XpScale = Presets.XpScale(opt.Preset, opt.Freq, opt.Level);
                summary.HintsUsed = HintsUsed;
                GameEnded?.Invoke(summary);
            }
            Sfx?.Invoke(Result == GameResult.Draw ? "draw" : (VsAi && Winner() == RivalSide ? "lose" : "win"));
        }
        Busy = false;
        Save();
        Changed?.Invoke();
    }

    // ---------- the finishing blow: the room dims, the king falls over, the big K.O. slams in (skippable) ----------

    /// <summary>True while the finale plays: the board dims except the king and the attacker's squares.</summary>
    public bool FinaleOn { get; private set; }
    /// <summary>The mated king's square and the square the last piece landed on (both stay lit).</summary>
    public int FinaleKingSq { get; private set; } = -1;
    public int FinaleAtkSq { get; private set; } = -1;
    /// <summary>The king is falling (it ends lying on its side). -1 falls left, 1 falls right, 0 not yet.</summary>
    public int FinaleFall { get; private set; }
    public bool FinaleKo { get; private set; }
    bool finaleSkip;

    public void SkipFinale()
    {
        if (!FinaleOn) return;
        finaleSkip = true;
    }

    async Task FinaleWait(int ms)
    {
        for (int waited = 0; waited < ms && !finaleSkip; waited += 40) await Task.Delay(40);
    }

    /// <summary>Plays the finish after a checkmate. Returns false (and plays nothing) when there is no king to knock over.</summary>
    async Task<bool> RunFinale(int attackerSq)
    {
        int king = -1;
        var loser = Result == GameResult.WhiteWins ? Side.Black : Side.White;
        for (int i = 0; i < 64; i++) if (State.Cells[i].Type == PieceType.King && State.Cells[i].Side == loser) king = i;
        if (king < 0) return false;
        finaleSkip = false;
        FinaleKingSq = king; FinaleAtkSq = attackerSq; FinaleFall = 0; FinaleKo = false;
        FinaleOn = true;
        Changed?.Invoke();
        await FinaleWait(T(650));                         // the room dims and holds a beat
        if (!finaleSkip)
        {
            Freeze(260, true); Shake(3); Sfx?.Invoke("thud");
            await FinaleWait(T(260));
        }
        if (!finaleSkip)
        {
            int dx = Sq.File(king) - (attackerSq >= 0 ? Sq.File(attackerSq) : Sq.File(king));
            FinaleFall = dx != 0 ? Math.Sign(dx) : (Sq.File(king) < 4 ? 1 : -1);   // falls away from the attacker
            Changed?.Invoke();
            await FinaleWait(T(850));
        }
        if (!finaleSkip)
        {
            FinaleKo = true; Shake(3); Sfx?.Invoke("thud");
            Changed?.Invoke();
            await FinaleWait(T(1500));
        }
        FinaleOn = false; FinaleKo = false; FinaleFall = 0;
        FinaleKingSq = FinaleAtkSq = -1;
        Changed?.Invoke();
        return true;
    }

    // ---------- resign ----------

    /// <summary>Resigning is offered in a normal game (not a puzzle) while it is still running.</summary>
    public bool CanResign => !GameOver && Puzzle == null && !Busy && !Reviewing && PuzzleOutcome == PuzzleResult.None;

    /// <summary>Give up the game: it ends as a loss for you (or for whoever is on move in a two-player game).</summary>
    public void Resign()
    {
        if (!CanResign) return;
        var side = VsAi ? HumanSide : State.ToMove;
        Rules.Resign(State, side);
        ClearSelection();
        Inspect = -1;
        Refresh();
        if (!endReported && tracker != null)
        {
            endReported = true;
            var summary = tracker.Finish(Result, ResultReason);
            summary.XpScale = Presets.XpScale(opt.Preset, opt.Freq, opt.Level);
            summary.HintsUsed = HintsUsed;
            GameEnded?.Invoke(summary);
        }
        Sfx?.Invoke(VsAi ? "lose" : "win");
        Save();
        Changed?.Invoke();
    }

    public Side? Winner() => Result switch
    {
        GameResult.WhiteWins => Side.White,
        GameResult.BlackWins => Side.Black,
        _ => null,
    };

    void Refresh()
    {
        string reason;
        Result = Rules.GetResult(State, out reason);
        ResultReason = reason;
        Legal = Result == GameResult.Ongoing ? MoveGen.LegalActions(State) : new List<GameAction>();
        InCheck = Geometry.IsKingAttacked(State, State.ToMove);
    }

    public int MissileCountdown { get { return State.MissileLeft; } }
    public int MissileAnchorSquare { get { return State.MissileLeft > 0 ? State.MissileAnchor : -1; } }

    /// <summary>The squares a missile in flight will hit.</summary>
    public HashSet<int> MissileZone
    {
        get
        {
            var zone = new HashSet<int>();
            if (State.MissileLeft <= 0 || State.MissileAnchor < 0) return zone;
            for (int i = 0; i < 16; i++) zone.Add(Sq.Make(Sq.File(State.MissileAnchor) + i % 4, Sq.Rank(State.MissileAnchor) + i / 4));
            return zone;
        }
    }

    public string Status
    {
        get
        {
            string core = StatusCore;
            if (State.MissileLeft > 0 && !GameOver && !Reviewing && core != "")
                core += $" - MISSILE in {State.MissileLeft} {(State.MissileLeft == 1 ? "move" : "moves")}!";
            return core;
        }
    }

    string StatusCore
    {
        get
        {
            if (GameOver && !Reviewing) return ResultReason == "" ? "Game over" : ResultReason;
            if (Reviewing) return "Reviewing past moves. Nothing here changes the game.";
            if (Thinking) return "Opponent is thinking...";
            if (Busy) return "";
            if (Responding)
                return State.Decider == HumanSide && VsAi || !VsAi
                    ? $"{Names.CardName(State.RespCard)} was played! {(CanRespondNuhUh ? "Cancel it with Nuh-uh?" : "")}"
                    : "";
            if (ActiveCard != CardKind.None && HumanTurn)
            {
                var st = NextCardStep(out _, out _);
                if (st != CardStep.None) return CardPrompt(st);
            }
            if (EffectPending) return State.EffectKind == PowerupKind.StunBolt ? "Stun! Tap an enemy piece to zap it" : State.EffectKind == PowerupKind.Leap ? "Jump again! Tap where to land, or Stop" : "Steal! Tap a rival piece next to you to take it";
            if (PickupPending) return "You found a powerup!";
            if (Mode == InputMode.Armed)
                return ArmedKind switch
                {
                    ActionKind.Fireball => "Fireball: pick one of your pieces to fire from",
                    ActionKind.Steal => "Steal: pick the enemy piece to convert",
                    ActionKind.Stun => "Stun: pick any enemy piece to freeze for 3 turns",
                    ActionKind.PlaceBoulder => "Boulder: pick an empty square",
                    ActionKind.ClearBoulder => "Reroll: pick a boulder to smash",
                    _ => "",
                };
            if (Mode == InputMode.FireAim) return "Fireball: tap an enemy to choose the direction";
            if (Mode == InputMode.LeapAim) return "Leap: tap the square to land on (the marked rival is captured)";
            if (Mode == InputMode.BombPreview)
                return $"Bomb: destroys {BombOwnLosses} of your pieces (the bomber too) and {BombEnemyLosses} of theirs"
                    + (BombChain > 0 ? $", and sets off {BombChain} more {(BombChain == 1 ? "bomb" : "bombs")}" : "") + ". Confirm or cancel.";
            if (Mode == InputMode.Promotion) return "Promote to...";
            if (State.ForcedMover >= 0) return $"Combo! {State.MultiLeft} more {(State.MultiLeft == 1 ? "move" : "moves")} with that piece";
            string who = VsAi ? "Your move" : (State.ToMove == Side.White ? "White to move" : "Black to move");
            if (InCheck)
            {
                int ks = Geometry.FindKing(State, State.ToMove);
                bool bomb = ks >= 0 && Geometry.BombThreatens(State, ks, State.ToMove.Opponent());
                return who + (bomb ? " - check! A bomb blast (chain reactions count) could reach your king" : " - you are in check!");
            }
            return who;
        }
    }

    // ---------- review (read-only) and Rewind (take back) ----------

    List<PieceView> SnapshotViews() => Pieces.Where(p => !p.Dying).Select(Copy).ToList();

    static PieceView Copy(PieceView p) => new()
    {
        Id = p.Id, Type = p.Type, Side = p.Side, Square = p.Square, Buffs = p.Buffs,
        Stun = p.Stun, Armor = p.Armor, Ghost = p.Ghost, Tier = p.Tier, AnimSeq = 0,
    };

    (int from, int to) LastOf(int position)
    {
        if (position <= 0 || position > history.Count) return (-1, -1);
        var a = history[position - 1].Action;
        switch (a.Kind)
        {
            case ActionKind.Move:
            case ActionKind.Fireball:
            case ActionKind.Leap:
            case ActionKind.StunBeam:
            case ActionKind.StealPick:
            case ActionKind.Steal:
            case ActionKind.Swap:
                return (a.From, a.To);
            case ActionKind.Detonate: return (a.From, a.From);
            case ActionKind.Stun:
            case ActionKind.PlaceBoulder:
            case ActionKind.ClearBoulder: return (a.To, a.To);
            default: return (-1, -1);
        }
    }

    /// <summary>Move the review position by <paramref name="delta"/>. Stepping forward to the newest position returns to the live game.</summary>
    public void ReviewStep(int delta)
    {
        if (Reviewing && ReviewPosition + delta >= history.Count) ExitReview();
        else ReviewTo(ReviewPosition + delta);
    }

    public void ReviewTo(int position)
    {
        if (!CanReview && !Reviewing) return;
        ClearSelection();
        Inspect = -1;
        position = Math.Clamp(position, 0, history.Count);
        ClearHint();
        reviewIndex = position;
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    public void ExitReview()
    {
        if (!Reviewing) return;
        reviewIndex = -1;
        ClearSelection();
        Inspect = -1;
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    /// <summary>Short description of the action that led into the position being shown.</summary>
    public string ReviewLabel
    {
        get
        {
            int p = ReviewPosition;
            if (p <= 0) return "Start of game";
            return Describe(history[p - 1]);
        }
    }

    string Describe(GameHistory.Entry e)
    {
        string who = VsAi ? (e.Mover == HumanSide ? "You" : "Rival") : (e.Mover == Side.White ? "White" : "Black");
        var a = e.Action;
        string text = a.Kind switch
        {
            ActionKind.Move => $"{e.Before.Cells[a.From].Type} {Sq.Name(a.From)} to {Sq.Name(a.To)}"
                + (!e.Before.Cells[a.To].IsEmpty ? " (capture)" : ""),
            ActionKind.Fireball => $"Fireball from {Sq.Name(a.From)}",
            ActionKind.Leap => $"Leap {Sq.Name(a.From)} to {Sq.Name(a.To)}",
            ActionKind.LeapStop => "Stopped jumping",
            ActionKind.Stun => $"Stun on {Sq.Name(a.To)}",
            ActionKind.Steal => $"Steal on {Sq.Name(a.To)}",
            ActionKind.PlaceBoulder => $"Boulder on {Sq.Name(a.To)}",
            ActionKind.ClearBoulder => $"Smashed boulder on {Sq.Name(a.To)}",
            ActionKind.Detonate => $"Bomb on {Sq.Name(a.From)}",
            ActionKind.KeepPickup => "Kept the powerup",
            ActionKind.RerollPickup => "Rerolled the powerup",
            _ => a.Kind.ToString(),
        };
        return $"{who}: {text}";
    }

    /// <summary>Take back the player's last turn (and the reply to it), spending one Rewind.</summary>
    public void UseRewind()
    {
        if (!CanRewind) return;
        int index;
        var restored = history.Rewind(State, Me, out index);
        var views = viewHistory[index];
        viewHistory.RemoveRange(index, viewHistory.Count - index);

        State = restored;
        Pieces.Clear();
        foreach (var v in views) Pieces.Add(Copy(v));
        Effects.Clear();
        var (lf, lt) = LastOf(index);
        LastFrom = lf;
        LastTo = lt;
        ClearSelection();
        Inspect = -1;
        Refresh();
        AddToast("REWOUND", "Your last turn was taken back", "gold", "rewind");
        Sfx?.Invoke("rewind");
        Save();
        Changed?.Invoke();
    }

    // ---------- events -> views and effects ----------

    PieceView? At(int sq) => Pieces.FirstOrDefault(p => !p.Dying && p.Square == sq);

    void Process(List<GameEvent> events)
    {
        try { ProcessEvents(events); }
        finally { toastSide = null; }
    }

    void ProcessEvents(List<GameEvent> events)
    {
        int movedId = -1;
        int fireFrom = -1;
        var fireDelay = new Dictionary<int, int>();
        var strikeDelay = new Dictionary<int, int>(); // piece square -> ms until the sword or dragon reaches it
        int smiteCount = 0;
        for (int ei = 0; ei < events.Count; ei++)
        {
            var e = events[ei];
            // Toasts for this event are about the side that did it; a few events carry a side that is not the actor.
            AdvanceCoach(e);
            toastSide = e.Kind == EventKind.PoisonTriggered || e.Kind == EventKind.MissileStruck || e.Kind == EventKind.TeleportBlocked || e.Kind == EventKind.DoubleMoveForfeited ? (Side?)null : e.Side;
            switch (e.Kind)
            {
                case EventKind.Moved:
                {
                    var v = At(e.From);
                    if (v != null)
                    {
                        v.Square = e.To;
                        v.AnimSeq++;
                        movedId = v.Id;
                    }
                    LastFrom = e.From;
                    LastTo = e.To;
                    if (opt.Drama && ei + 1 < events.Count && events[ei + 1].Kind == EventKind.Captured)
                        AddFx("streak", e.From, e.To);
                    Sfx?.Invoke("move");
                    break;
                }
                case EventKind.Castled:
                {
                    var rook = At(e.From);
                    if (rook != null) { rook.Square = e.To; rook.AnimSeq++; }
                    break;
                }
                case EventKind.Captured:
                {
                    var victim = Pieces.FirstOrDefault(p => !p.Dying && p.Square == e.To && p.Id != movedId);
                    if (opt.Drama && victim != null && movedId >= 0)
                    {
                        Strike(victim, e);
                        break;
                    }
                    if (victim != null) Kill(victim);
                    AddFx("burst", e.To, css: Heat(e.Side));
                    Shake();
                    Sfx?.Invoke("capture");
                    break;
                }
                case EventKind.Promoted:
                {
                    var v = Pieces.FirstOrDefault(p => p.Id == movedId);
                    if (v != null) v.Type = e.Piece;
                    AddFx("ring", e.To, radius: 1, css: "gold");
                    AddToast("Promotion!", Names.Name(PowerupKind.TransformQueen).Replace("Become ", ""), "gold", "spark");
                    Sfx?.Invoke("pickup");
                    break;
                }
                case EventKind.PoisonTriggered:
                {
                    var v = Pieces.FirstOrDefault(p => p.Id == movedId);
                    if (v != null) Kill(v);
                    AddFx("burst", e.From, css: "poison");
                    AddToast("POISON!", "The capturer is destroyed too", "poison", "skull");
                    Shake();
                    Sfx?.Invoke("poison");
                    break;
                }
                case EventKind.Destroyed:
                {
                    var v = At(e.From);
                    if (v != null && strikeDelay.TryGetValue(e.From, out var sdelay))
                    {
                        // Stays on the board until the sword lands or the dragon passes.
                        v.PendingDeath = true;
                        int at = e.From;
                        Later(T(sdelay), () => { v.PendingDeath = false; Kill(v); AddFx("burst", at, css: "boom"); Shake(); });
                    }
                    else if (fireFrom >= 0 && v != null && fireDelay.TryGetValue(e.From, out var wait))
                    {
                        // Stays on the board until the ball reaches it, then ignites.
                        v.PendingDeath = true;
                        Later(wait, () => { v.PendingDeath = false; Ignite(v); });
                    }
                    else
                    {
                        if (v != null) Kill(v);
                        AddFx("burst", e.From, css: "boom");
                    }
                    break;
                }
                case EventKind.FireballFired:
                {
                    // Look ahead at what this shot destroys so the ball can fly to the farthest victim and each
                    // piece ignites at the moment the ball reaches it (a queen's ball rolls the whole line).
                    var victims = new List<int>();
                    for (int j = ei + 1; j < events.Count && events[j].Kind == EventKind.Destroyed; j++) victims.Add(events[j].From);
                    int far = 1;
                    int target = e.From;
                    foreach (int v in victims)
                    {
                        int d = Distance(e.From, v);
                        if (d >= far) { far = d; target = v; }
                    }
                    int dur = Math.Max(350, far * 110);
                    lastFireMs = dur;
                    AddFx("fireball", e.From, target, css: "fire", durMs: dur);
                    foreach (int v in victims) fireDelay[v] = (int)(dur * (double)Distance(e.From, v) / far);
                    fireFrom = e.From;
                    AddToast("FIREBALL!", "", "fire", "fire");
                    Sfx?.Invoke("fire");
                    break;
                }
                case EventKind.Detonated:
                {
                    int r = e.Powerup == PowerupKind.BigBomb ? 2 : 1;
                    AddFx("ring", e.From, radius: r, css: "boom");
                    AddFx("flash", e.From, radius: r, css: "boom");
                    AddToast(r == 2 ? "BIG BOMB!" : "BOOM!", "", "boom", r == 2 ? "bigbomb" : "bomb");
                    Shake();
                    Sfx?.Invoke("bomb");
                    break;
                }
                case EventKind.Converted:
                {
                    var v = At(e.To);
                    if (v != null) v.Side = e.Side;
                    AddFx("ring", e.To, radius: 1, css: "steal");
                    AddFx("beam", e.From, e.To, css: "steal");
                    AddToast("STOLEN!", "", "steal", "steal");
                    Sfx?.Invoke("steal");
                    break;
                }
                case EventKind.StunStruck:
                    AddFx("stars", e.To, css: "stun");
                    AddToast("STUNNED!", e.Powerup == PowerupKind.Stun ? "Frozen for 3 turns" : $"Frozen for {Rules.StunBoltTurns} turns", "stun", "star");
                    Shake();
                    Sfx?.Invoke("stun");
                    break;
                case EventKind.Transformed:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    AddToast(Names.Name(e.Powerup), Names.Desc(e.Powerup), "gold", "spark");
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.BuffGained:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    AddToast(Names.TierName(e.Powerup, 1), Names.Desc(e.Powerup, 1), "gold", Names.Icon(e.Powerup));
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.PowerUpgraded:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    AddFx("burst", e.From, css: "fire");
                    AddToast(Names.TierName(e.Powerup, 2) + "!", Names.Desc(e.Powerup, 2), "gold", Names.Icon(e.Powerup));
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.Leaped:
                    AddFx("ring", e.To, radius: 1, css: "gold");
                    AddToast("LEAP!", "", "gold", "checker");
                    Sfx?.Invoke("steal");
                    break;
                case EventKind.CardDrawn:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    StartDrawShow(e.Card, e.Side);
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.CardPlayed:
                    if (e.Card == CardKind.SummonCaissa || e.Card == CardKind.SummonMars || e.Card == CardKind.SummonShiva)
                        ShowSummon(e.Card);
                    // Cards with their own show (portrait splash, dragon) do not also pop a toast over the board.
                    if (e.Card != CardKind.DragonStrike && e.Card != CardKind.SummonCaissa && e.Card != CardKind.SummonMars && e.Card != CardKind.SummonShiva)
                        AddToast("PLAYED: " + Names.CardName(e.Card), Names.CardDesc(e.Card), "gold", Names.CardIcon(e.Card));
                    else if (e.Card != CardKind.DragonStrike)
                        SummonBy = e.Side;
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.CardCancelled:
                    AddFx("callout", 28, css: "heavy mate", text: "NUH-UH!");
                    AddToast("NUH-UH!", Names.CardName(e.Card) + " was cancelled", "stun", Names.CardIcon(CardKind.NuhUh));
                    Shake();
                    Sfx?.Invoke("thud");
                    break;
                case EventKind.CardKept:
                    AddToast("Card kept", Names.CardName(e.Card) + " had no legal use, so it went in the bag", "gold", Names.CardIcon(e.Card));
                    break;
                case EventKind.PieceRestored:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    AddFx("burst", e.From, css: "fire");
                    AddToast("CAÏSSA!", "A piece returns", "gold", "spark");
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.PowersCleared:
                    Shake(2);
                    AddToast("NO MORE POWERS", "Every power and powerup square is gone", "stun", "ban");
                    Sfx?.Invoke("bomb");
                    break;
                case EventKind.TurnRewound:
                    rebuildViews = true;
                    AddToast("TAKE BACK", "A round of moves is undone", "stun", "rewind");
                    Shake();
                    Sfx?.Invoke("thud");
                    break;
                case EventKind.BoardRestarted:
                    rebuildViews = true;
                    AddToast("HIT RESTART", "Everything goes home", "gold", "history");
                    Shake(2);
                    Sfx?.Invoke("bomb");
                    break;
                case EventKind.BoardFlipped:
                    rebuildViews = true;
                    Flipping = true;
                    Later(1000, () => Flipping = false);
                    AddToast("THE WORLD TURNS", "You command the other army", "stun", "portal");
                    Shake(2);
                    Sfx?.Invoke("stun");
                    break;
                case EventKind.DragonFire:
                {
                    // A dragon (green for White, flying left to right; red for Black, right to left) sweeps along the row; each piece falls as it passes.
                    int rank = Sq.Rank(e.From), dms = 1500;
                    AddFx("dragon", e.From, css: e.Side == Side.White ? "w" : "b", durMs: dms);
                    for (int j = ei + 1; j < events.Count && events[j].Kind == EventKind.Destroyed; j++)
                        strikeDelay[events[j].From] = (int)(dms * 0.8 * Math.Clamp((((e.Side == Side.White ? Sq.File(events[j].From) : 7 - Sq.File(events[j].From)) + 0.5) * 12.5 - 6) / 134, 0, 1));
                    lastFireMs = Math.Max(lastFireMs, dms);
                    {
                        // The toast waits until the dragon has passed, so it never hides the row burning.
                        int burned = 0;
                        for (int j = ei + 1; j < events.Count && events[j].Kind == EventKind.Destroyed; j++) burned++;
                        string who = VsAi ? (e.Side == HumanSide ? "Your" : "Rival's") : (e.Side == Side.White ? "White's" : "Black's");
                        Side dragonSide = e.Side;
                        Later(T(dms + 300), () => AddToast("DRAGON STRIKE!", $"{who} dragon burned {burned} {(burned == 1 ? "piece" : "pieces")}", "fire", "hadouken", dragonSide, false));
                    }
                    Shake(2);
                    Sfx?.Invoke("fire");
                    break;
                }
                case EventKind.ShivaStrike:
                {
                    // One sword per victim, dropped in turn after the portrait has had its moment.
                    int drop = 1700 + smiteCount * 650;
                    smiteCount++;
                    int at = e.From;
                    strikeDelay[at] = drop + 380;
                    Later(T(drop), () => AddFx("sword", at, durMs: 700));
                    Later(T(drop + 380), () => { AddFx("ring", at, radius: 1, css: "gold"); Sfx?.Invoke("slash"); });
                    lastFireMs = Math.Max(lastFireMs, T(drop + 1000));
                    break;
                }
                case EventKind.MissileLaunched:
                    AddToast("MISSILE INCOMING", "It lands in 10 moves. Clear the zone!", "fire", "bigbomb");
                    Sfx?.Invoke("fire");
                    break;
                case EventKind.MissileStruck:
                {
                    int ax = Sq.File(e.From), ay = Sq.Rank(e.From);
                    AddFx("ring", Sq.Make(ax + 1, ay + 1), radius: 2, css: "boom");
                    AddFx("flash", Sq.Make(ax + 1, ay + 1), radius: 2, css: "boom");
                    AddToast("MISSILE STRIKE!", "The quadrant is gone", "boom", "bigbomb");
                    Shake(2);
                    Sfx?.Invoke("bomb");
                    break;
                }
                case EventKind.ZapFired:
                    AddFx("bolt", e.From, e.To);
                    AddFx("callout", e.To, css: "heavy zap", text: "ZAP!");
                    AddToast("ZAP!", "", "stun", "bolt");
                    Sfx?.Invoke("stun");
                    break;
                case EventKind.EffectFizzled:
                    AddFx("ping", e.From, css: "steal");
                    AddToast("No target", "Nothing legal to hit, so the power was wasted.", "gold", Names.Icon(e.Powerup));
                    break;
                case EventKind.PulledIn:
                    AddFx("ping", e.To, css: "steal");
                    Shake();
                    break;
                case EventKind.PoisonFaded:
                    AddFx("ping", e.From, css: "steal");
                    break;
                case EventKind.PowerMaxed:
                    AddFx("ping", e.From, css: "steal");
                    AddToast("Already maxed", "This piece already has the top tier. The square is used up.", "gold", Names.Icon(e.Powerup));
                    Sfx?.Invoke("thud");
                    break;
                case EventKind.PerkGained:
                    AddFx("ring", e.From, radius: 1, css: "gold");
                    AddToast("+1 " + Names.Name(e.Powerup), Names.Desc(e.Powerup), "gold", Names.Icon(e.Powerup));
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.PickupFound:
                    AddFx("ring", e.To, radius: 1, css: "gold");
                    Sfx?.Invoke("pickup");
                    break;
                case EventKind.PickupRerolled:
                    AddToast("Rerolled!", "", "gold", "dice");
                    break;
                case EventKind.BoulderPlaced:
                    AddFx("burst", e.To, css: "rock");
                    Sfx?.Invoke("thud");
                    break;
                case EventKind.BoulderCleared:
                    AddFx("burst", e.To, css: "rock");
                    Sfx?.Invoke("capture");
                    break;
                case EventKind.SquareSpawned:
                    AddFx("ping", e.From, css: "gold");
                    AddFx("ping", e.To, css: "gold");
                    Sfx?.Invoke("spawn");
                    break;
                case EventKind.PortalSpawned:
                    AddFx("ping", e.From, css: "portal");
                    AddFx("ping", e.To, css: "portal");
                    AddToast("Teleporters!", "Land on one to jump to its twin", "portal", "portal");
                    Sfx?.Invoke("spawn");
                    break;
                case EventKind.Teleported:
                {
                    var v = Pieces.FirstOrDefault(p => p.Id == movedId);
                    if (v != null)
                    {
                        v.Square = e.To;
                        v.Warping = true;
                        v.AnimSeq++;
                        Later(700, () => v.Warping = false);
                    }
                    AddFx("ring", e.From, radius: 1, css: "portal");
                    AddFx("ring", e.To, radius: 1, css: "portal");
                    AddToast("WARP!", "", "portal", "portal");
                    Sfx?.Invoke("warp");
                    break;
                }
                case EventKind.Swapped:
                {
                    var a = At(e.From);
                    var b = At(e.To);
                    if (a != null) { a.Square = e.To; a.AnimSeq++; }
                    if (b != null) { b.Square = e.From; b.AnimSeq++; }
                    AddFx("ring", e.From, radius: 1, css: "steal");
                    AddFx("ring", e.To, radius: 1, css: "steal");
                    AddToast("SWAP!", "", "steal", "swap");
                    Sfx?.Invoke("steal");
                    break;
                }
                case EventKind.PickupRejected:
                    AddFx("burst", e.To, css: "rock");
                    if (e.Piece != PieceType.King)
                        AddToast("Too risky", "Taking this bomb would put your own king in a blast chain. The square is used up.", "gold", Names.Icon(e.Powerup));
                    else
                        AddToast("King can't use " + Names.Name(e.Powerup), PowerupInfo.IsTransform(e.Powerup) ? "A king can never change type. The square is used up." : PowerupInfo.IsBanked(e.Powerup) && e.Powerup != PowerupKind.Poison ? "A king can't carry a bomb. The square is used up." : "A king can't be poisoned. The square is used up.", "gold", "crown");
                    Sfx?.Invoke("thud");
                    break;
                case EventKind.TeleportBlocked:
                    AddFx("ping", e.From, css: "portal");
                    AddToast("Exit blocked", "The twin teleporter was occupied, so nothing happened. Both are used up.", "portal", "portal");
                    break;
                case EventKind.GhostFaded:
                    AddFx("ping", e.From, css: "steal");
                    break;
                case EventKind.DoubleMoveForfeited:
                    AddToast("Check!", "Second move forfeited", "gold", "x2");
                    break;
                case EventKind.ArmorExpired:
                    AddFx("ping", e.From, css: "steal");
                    break;
            }
        }
    }

    static string Heat(Side s) => s == Side.White ? "boom" : "steal";

    /// <summary>Copies buffs/types from the engine so the view can never drift from the rules.</summary>
    void SyncViews()
    {
        foreach (var v in Pieces.Where(p => !p.Dying && !p.PendingDeath).ToList())
        {
            var c = State.Cells[v.Square];
            if (c.IsEmpty) { Kill(v); continue; }
            v.Type = c.Type;
            v.Side = c.Side;
            v.Buffs = c.Buffs;
            v.Stun = c.StunTurns;
            v.Armor = c.ArmorTurns;
            v.Ghost = c.GhostTurns;
            v.Tier = c.Tier;
        }
        for (int sq = 0; sq < 64; sq++)
        {
            if (!State.Cells[sq].IsEmpty && At(sq) == null)
            {
                var c = State.Cells[sq];
                Pieces.Add(new PieceView { Id = nextId++, Square = sq, Type = c.Type, Side = c.Side, Buffs = c.Buffs, Stun = c.StunTurns, Armor = c.ArmorTurns, Ghost = c.GhostTurns, Tier = c.Tier });
            }
        }
    }

    void Kill(PieceView v)
    {
        v.Dying = true;
        Later(T(650), () => Pieces.Remove(v));
    }

    /// <summary>A fireball reaches this piece: it bursts into flame and burns away.</summary>
    void Ignite(PieceView v)
    {
        v.Burning = true;
        Kill(v);
        AddFx("burst", v.Square, css: "fire");
        AddFx("flash", v.Square, radius: 0, css: "boom");
        Shake();
        Sfx?.Invoke("capture");
    }

    static int Distance(int a, int b) => Math.Max(Math.Abs(Sq.File(a) - Sq.File(b)), Math.Abs(Sq.Rank(a) - Sq.Rank(b)));

    void AddFx(string kind, int sq, int to = -1, int radius = 0, string css = "", int durMs = 0, string text = "")
    {
        var fx = new FxItem { Id = nextId++, Kind = kind, Sq = sq, To = to, Radius = radius, Css = css, DurMs = durMs, Text = text };
        Effects.Add(fx);
        Later(Math.Max(1100, durMs + 500), () => Effects.Remove(fx));
    }

    // ---------- card draw reveal ----------

    public DrawShow? Draw { get; private set; }
    int drawSerial;

    void LogOnly(Side who, string text)
    {
        Log.Add(new LogEntry { Who = who, WhoLabel = WhoLabelFor(who), Text = text, Ply = State.Ply });
        if (Log.Count > 60) Log.RemoveAt(0);
    }

    /// <summary>
    /// A card was drawn: the player gets the pick-a-card moment (it waits for a tap, or flips by itself after three seconds), the rival's
    /// card flips face-up for a second. Calm effects skip the picking and just show the card.
    /// </summary>
    void StartDrawShow(CardKind card, Side side)
    {
        LogOnly(side, "Drew " + Names.CardName(card));
        if (Draw != null && Draw.Pick) { AddToast("DREW: " + Names.CardName(card), Names.CardDesc(card), "gold", Names.CardIcon(card), side, false); return; }
        bool human = !VsAi || side == HumanSide;
        int serial = ++drawSerial;
        var show = new DrawShow { Card = card, Side = side, WhoLabel = WhoLabelFor(side), Serial = serial, Pick = human && opt.Drama, Phase = 0 };
        if (!show.Pick) show.Phase = 1;
        Draw = show;
        if (show.Pick)
            Later(3000, () => { if (Draw?.Serial == serial && Draw.Phase == 0) FlipDraw(1); });
        else
            Later(human ? 1900 : 1300, () => { if (Draw?.Serial == serial) { Draw = null; } });
    }

    /// <summary>The player taps one of the face-down cards.</summary>
    public void PickDrawCard(int index)
    {
        if (Draw == null || Draw.Phase != 0) return;
        FlipDraw(index);
    }

    void FlipDraw(int index)
    {
        if (Draw == null) return;
        int serial = Draw.Serial;
        Draw.Chosen = index;
        Draw.Phase = 1;
        Sfx?.Invoke("pickup");
        Changed?.Invoke();
        Later(2300, () => { if (Draw?.Serial == serial) Draw = null; });
    }

    /// <summary>A tap anywhere on the revealed card closes it.</summary>
    public void DismissDraw()
    {
        if (Draw == null || Draw.Phase == 0) return;
        Draw = null;
        Changed?.Invoke();
    }

    /// <summary>The side the event being processed belongs to; toasts made during <see cref="Process"/> are tagged with it.</summary>
    Side? toastSide;
    readonly Queue<Toast> toastQueue = new();
    const int MaxToasts = 4;

    public List<LogEntry> Log { get; } = new();

    /// <summary>"YOU" / "RIVAL" against the computer, "WHITE" / "BLACK" between two players.</summary>
    public string WhoLabelFor(Side side) => VsAi ? (side == HumanSide ? "YOU" : "RIVAL") : (side == Side.White ? "WHITE" : "BLACK");

    void AddToast(string title, string text, string css, string icon, Side? who = null, bool useCurrent = true)
    {
        if (who == null && useCurrent) who = toastSide;
        // Short banners (FIREBALL!, BOOM!) clear quickly; anything with words, and anything about a player, stays long enough to read.
        // Long help text is cut to its first sentence on the toast (the full text is on the card and in the demos).
        int cut = text.IndexOf(". ", StringComparison.Ordinal);
        if (text.Length > 70 && cut > 0) text = text.Substring(0, cut + 1);
        int ms = text == "" ? 1900 : who != null ? 3600 : 2600;
        var t = new Toast { Id = nextId++, Title = title, Text = text, Css = css, Icon = icon, Who = who, WhoLabel = who is Side w ? WhoLabelFor(w) : "", Ms = ms };
        if (who != null)
        {
            Log.Add(new LogEntry { Who = who, WhoLabel = t.WhoLabel, Text = title + (text != "" ? " - " + text : ""), Ply = State.Ply });
            if (Log.Count > 60) Log.RemoveAt(0);
        }
        if (Toasts.Count < MaxToasts) ShowToast(t); else toastQueue.Enqueue(t); // a full screen waits its turn instead of dropping the oldest
    }

    void ShowToast(Toast t)
    {
        Toasts.Add(t);
        Later(t.Ms, () => RemoveToast(t));
    }

    void RemoveToast(Toast t)
    {
        Toasts.Remove(t);
        while (Toasts.Count < MaxToasts && toastQueue.Count > 0) ShowToast(toastQueue.Dequeue());
    }

    /// <summary>Tap a toast to clear it.</summary>
    public void DismissToast(int id)
    {
        var t = Toasts.FirstOrDefault(x => x.Id == id);
        if (t != null) { RemoveToast(t); Changed?.Invoke(); }
    }

    void Shake(int level = 1)
    {
        ShakeLevel = level;
        Shaking = true;
        Later(level > 1 ? 520 : 380, () => Shaking = false);
    }

    void Freeze(int ms, bool punch)
    {
        Frozen = true;
        Punch = punch;
        Later(ms, () => { Frozen = false; });
        if (punch) Later(ms + 350, () => Punch = false);
    }

    /// <summary>A capture with drama: the attacker lands, the world holds for a beat under a slash, then the victim goes.</summary>
    void Strike(PieceView victim, GameEvent e)
    {
        int travel = T(230);
        int hit = victim.Type switch
        {
            PieceType.Pawn => T(60),
            PieceType.Knight or PieceType.Bishop => T(120),
            PieceType.Rook => T(150),
            PieceType.Queen => T(240),
            _ => T(100),
        };
        string word = victim.Type switch
        {
            PieceType.Knight or PieceType.Bishop => "STRIKE!",
            PieceType.Rook => "CRUSHED!",
            PieceType.Queen => "SLAIN!",
            _ => "",
        };
        bool heavy = victim.Type is PieceType.Queen or PieceType.Rook;
        victim.PendingDeath = true;
        int sq = e.To;
        Later(travel, () =>
        {
            Freeze(hit, victim.Type == PieceType.Queen || GameOver);
            AddFx("slash", sq, css: sq % 2 == 0 ? "a" : "b");
            if (word != "" && !GameOver) AddFx("callout", sq, css: heavy ? "heavy" : "", text: word);
            Sfx?.Invoke("slash");
        });
        Later(travel + hit, () =>
        {
            victim.PendingDeath = false;
            Kill(victim);
            AddFx("burst", sq, css: Heat(e.Side));
            Shake(heavy ? 2 : 1);
            Sfx?.Invoke("capture");
        });
    }

    /// <summary>The summon portrait splash: the figure slides in over the board for a moment.</summary>
    public CardKind SummonSplash { get; private set; }
    public Side SummonBy { get; private set; }
    public int SummonSerial { get; private set; }

    void ShowSummon(CardKind kind)
    {
        SummonSplash = kind;
        int serial = ++SummonSerial;
        Later((int)(2000 * Pace / 0.75), () => { if (SummonSerial == serial) { SummonSplash = CardKind.None; } });
    }

    async void Later(int ms, Action act)
    {
        await Task.Delay(ms);
        act();
        Changed?.Invoke();
    }
}
