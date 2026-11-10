using ChessFighter.Core;

namespace ChessFighter.Web.Game;

public static class Names
{
    public static string Name(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "Double Move",
        PowerupKind.Poison => "Poison",
        PowerupKind.SpikedArmor => "Armor",
        PowerupKind.Stun => "Stun",
        PowerupKind.SmallBomb => "Bomb",
        PowerupKind.BigBomb => "Bomb II",
        PowerupKind.TransformPawn => "Become Pawn",
        PowerupKind.TransformKnight => "Become Knight",
        PowerupKind.TransformBishop => "Become Bishop",
        PowerupKind.TransformRook => "Become Rook",
        PowerupKind.TransformQueen => "Become Queen",
        PowerupKind.Fireball => "Fireball",
        PowerupKind.Steal => "Steal",
        PowerupKind.Boulder => "Boulder",
        PowerupKind.Reroll => "Reroll",
        PowerupKind.Rewind => "Rewind",
        PowerupKind.Ghost => "Ghost",
        PowerupKind.TripleMove => "Triple Move",
        PowerupKind.Swap => "Swap",
        PowerupKind.Combo2 => "Combo +2",
        PowerupKind.Combo4 => "Combo +4",
        PowerupKind.Harpoon => "Harpoon",
        PowerupKind.StunBolt => "Stun",
        PowerupKind.StealGrab => "Steal",
        PowerupKind.DrawCard => "Draw Card",
        _ => "",
    };

    /// <summary>Name with its tier where the power has them: Fireball I / II, Bomb I / II.</summary>
    public static string TierName(PowerupKind k, int tier) => k switch
    {
        PowerupKind.Fireball => tier >= 2 ? "Fireball II" : "Fireball I",
        PowerupKind.Harpoon => tier >= 2 ? "Harpoon II" : "Harpoon I",
        PowerupKind.SmallBomb => "Bomb I",
        PowerupKind.BigBomb => "Bomb II",
        _ => Name(k),
    };

    /// <summary>Description for a power at a given tier.</summary>
    public static string Desc(PowerupKind k, int tier) => k == PowerupKind.Harpoon
        ? (tier >= 2
            ? "Harpoon II: fire a hook on every diagonal at once. Each rival it hooks is dragged toward this piece; one that cannot be pulled any closer is stunned for a turn. Uses your turn, one use."
            : "Harpoon I: fire a hook along one diagonal and drag the first rival in line toward this piece. A rival that cannot be pulled any closer is stunned for a turn. Land on another Harpoon square for II. Uses your turn, one use.")
        : k == PowerupKind.Fireball
        ? (tier >= 2
            ? "Fireball II: fire along ranks, files and diagonals (like a queen). Destroys the first enemy in line instead of moving. Counts as a threat for check. Lost if the piece is destroyed."
            : "Fireball I: fire along ranks and files (like a rook). Destroys the first enemy in line instead of moving. Counts as a threat for check. Land on another Fireball square to upgrade to II.")
        : Desc(k);

