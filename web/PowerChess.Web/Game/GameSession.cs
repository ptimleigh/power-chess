using PowerChess.Core;

namespace PowerChess.Web.Game;

public enum InputMode { Normal, Armed, FireAim, BombPreview, Promotion }

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
    public bool Dying;
    public int AnimSeq;
}

public sealed class FxItem
{
    public int Id;
    public string Kind = "";
    public int Sq;
    public int To = -1;
    public int Radius;
    public string Css = "";
}

public sealed class Toast
{
    public int Id;
    public string Title = "";
    public string Text = "";
    public string Css = "";
    public string Icon = "";
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
    /// <summary>Square of the powered-up piece whose info card is open, or -1.</summary>
    public int Inspect { get; private set; } = -1;
    public List<GameAction> PromoChoices { get; private set; } = new();
    public int LastFrom { get; private set; } = -1;
    public int LastTo { get; private set; } = -1;
    public bool InCheck { get; private set; }
    public int Moves { get; private set; }

    public bool Reviewing => reviewIndex >= 0 && reviewIndex < history.Count;
    public int HistoryCount => history.Count;
    /// <summary>Position being shown while reviewing (0 = start). Equals <see cref="HistoryCount"/> when live.</summary>
    public int ReviewPosition => Reviewing ? reviewIndex : history.Count;
    /// <summary>What the board shows: the live game, or a past position while reviewing.</summary>
    public GameState View => Reviewing ? history[reviewIndex].Before : State;
    public List<PieceView> ViewPieces => Reviewing ? viewHistory[reviewIndex] : Pieces;
    public int ViewLastFrom => Reviewing ? LastOf(reviewIndex).from : LastFrom;
    public int ViewLastTo => Reviewing ? LastOf(reviewIndex).to : LastTo;
    public bool CanReview => history.Count > 0 && !Busy && !PickupPending;
    public bool CanRewind => HumanTurn && !Reviewing && history.CanRewind(State, Me);
    /// <summary>The side the player controls: White vs the AI, otherwise whoever is to move.</summary>
    public Side Me => VsAi ? Side.White : State.ToMove;

    public Action? Changed;
    public Action<string>? Sfx;
    /// <summary>Called with a save string after every completed action, or null when there is nothing to resume (game over).</summary>
    public Action<string?>? Persist;

    public GameSession(GameOptions options, TutorialState? tutorials = null)
    {
        opt = options;
        Tut = tutorials ?? new TutorialState();
        ai = new Ai(opt.Ai switch { 1 => 1, 3 => 3, _ => 2 });
        NewGame();
    }

    // ---------- powerup tutorials ----------

    public const string TrainingFen = "r3k3/pppp4/8/8/8/8/4PPPP/4K2R w - - 0 1";

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
    bool AiToMove => VsAi && !GameOver && State.ToMove == Side.Black;
    public bool HumanTurn => !GameOver && !Busy && !Reviewing && (!VsAi || State.ToMove == Side.White);
    public bool PickupPending => State.PendingSquare >= 0;
    public Side Turn => State.ToMove;

    // ---------- setup ----------

    GameConfig BuildConfig()
    {
        var cfg = new GameConfig { StartRerolls = opt.Rerolls };
        if (opt.Pairs >= 0) cfg.InitialPairs = opt.Pairs;
        if (opt.Spawn >= 0) cfg.SpawnInterval = opt.Spawn;
        if (opt.Only.Count > 0) cfg.Pool = opt.Only.Select(k => new WeightedKind(k, 1)).ToList();
        cfg.RewindAllowed[(int)Side.Black] = !VsAi; // the AI cannot use Rewind, so it never draws it
        return cfg;
    }

