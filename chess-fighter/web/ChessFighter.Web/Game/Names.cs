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
        PowerupKind.SmallBomb => "Small Bomb",
        PowerupKind.BigBomb => "Big Bomb",
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
        _ => "",
    };

    /// <summary>Name with its tier where the power has them: Fireball I / II, Bomb I / II.</summary>
    public static string TierName(PowerupKind k, int tier) => k switch
    {
        PowerupKind.Fireball => tier >= 2 ? "Fireball II" : "Fireball I",
        PowerupKind.SmallBomb => "Bomb I",
        PowerupKind.BigBomb => "Bomb II",
        _ => Name(k),
    };

    /// <summary>Description for a power at a given tier.</summary>
    public static string Desc(PowerupKind k, int tier) => k == PowerupKind.Fireball
        ? (tier >= 2
            ? "Fireball II: fire along ranks, files and diagonals (like a queen). Destroys the first enemy in line instead of moving. Counts as a threat for check. Lost if the piece is destroyed."
            : "Fireball I: fire along ranks and files (like a rook). Destroys the first enemy in line instead of moving. Counts as a threat for check. Land on another Fireball square to upgrade to II.")
        : Desc(k);

    public static string Desc(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "This piece must move twice next time it moves. Giving check cancels the second move.",
        PowerupKind.Poison => "If this piece is captured, the capturer dies too.",
        PowerupKind.SpikedArmor => "Uncapturable and immune to effects for 3 turns. An armored king can't be checked, and may even stand next to the enemy king and check it.",
        PowerupKind.Stun => "Pick any enemy piece. It can't move for 3 turns (kings and armored pieces are immune).",
        PowerupKind.SmallBomb => "Bomb I: detonate instead of moving to destroy everything in the 3x3 around it (not kings). Land on another Bomb square to upgrade to II (5x5).",
        PowerupKind.BigBomb => "Bomb II: detonate instead of moving to destroy everything in the 5x5 around it (not kings).",
        PowerupKind.Fireball => "Fireball I fires along ranks and files like a rook; land on another Fireball square for II, which also fires diagonally like a queen. It destroys the first enemy in line instead of moving, and counts as a threat for check. Lost if the piece is destroyed.",
        PowerupKind.Combo2 => "Instant: this piece moves 2 more times in a row (3 moves in all). Giving check ends the combo. Landing on another Combo adds its moves.",
        PowerupKind.Combo4 => "Instant: this piece moves 4 more times in a row (5 moves in all). Giving check ends the combo. Landing on another Combo adds its moves.",
        PowerupKind.Steal => "Convert an enemy piece that one of your pieces could capture. Your piece stays put.",
        PowerupKind.Boulder => "Place a boulder on an empty square. Nothing can enter or cross it.",
        PowerupKind.Reroll => "Re-draw a powerup you just found, or smash a boulder.",
        PowerupKind.Rewind => "Take back your last turn and your opponent's reply. Use it on your turn.",
        PowerupKind.Ghost => "For 3 turns this piece passes through pieces and boulders. Sliders slide through, pawns leapfrog a blocker, knights also step like a king, and a king reaches two squares. It never lands on its own side or gives check through a blocker.",
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
        _ => PowerupKind.None,
    };

    /// <summary>The powers listed in the help sheet, banked moves first, then instant moves.</summary>
    public static readonly PowerupKind[] Guide =
    {
        PowerupKind.Fireball, PowerupKind.SmallBomb, PowerupKind.Poison, PowerupKind.Ghost,
        PowerupKind.Combo2, PowerupKind.Combo4, PowerupKind.Stun, PowerupKind.Steal, PowerupKind.SpikedArmor,
    };

    public const string PortalDesc = "Teleporters come in matching pairs. Land on one and you are sent to its twin (if it is free); then both vanish.";
}