    public static string Desc(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "This piece must move twice next time it moves. Giving check cancels the second move.",
        PowerupKind.Poison => "If this piece is captured, the capturer dies too. A king cannot capture it. Wears off after 5 of your turns.",
        PowerupKind.SpikedArmor => "Uncapturable and immune to effects for 3 turns. An armored king can't be checked, and may even stand next to the enemy king and check it.",
        PowerupKind.Stun => "Pick any enemy piece. It can't move for 3 turns (kings and armored pieces are immune).",
        PowerupKind.SmallBomb => "Bomb I: detonate instead of moving to destroy everything in the 3x3 around it (not kings). Land on another Bomb square to upgrade to II (5x5).",
        PowerupKind.BigBomb => "Bomb II: detonate instead of moving to destroy everything in the 5x5 around it (not kings).",
        PowerupKind.Fireball => "Fireball I fires along ranks and files like a rook; land on another Fireball square for II, which also fires diagonally like a queen. It destroys the first enemy in line instead of moving, and counts as a threat for check. Lost if the piece is destroyed.",
        PowerupKind.StunBolt => "Instant: right after landing, fire a stun bolt in any direction. It freezes the first enemy in line (a king too) for 2 turns. Armored pieces are immune. A stunned king with no other way out of check is checkmate.",
        PowerupKind.StealGrab => "Instant: right after landing, take over one rival piece in the 3x3 around this piece. Not kings, not armored pieces. It stays where it is.",
        PowerupKind.DrawCard => "Instant: draw a card into your bag. Everyone can see the cards. Some cards fire straight away, the rest wait until you play them (which costs your turn).",
        PowerupKind.Combo2 => "Instant: this piece moves 2 more times in a row (3 moves in all). Giving check ends the combo. Landing on another Combo adds its moves.",
        PowerupKind.Combo4 => "Instant: this piece moves 4 more times in a row (5 moves in all). Giving check ends the combo. Landing on another Combo adds its moves.",
        PowerupKind.Steal => "Convert an enemy piece that one of your pieces could capture. Your piece stays put.",
        PowerupKind.Boulder => "Place a boulder on an empty square. Nothing can enter or cross it.",
        PowerupKind.Reroll => "Re-draw a powerup you just found, or smash a boulder.",
        PowerupKind.Rewind => "Take back your last turn and your opponent's reply. Use it on your turn.",
        PowerupKind.Ghost => "This piece passes through pieces and boulders for its next 3 moves (standing still costs nothing). Sliders slide through, pawns leapfrog a blocker, knights also step like a king, and a king reaches two squares. It can give check and mate through blockers.",
        PowerupKind.TripleMove => "This piece must move three times in a row next time it moves. Giving check cancels the rest.",
        PowerupKind.Swap => "Trade places between two of your own pieces (not kings). Buffs travel with the pieces. A pawn swapped onto the far rank becomes a Queen. Costs your turn.",
        _ => PowerupInfo.IsTransform(k) ? "This piece changes type on the spot." : "",
    };

    public static string Icon(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "x2",
        PowerupKind.Poison => "skull",
        PowerupKind.SpikedArmor => "shield",
        PowerupKind.Stun => "star",
        PowerupKind.SmallBomb => "bomb",
        PowerupKind.BigBomb => "bigbomb",
        PowerupKind.Fireball => "hadouken",
        PowerupKind.Harpoon => "hook",
        PowerupKind.StunBolt => "bolt",
        PowerupKind.StealGrab => "steal",
        PowerupKind.DrawCard => "card",
        PowerupKind.Combo2 => "plus2",
        PowerupKind.Combo4 => "plus4",
        PowerupKind.Steal => "steal",
        PowerupKind.Boulder => "rock",
        PowerupKind.Reroll => "dice",
        PowerupKind.Rewind => "rewind",
        PowerupKind.Ghost => "ghost",
        PowerupKind.TripleMove => "x3",
        PowerupKind.Swap => "swap",
        _ => PowerupInfo.IsTransform(k) ? "spark" : "question",
    };

    /// <summary>Style class for a powerup square: hidden mystery, a buff for a piece, or a perk for the player.</summary>
    public static string SquareClass(PowerupKind k) => k == PowerupKind.None ? "pu-hidden" : PowerupInfo.IsPerk(k) ? "pu-perk" : "pu-buff";

    public static PowerupKind ForBuff(Buff b) => b switch
    {
        Buff.DoubleMove => PowerupKind.DoubleMove,
        Buff.TripleMove => PowerupKind.TripleMove,
        Buff.Ghost => PowerupKind.Ghost,
        Buff.Poison => PowerupKind.Poison,
        Buff.Fireball => PowerupKind.Fireball,
        Buff.Armor => PowerupKind.SpikedArmor,
        Buff.SmallBomb => PowerupKind.SmallBomb,
        Buff.BigBomb => PowerupKind.BigBomb,
        Buff.Harpoon => PowerupKind.Harpoon,
        _ => PowerupKind.None,
    };

    /// <summary>The powers listed in the help sheet, banked moves first, then instant moves.</summary>
    public static readonly PowerupKind[] Guide =
    {
        PowerupKind.Fireball, PowerupKind.Harpoon, PowerupKind.SmallBomb, PowerupKind.Poison, PowerupKind.Ghost,
        PowerupKind.Combo2, PowerupKind.Combo4, PowerupKind.StunBolt, PowerupKind.StealGrab, PowerupKind.DrawCard, PowerupKind.SpikedArmor,
    };

    // ---------- cards ----------