    public void NewGame()
    {
        var cfg = BuildConfig();

        string? fen = opt.Training ? TrainingFen : opt.Fen;
        if (opt.Training)
        {
            // A small first game: a rook and pawns each, and two mystery squares in the middle holding friendly powerups.
            cfg.InitialPairs = 0;
            cfg.Pool = new[] { PowerupKind.Fireball, PowerupKind.SpikedArmor, PowerupKind.Poison, PowerupKind.Steal, PowerupKind.SmallBomb }
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
            for (int i = 0; i < cfg.InitialPairs; i++) Rules.TrySpawnPair(State, null);
        }

        foreach (var (side, kind, n) in opt.Perks) State.AddPerk(side, kind, n);
        foreach (var (sq, kind) in opt.Buffs)
        {
            int i = Sq.Parse(sq);
            if (!State.Cells[i].IsEmpty) Rules.ApplyPickup(State, State.Cells[i].Side, i, kind, null);
        }
        foreach (var sq in opt.Mystery) State.Mystery[Sq.Parse(sq)] = true;
        if (opt.Training)
        {
            State.Mystery[Sq.Parse("d4")] = true;
            State.Mystery[Sq.Parse("e5")] = true;
            ai = new Ai(1);
        }

        Pieces.Clear();
        Effects.Clear();
        Toasts.Clear();
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
        return string.Join("|", GameStateCodec.EncodeGame(State, history), nextId, opt.Ai, Convert.ToBase64String(ids.ToArray()));
    }

