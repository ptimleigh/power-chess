using PowerChess.Core;

namespace PowerChess.Web.Game;

public static class Names
{
    public static string Name(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "Double Move",
        PowerupKind.Poison => "Poison",
        PowerupKind.SpikedArmor => "Spiked Armor",
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
        _ => "",
    };

    public static string Desc(PowerupKind k) => k switch
    {
        PowerupKind.DoubleMove => "This piece must move twice next time it moves. Giving check cancels the second move.",
        PowerupKind.Poison => "If this piece is captured, the capturer dies too.",
        PowerupKind.SpikedArmor => "Uncapturable and immune to effects for 3 turns.",
        PowerupKind.Stun => "Pick any enemy piece. It can't move for 3 turns (kings and armored pieces are immune).",
        PowerupKind.SmallBomb => "Detonate instead of moving: destroys everything in the 3x3 around it (not kings).",
        PowerupKind.BigBomb => "Detonate instead of moving: destroys everything in the 5x5 around it (not kings).",
        PowerupKind.Fireball => "This piece can fire a fireball once, instead of moving: pick a direction and it destroys the first enemy in line. A Queen burns the whole line. Lost if the piece is destroyed.",
        PowerupKind.Steal => "Convert an enemy piece that one of your pieces could capture. Your piece stays put.",
        PowerupKind.Boulder => "Place a boulder on an empty square. Nothing can enter or cross it.",
        PowerupKind.Reroll => "Re-draw a powerup you just found, or smash a boulder.",
        PowerupKind.Rewind => "Take back your last turn and your opponent's reply. Use it on your turn.",
        PowerupKind.Ghost => "For 3 turns this rook, bishop or queen slides through pieces and boulders. It still can't land on its own side or give check through a blocker.",
        PowerupKind.TripleMove => "This piece must move three times in a row next time it moves. Giving check cancels the rest.",
        PowerupKind.Swap => "Trade places between two of your own pieces (not kings). Buffs travel with the pieces. Costs your turn.",
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
        PowerupKind.Fireball => "fire",
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

    public static readonly PowerupKind[] Guide =
    {
        PowerupKind.DoubleMove, PowerupKind.Poison, PowerupKind.SpikedArmor,
        PowerupKind.SmallBomb, PowerupKind.BigBomb, PowerupKind.TransformQueen, PowerupKind.Fireball,
        PowerupKind.Stun, PowerupKind.Steal, PowerupKind.Boulder, PowerupKind.Reroll, PowerupKind.Rewind,
        PowerupKind.TripleMove, PowerupKind.Ghost, PowerupKind.Swap,
    };

    public const string PortalDesc = "Teleporters come in matching pairs. Land on one and you are sent to its twin (if it is free); then both vanish.";
}