    public static string CardName(CardKind k) => k switch
    {
        CardKind.NoMorePowers => "No More Powers",
        CardKind.RoleReversal => "Role Reversal",
        CardKind.HorseSacrifice => "Horse Sacrifice",
        CardKind.Promotion => "You Get a Promotion!",
        CardKind.PlaceBoulder => "Okay, I'll Keep Boulders",
        CardKind.SmashBoulder => "Smash Boulder",
        CardKind.SummonCaissa => "Summon Caïssa",
        CardKind.NuhUh => "Nuh-uh",
        CardKind.TakeBack => "Take Back",
        CardKind.SummonMars => "Summon Mars",
        CardKind.DragonStrike => "Dragon Strike",
        CardKind.SummonShiva => "Summon Shiva",
        CardKind.HitRestart => "Hit Restart",
        CardKind.WorldUpsideDown => "World Turned Upside Down",
        CardKind.MissileStrike => "Missile Strike",
        _ => "",
    };

    public static string CardShort(CardKind k) => k switch
    {
        CardKind.NoMorePowers => "No Powers",
        CardKind.RoleReversal => "Role Rev",
        CardKind.HorseSacrifice => "Horse",
        CardKind.Promotion => "Promote",
        CardKind.PlaceBoulder => "Boulder",
        CardKind.SmashBoulder => "Smash",
        CardKind.SummonCaissa => "Caïssa",
        CardKind.NuhUh => "Nuh-uh",
        CardKind.TakeBack => "Take Back",
        CardKind.SummonMars => "Mars",
        CardKind.DragonStrike => "Dragon",
        CardKind.SummonShiva => "Shiva",
        CardKind.HitRestart => "Restart",
        CardKind.WorldUpsideDown => "World",
        CardKind.MissileStrike => "Missile",
        _ => "",
    };

    public static string CardDesc(CardKind k) => k switch
    {
        CardKind.NoMorePowers => "Removes every active power from every piece and every powerup square. New squares return after a round. At will.",
        CardKind.RoleReversal => "Switch any 2 of your pieces (not kings). At will.",
        CardKind.HorseSacrifice => "Sacrifice a knight to turn a pawn, bishop or another knight into a rook. At will.",
        CardKind.Promotion => "Promote one piece a level: pawn to knight or bishop, knight or bishop to rook, rook to queen. Used straight away.",
        CardKind.PlaceBoulder => "Put a boulder on an empty square. Used straight away.",
        CardKind.SmashBoulder => "Remove one boulder. At will.",
        CardKind.SummonCaissa => "Bring one of your captured pieces back, on any free square. At will.",
        CardKind.NuhUh => "Cancel the card your rival just played. Used as a reaction, never on your own turn.",
        CardKind.TakeBack => "Undo a round of moves: the board returns to the start of your previous turn. At will.",
        CardKind.SummonMars => "Mars shows you the best move at the start of your next 3 turns. At will.",
        CardKind.DragonStrike => "A dragon burns every piece on one row (rows 3 to 6, not a row holding a king). At will.",
        CardKind.SummonShiva => "Shiva strikes up to 3 random rival pieces with falling weapons. Never kings. At will.",
        CardKind.HitRestart => "Every piece goes back to its starting square and keeps its powers. Captured pieces stay captured. Used straight away.",
        CardKind.WorldUpsideDown => "The board flips 180 degrees and you command the other army. Used straight away.",
        CardKind.MissileStrike => "Pick a quadrant. In 10 moves a missile destroys everything there. A king still inside loses the game. At will.",
        _ => "",
    };

    public static string CardIcon(CardKind k) => k switch
    {
        CardKind.NoMorePowers => "ban",
        CardKind.RoleReversal => "swap",
        CardKind.HorseSacrifice => "skull",
        CardKind.Promotion => "crown",
        CardKind.PlaceBoulder => "rock",
        CardKind.SmashBoulder => "hammer",
        CardKind.SummonCaissa => "spark",
        CardKind.NuhUh => "hand",
        CardKind.TakeBack => "rewind",
        CardKind.SummonMars => "star",
        CardKind.DragonStrike => "hadouken",
        CardKind.SummonShiva => "bolt",
        CardKind.HitRestart => "history",
        CardKind.WorldUpsideDown => "portal",
        CardKind.MissileStrike => "bigbomb",
        _ => "card",
    };

    public const string PortalDesc = "Teleporters come in matching pairs. Land on one and you are sent to its twin (if it is free); then both vanish.";
}