    /// <summary>Restore a saved game. Returns false (leaving the current game untouched) if the save is not usable.</summary>
    public bool Restore(string saved)
    {
        try
        {
            var parts = saved.Split('|');
            if (parts.Length != 4) return false;
            int savedNextId = int.Parse(parts[1]);
            int savedAi = int.Parse(parts[2]);
            var idBytes = Convert.FromBase64String(parts[3]);

            int oldAi = opt.Ai;
            opt.Ai = Math.Clamp(savedAi, 0, 3);
            GameState state;
            GameHistory hist;
            if (!GameStateCodec.TryDecodeGame(parts[0], BuildConfig(), out state, out hist) || idBytes.Length != (hist.Count + 1) * 128)
            {
                opt.Ai = oldAi;
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
                    list.Add(new PieceView { Id = id, Type = c.Type, Side = c.Side, Square = sq, Buffs = c.Buffs, Stun = c.StunTurns, Armor = c.ArmorTurns });
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
            nextId = Math.Max(savedNextId, 1);
            ai = new Ai(opt.Ai switch { 1 => 1, 3 => 3, _ => 2 });
            Effects.Clear();
            Toasts.Clear();
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
        var area = new List<int>();
        Geometry.AreaAround(bomber, Geometry.BombRadius(piece), area);
        foreach (int sq in area)
        {
            BlastArea.Add(sq);
            var c = State.Cells[sq];
            if (Geometry.IsVulnerableToBomb(c))
            {
                Doomed.Add(sq);
                if (c.Side == piece.Side) BombOwnLosses++; else BombEnemyLosses++;
            }
            else if (State.Boulders[sq]) Doomed.Add(sq);
            else if (!c.IsEmpty) Spared.Add(sq);
        }
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

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
        if (!CanUse(kind)) return;
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
            _ => PowerupKind.Reroll,
        });
        foreach (var a in Legal.Where(a => a.Kind == kind))
            Targets.Add(a.To);
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    void SelectPiece(int sq)
    {
        ClearSelection();
        bool mine = !State.Cells[sq].IsEmpty && State.Cells[sq].Side == State.ToMove;
        if (!mine) return;
        var acts = Legal.Where(a => a.From == sq && a.Kind == ActionKind.Move).ToList();
        bool special = Legal.Any(a => (a.Kind == ActionKind.Detonate || a.Kind == ActionKind.Fireball) && a.From == sq);
        if (acts.Count == 0 && !special) return;
        Selected = sq;
        foreach (var a in acts) Targets.Add(a.To);
        Sfx?.Invoke("click");
        var held = State.Cells[sq];
        foreach (var flag in new[] { Buff.Fireball, Buff.DoubleMove, Buff.Poison, Buff.Armor, Buff.SmallBomb, Buff.BigBomb })
        {
            if (!held.Has(flag)) continue;
            var kind = flag == Buff.Armor ? PowerupKind.SpikedArmor : Names.ForBuff(flag);
            if (opt.Tutorials && Tut.ShouldShow(kind)) { MaybeTutorial(kind); break; }
        }
    }

    public async Task TapSquare(int sq)
    {
        if (!HumanTurn || PickupPending || Mode == InputMode.Promotion) return;

        if (Mode == InputMode.BombPreview)
        {
            Cancel(); // tapping the board backs out; the Confirm button is the only way to detonate
            return;
        }

        if (Mode == InputMode.Armed)
        {
            if (!Targets.Contains(sq)) { Cancel(); return; }
            var cands = Legal.Where(a => a.Kind == ArmedKind && a.To == sq).ToList();
            await Human(cands[0]);
            return;
        }

        if (Mode == InputMode.FireAim)
        {
            if (!Targets.Contains(sq)) { Cancel(); return; }
            await Human(Legal.First(a => a.Kind == ActionKind.Fireball && a.From == Selected && a.To == sq));
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
        Inspect = -1;
        if (Selected == sq && State.ForcedMover < 0)
        {
            ClearSelection();
        }
        else
        {
            if (State.ForcedMover < 0) SelectPiece(sq);
            if (IsPowered(sq) && !wasInspected) Inspect = sq;
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
        await Play(a);
        await AiLoop();
    }

    // ---------- turn flow ----------

    async Task AiLoop()
    {
        while (AiToMove)
        {
            Thinking = true;
            Busy = true;
            Changed?.Invoke();
            await Task.Delay(550); // let the UI paint before the (synchronous) search
            GameAction act;
            if (opt.Ai == 1 && rnd.NextDouble() < 0.3) act = Legal[rnd.Next(Legal.Count)];
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                act = ai.Choose(State);
                Console.WriteLine($"[ai] depth {ai.Depth} chose {act} in {sw.ElapsedMilliseconds} ms");
            }
            Thinking = false;
            await Play(act);
        }
        Busy = false;
        AutoSelectForced();
        Changed?.Invoke();
    }

    async Task Play(GameAction a)
    {
        Busy = true;
        Inspect = -1;
        reviewIndex = -1;
        ClearSelection();
        history.Record(State, a);
        viewHistory.Add(SnapshotViews());
        var ev = new List<GameEvent>();
        Rules.Apply(State, a, ev);
        Moves++;
        Process(ev);
        SyncViews();
        Refresh();
        Changed?.Invoke();

        bool big = ev.Any(e => e.Kind == EventKind.Detonated || e.Kind == EventKind.FireballFired);
        bool hit = ev.Any(e => e.Kind == EventKind.Captured || e.Kind == EventKind.Destroyed || e.Kind == EventKind.Converted);
        await Task.Delay(big ? 1000 : hit ? 700 : 450);

        if (GameOver)
        {
            Sfx?.Invoke(Result == GameResult.Draw ? "draw" : (VsAi && Winner() == Side.Black ? "lose" : "win"));
        }
        Busy = false;
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

    public string Status
    {
        get
        {
            if (GameOver && !Reviewing) return ResultReason == "" ? "Game over" : ResultReason;
            if (Reviewing) return "Reviewing past moves. Nothing here changes the game.";
            if (Thinking) return "Opponent is thinking...";
            if (Busy) return "";
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
            if (Mode == InputMode.BombPreview)
                return $"Bomb: destroys {BombOwnLosses} of your pieces and {BombEnemyLosses} of theirs. Confirm or cancel.";
            if (Mode == InputMode.Promotion) return "Promote to...";
            if (State.ForcedMover >= 0) return "Double Move! Move that piece again";
            string who = VsAi ? "Your move" : (State.ToMove == Side.White ? "White to move" : "Black to move");
            return InCheck ? who + " - you are in check!" : who;
        }
    }

    // ---------- review (read-only) and Rewind (take back) ----------

    List<PieceView> SnapshotViews() => Pieces.Where(p => !p.Dying).Select(Copy).ToList();

    static PieceView Copy(PieceView p) => new()
    {
        Id = p.Id, Type = p.Type, Side = p.Side, Square = p.Square, Buffs = p.Buffs,
        Stun = p.Stun, Armor = p.Armor, AnimSeq = 0,
    };

    (int from, int to) LastOf(int position)
    {
        if (position <= 0 || position > history.Count) return (-1, -1);
        var a = history[position - 1].Action;
        return a.Kind == ActionKind.Move ? (a.From, a.To) : (-1, -1);
    }

    /// <summary>Move the review position by <paramref name="delta"/>. Stepping past the newest position returns to the live game.</summary>
    public void ReviewStep(int delta) => ReviewTo(ReviewPosition + delta);

    public void ReviewTo(int position)
    {
        if (!CanReview && !Reviewing) return;
        ClearSelection();
        Inspect = -1;
        position = Math.Clamp(position, 0, history.Count);
        reviewIndex = position >= history.Count ? -1 : position;
        Sfx?.Invoke("click");
        Changed?.Invoke();
    }

    public void ExitReview() => ReviewTo(history.Count);

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
        string who = VsAi ? (e.Mover == Side.White ? "You" : "Rival") : (e.Mover == Side.White ? "White" : "Black");
        var a = e.Action;
        string text = a.Kind switch
        {
            ActionKind.Move => $"{e.Before.Cells[a.From].Type} {Sq.Name(a.From)} to {Sq.Name(a.To)}"
                + (!e.Before.Cells[a.To].IsEmpty ? " (capture)" : ""),
            ActionKind.Fireball => $"Fireball from {Sq.Name(a.From)}",
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
        int movedId = -1;
        int fireFrom = -1;
        foreach (var e in events)
        {
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
                    if (v != null) Kill(v);
                    AddFx("burst", e.From, css: fireFrom >= 0 ? "fire" : "boom");
                    if (fireFrom >= 0) AddFx("beam", fireFrom, e.From, css: "fire");
                    break;
                }
                case EventKind.FireballFired:
                    fireFrom = e.From;
                    AddToast("FIREBALL!", "", "fire", "fire");
                    Shake();
                    Sfx?.Invoke("fire");
                    break;
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
                    AddToast("STUNNED!", "Frozen for 3 turns", "stun", "star");
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
                    AddToast(Names.Name(e.Powerup), Names.Desc(e.Powerup), "gold", Names.Icon(e.Powerup));
                    Sfx?.Invoke("pickup");
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
        foreach (var v in Pieces.Where(p => !p.Dying).ToList())
        {
            var c = State.Cells[v.Square];
            if (c.IsEmpty) { Kill(v); continue; }
            v.Type = c.Type;
            v.Side = c.Side;
            v.Buffs = c.Buffs;
            v.Stun = c.StunTurns;
            v.Armor = c.ArmorTurns;
        }
        for (int sq = 0; sq < 64; sq++)
        {
            if (!State.Cells[sq].IsEmpty && At(sq) == null)
            {
                var c = State.Cells[sq];
                Pieces.Add(new PieceView { Id = nextId++, Square = sq, Type = c.Type, Side = c.Side, Buffs = c.Buffs, Stun = c.StunTurns, Armor = c.ArmorTurns });
            }
        }
    }

    void Kill(PieceView v)
    {
        v.Dying = true;
        Later(650, () => Pieces.Remove(v));
    }

    void AddFx(string kind, int sq, int to = -1, int radius = 0, string css = "")
    {
        var fx = new FxItem { Id = nextId++, Kind = kind, Sq = sq, To = to, Radius = radius, Css = css };
        Effects.Add(fx);
        Later(1100, () => Effects.Remove(fx));
    }

    void AddToast(string title, string text, string css, string icon)
    {
        var t = new Toast { Id = nextId++, Title = title, Text = text, Css = css, Icon = icon };
        Toasts.Add(t);
        if (Toasts.Count > 3) Toasts.RemoveAt(0);
        Later(2200, () => Toasts.Remove(t));
    }

    void Shake()
    {
        Shaking = true;
        Later(380, () => Shaking = false);
    }

    async void Later(int ms, Action act)
    {
        await Task.Delay(ms);
        act();
        Changed?.Invoke();
    }
}
